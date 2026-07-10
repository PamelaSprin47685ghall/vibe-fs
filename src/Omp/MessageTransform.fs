module Wanxiangshu.Omp.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Omp
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Kernel.BacklogProjection
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Omp.CapsCodec
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.MagicTodo
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.ToolResultEvent
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FileSys
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformHostEntry
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.OmpCaps
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.TreeSitterShell
open Wanxiangshu.Shell.RuntimeScope

module Dyn = Wanxiangshu.Shell.Dyn

let private defaultBacklogSession = BacklogSession omp

let private resolveAgent (ctx: obj) : string =
    let sm = Dyn.get ctx "sessionManager"

    if Dyn.isNullish sm then
        "manager"
    else
        let name = Dyn.str sm "agentName"
        if name <> "" then name else "manager"

let transformEntriesAsyncWithAgent
    (reviewStore: ReviewStore)
    (cwd: string)
    (sessionId: string)
    (entriesObj: obj)
    (agent: string)
    (getContextUsage: obj array -> JS.Promise<int option>)
    (ctx: obj)
    : JS.Promise<obj array> =
    promise {
        if Dyn.isNullish entriesObj || not (Dyn.isArray entriesObj) then
            return unbox<obj array> entriesObj
        else
            let entriesArr = unbox<obj array> entriesObj

            if entriesArr.Length = 0 then
                return entriesArr
            else
                let messagesList = decodeEntries sessionId entriesArr

                let excluded =
                    shouldExcludeAgentFromProjection agent (isChildSession ExecutorTools.ompScope sessionId)

                let cleaned = stripSyntheticBySource messagesList

                defaultBacklogSession.WorkspaceRoot <- cwd

                let backlogOps =
                    backlogSessionOpsFrom defaultBacklogSession.Host (fun sid msgs ->
                        defaultBacklogSession.GetOrRebuildBacklog(sid, msgs))

                let! maxInputTokens = Wanxiangshu.Shell.ContextBudgetUsageCodec.resolveMaxInputTokens [ ctx ] sessionId

                let plan =
                    { SessionID = sessionId
                      Agent = agent
                      Directory = cwd
                      Excluded = excluded
                      IsSubagentSession = isChildSession ExecutorTools.ompScope sessionId
                      Cleaned = cleaned
                      RawArray = Some entriesArr
                      SembleInjectEnabled = false
                      Scope = ExecutorTools.ompScope
                      MaxInputTokens = maxInputTokens
                      GetContextUsage = getContextUsage }


                let replayTexts () : JS.Promise<string seq> =
                    Promise.lift (extractHistoryTexts messagesList |> Seq.ofList)

                let injectFn _ encoded = Promise.lift encoded

                let loadCaps () : JS.Promise<CapsFile list> =
                    promise {
                        if plan.Excluded || cwd = "" then
                            return ([]: CapsFile list)
                        else
                            let! ompFiles = findOmpCapsFiles cwd

                            let baseFiles =
                                ompFiles
                                |> List.map (fun f ->
                                    ({ filePath = f.filePath
                                       label = f.label
                                       content = f.content }
                                    : CapsFile))

                            return!
                                Wanxiangshu.Shell.MessageTransformHostHooks.injectSubagentFilesIfAny
                                    ExecutorTools.ompScope
                                    plan
                                    baseFiles
                    }

                let buildCaps encoded (capsFiles: CapsFile list) prelude =
                    let ompCaps =
                        capsFiles
                        |> List.map (fun f ->
                            { filePath = f.filePath
                              label = f.label
                              content = f.content })

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
                        injectFn
                        loadCaps
                        buildCaps
    }

let transformEntriesAsync
    (reviewStore: ReviewStore)
    (cwd: string)
    (sessionId: string)
    (entriesObj: obj)
    : JS.Promise<obj array> =
    transformEntriesAsyncWithAgent
        reviewStore
        cwd
        sessionId
        entriesObj
        "manager"
        (fun _ -> Promise.lift None)
        (box null)

let beforeAgentStart (_cwd: string) (systemPrompt: obj) : JS.Promise<obj> =
    promise {
        let stripped = stripHostAgentsPrompt systemPrompt
        return createObj [ "systemPrompt", box stripped ]
    }

let appendToolResultSyntax (cwd: string) (event: obj) : JS.Promise<unit> =
    promise {
        let toolName = Dyn.str event "toolName"

        if toolName <> "read" && toolName <> "write" && toolName <> "edit" then
            return ()

        let args = getToolInput event
        let content = getToolResultText event
        let paths = extractFilePaths args

        match paths |> List.tryHead with
        | None -> ()
        | Some path ->
            let! extra = appendSyntaxDiagnostics path content false

            match extra with
            | None -> ()
            | Some diag -> setToolResultText event (content + "\n" + diag)
    }

let registerContextTransform (pi: obj) (reviewStore: ReviewStore) : unit =
    let run (event: obj) (ctx: obj) =
        promise {
            let cwd = Dyn.str ctx "cwd"
            let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
            let agent = resolveAgent ctx
            let entries = Dyn.get event "entries"

            if Dyn.isNullish entries then
                return event
            else
                let getContextUsage =
                    match Wanxiangshu.Shell.ContextBudgetUsageCodec.tryGetRealContextUsage ctx sessionId with
                    | Some f -> f
                    | None -> fun _ -> Promise.lift None

                let! transformed =
                    transformEntriesAsyncWithAgent reviewStore cwd sessionId entries agent getContextUsage ctx

                event?entries <- transformed
                return event
        }

    pi?on ("context", box run)
    pi?on ("before_context", box run)
