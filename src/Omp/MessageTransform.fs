module VibeFs.Omp.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Config
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.LoopMessages
open VibeFs.Omp.Codec
open VibeFs.Kernel.BacklogProjection
open VibeFs.Kernel.Messaging
open VibeFs.Omp.CapsCodec
open VibeFs.Omp.KnowledgeGraphRuntime
open VibeFs.Omp.MagicTodo
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.ReadDedup
open VibeFs.Shell.Dyn
open VibeFs.Shell.FileSys
open VibeFs.Shell.OmpCaps
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.TreeSitterShell

module Dyn = VibeFs.Shell.Dyn

/// Omp 路径不实现 Opencode 风格的 `resolveAgent`（无 ChildAgentRegistry
/// 注入），假定主代理永远是 `manager`。若未来需要支持子 agent 上下文
/// prelude，从 ctx.sessionManager.agentName 读即可。
let private managerAgent = "manager"

let private defaultBacklogSession = BacklogSession(omp)

let transformEntriesAsync (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) (cwd: string) (sessionId: string)
    (entriesObj: obj) : JS.Promise<obj array> =
    promise {
        if Dyn.isNullish entriesObj || not (Dyn.isArray entriesObj) then return entriesObj :?> obj array
        else
            let entriesArr = entriesObj :?> obj array
            if entriesArr.Length = 0 then return entriesArr
            else
                kgRuntime.BindGetEntries(fun () -> entriesArr)
                let messagesList = decodeEntries sessionId entriesArr
                inferReviewTaskFromTexts (extractHistoryTexts messagesList)
                |> syncReviewProjection reviewStore sessionId
                let cleaned = stripSyntheticBySource messagesList
                let backlog = defaultBacklogSession.GetOrRebuildBacklog(sessionId, cleaned)
                let afterMagic = projectBacklogFor defaultBacklogSession.Host cleaned backlog false sessionId
                let encoded = encodeMessages afterMagic
                applyReadDedup encoded
                let! capsFiles = findOmpCapsFiles cwd
                let! knowledgeGraphPrelude =
                    if canUse managerAgent "knowledge_graph_fetch" then
                        kgRuntime.BuildPreludeForSession(sessionId, cwd)
                    else
                        Promise.lift (None: string option)
                let final =
                    if hasExistingCapsMessages encoded then encoded
                    else buildCapsEntries sha256HexTruncated encoded cwd capsFiles knowledgeGraphPrelude
                return final
    }

let beforeAgentStart (_cwd: string) (systemPrompt: obj) : JS.Promise<obj> =
    promise {
        let stripped = stripHostAgentsPrompt systemPrompt
        return createObj [ "systemPrompt", box stripped ]
    }

let appendToolResultSyntax (cwd: string) (event: obj) : JS.Promise<unit> =
    promise {
        let toolName = Dyn.str event "toolName"
        if toolName <> "read" && toolName <> "write" && toolName <> "edit" then return ()
        let args = Dyn.get event "args"
        let content = Dyn.str event "content"
        let paths = extractFilePaths args
        match paths |> List.tryHead with
        | None -> ()
        | Some path ->
            let! extra = appendSyntaxDiagnostics path content false
            match extra with
            | None -> ()
            | Some diag ->
                let existing = Dyn.str event "content"
                event?content <- existing + "\n" + diag
    }

let registerContextTransform (pi: obj) (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) : unit =
    let run (event: obj) (ctx: obj) =
        promise {
            let cwd = Dyn.str ctx "cwd"
            let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
            let entries = Dyn.get event "entries"
            if Dyn.isNullish entries then return event
            else
                let! transformed = transformEntriesAsync reviewStore kgRuntime cwd sessionId entries
                event?entries <- transformed
                return event
        }
    pi?on("context", box run)
    pi?on("before_context", box run)

/// Host-agnostic entrypoint mirroring Opencode's `messagesTransform`. The
/// `output.entries` array is rewritten in-place when a `context` event lands,
/// and we expose this as a stand-alone function so future `experimental.chat
/// .messages.transform`-style plumbing can call it without going through the
/// `pi?on("context", ...)` registration path.
let messagesTransform (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) (cwd: string)
                      (sessionId: string) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let entries =
            let fromOutput = Dyn.get output "entries"
            if Dyn.isNullish fromOutput then Dyn.get input "entries" else fromOutput
        if Dyn.isNullish entries then ()
        else
            let! transformed = transformEntriesAsync reviewStore kgRuntime cwd sessionId entries
            output?entries <- transformed
    }

/// Omp has no host-driven compacting pass; kept as a no-op to mirror the
/// opencode signature so a future `experimental.session.compacting` hook in
/// the pi host can wire through without an F# change.
let compactingHandler (_backlogSession: BacklogSession) (_input: obj) (_output: obj) : JS.Promise<unit> =
    Promise.lift ()

/// Omp's `before_agent_start` is a separate hook (`beforeAgentStart` above);
/// the opencode-style `system.transform` has no pi analogue today, so this is
/// a passthrough.
let systemTransform (_input: obj) (_output: obj) : JS.Promise<unit> =
    Promise.lift ()