module VibeFs.Omp.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Config
open VibeFs.Kernel.HostTools
open VibeFs.Omp.Codec
open VibeFs.Kernel.BacklogProjection
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.MessageTransformPolicy
open VibeFs.Omp.CapsCodec
open VibeFs.Omp.ChildSession
open VibeFs.Omp.KnowledgeGraph.Runtime
open VibeFs.Omp.MagicTodo
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.ReadDedup
open VibeFs.Shell.Dyn
open VibeFs.Shell.FileSys
open VibeFs.Shell.MessageTransformCore
open VibeFs.Shell.MessageTransformHostEntry
open VibeFs.Shell.MessageTransformPipeline
open VibeFs.Shell.OmpCaps
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.TreeSitterShell

module Dyn = VibeFs.Shell.Dyn

let private defaultBacklogSession = BacklogSession omp

let private resolveAgent (ctx: obj) : string =
    let sm = Dyn.get ctx "sessionManager"
    if Dyn.isNullish sm then "manager"
    else
        let name = Dyn.str sm "agentName"
        if name <> "" then name else "manager"

let transformEntriesAsyncWithAgent (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) (cwd: string) (sessionId: string)
    (entriesObj: obj) (agent: string) : JS.Promise<obj array> =
    promise {
        if Dyn.isNullish entriesObj || not (Dyn.isArray entriesObj) then return unbox<obj array> entriesObj
        else
            let entriesArr = unbox<obj array> entriesObj
            if entriesArr.Length = 0 then return entriesArr
            else
                kgRuntime.BindGetEntries(fun () -> entriesArr)
                let messagesList = decodeEntries sessionId entriesArr
                let excluded = shouldExcludeAgentFromProjection agent (isChildSession sessionId)
                let cleaned = stripSyntheticBySource messagesList
                let backlogOps =
                    backlogSessionOpsFrom defaultBacklogSession.Host (fun sid msgs -> defaultBacklogSession.GetOrRebuildBacklog(sid, msgs))
                let plan = {
                    SessionID = sessionId
                    Agent = agent
                    Directory = cwd
                    Excluded = excluded
                    Cleaned = cleaned
                }
                let replayTexts () = extractHistoryTexts messagesList |> Seq.ofList
                let dedupFn excluded encoded =
                    if excluded then encoded
                    else
                        applyReadDedup encoded
                        encoded
                let loadCaps () : JS.Promise<CapsFile list> =
                    promise {
                        if plan.Excluded || cwd = "" then return ([] : CapsFile list)
                        else
                            let! ompFiles = findOmpCapsFiles cwd
                            return
                                ompFiles
                                |> List.map (fun (f: OmpCapsFile) ->
                                    { filePath = f.filePath
                                      label = f.label
                                      content = f.content } : CapsFile)
                    }
                let loadKgPrelude () =
                    if plan.Excluded then Promise.lift None
                    elif not (canUse agent "knowledge_graph_fetch") then Promise.lift None
                    else kgRuntime.BuildPreludeForSession(sessionId, cwd)
                let buildCaps encoded (capsFiles: CapsFile list) prelude =
                    let ompCaps =
                        capsFiles
                        |> List.map (fun f -> { filePath = f.filePath; label = f.label; content = f.content } : OmpCapsFile)
                    buildCapsEntries sha256HexTruncated encoded cwd ompCaps prelude
                return!
                    runHostMessagesTransform
                        reviewStore
                        sessionId
                        IfStoreEmpty
                        replayTexts
                        plan
                        backlogOps
                        encodeMessages
                        dedupFn
                        loadCaps
                        loadKgPrelude
                        buildCaps
    }

let transformEntriesAsync (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) (cwd: string) (sessionId: string)
    (entriesObj: obj) : JS.Promise<obj array> =
    transformEntriesAsyncWithAgent reviewStore kgRuntime cwd sessionId entriesObj "manager"

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
            let agent = resolveAgent ctx
            let entries = Dyn.get event "entries"
            if Dyn.isNullish entries then return event
            else
                let! transformed = transformEntriesAsyncWithAgent reviewStore kgRuntime cwd sessionId entries agent
                event?entries <- transformed
                return event
        }
    pi?on("context", box run)
    pi?on("before_context", box run)

