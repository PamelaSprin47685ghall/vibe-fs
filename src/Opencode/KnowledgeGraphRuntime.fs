module Wanxiangshu.Opencode.KnowledgeGraphRuntime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.Prompts
open Wanxiangshu.Kernel.KnowledgeGraph.Maintenance
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.KnowledgeGraphStorage
open Wanxiangshu.Shell.KnowledgeGraphWorkflow
open Wanxiangshu.Shell.KnowledgeGraphMaintenanceRun
open Wanxiangshu.Shell.KnowledgeGraphBookkeeperLaunch
open Wanxiangshu.Shell.KnowledgeGraphRuntimeTestPorts
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Opencode.KnowledgeGraphRuntimeIO
open Wanxiangshu.Shell.DelegatedAiSettings

/// KnowledgeGraph host IO shell (P53/P72): holds the single mutable state cell and
/// serializes every state+IO change through `commandQueue`. All pure state
/// transitions live in `Kernel.KnowledgeGraphRuntimeState`; the stateless IO orchestration
/// (prompt building, file IO, background session launch) lives in
/// `KnowledgeGraphRuntimeIO`. Synchronous methods (RegisterJob/DeleteJob/MarkRwTool/...)
/// run to completion in a single tick and cannot dangle a JS.Promise mid
/// state-change; the only async-with-await method that holds a job context
/// across an await is `Submit`, which caches `ctx` before awaiting and uses an
/// idempotent `RemoveJobCmd` on exit so a synchronous `DeleteJob` raised by a
/// stream-abort/session.delete event during the await cannot corrupt it.
type KnowledgeGraphRuntime(client: obj, initialWorkspaceRoot: string, nowUtc: unit -> System.DateTime, registry: ChildAgentRegistry, portLockTimeoutMs: int64, portLockRetryDelayMs: int) =
    let mutable state = initialKnowledgeGraphState
    let commandQueue = SerialQueue()
    let mutable writeQueues = Map.empty<string, SerialQueue>
    let mutable registeredJobs = Map.empty<string, KnowledgeGraphJobContext>
    let workspaceRoot = initialWorkspaceRoot

    let today () = (nowUtc ()).ToString("yyyy-MM-dd")

    let applyCmd (cmd: KnowledgeGraphCommand) : unit = state <- reducer state cmd

    let backgroundSink =
        createSink (fun title result -> applyCmd (UpdateLatestLaunchResultCmd (title, result)))

    let getWorkspaceQueue (root: string) =
        match Map.tryFind root writeQueues with
        | Some queue -> queue
        | None ->
            let queue = SerialQueue()
            writeQueues <- Map.add root queue writeQueues
            queue

    let runWorkspace (root: string) (work: unit -> JS.Promise<string>) : JS.Promise<string> =
        getWorkspaceQueue root |> fun queue -> queue.Enqueue(work)

    let effectiveWorkspaceRoot (value: string) : string =
        if System.String.IsNullOrWhiteSpace value then workspaceRoot else value

    let testPorts =
        createFromStateQueueSink
            (fun () -> state)
            (fun s -> state <- s)
            (fun work -> commandQueue.Enqueue work)
            backgroundSink

    let launchBg root parentID kind title buildPrompt aiSettings =
        queueBackgroundLaunch
            client
            (trackBackgroundJob backgroundSink)
            (recordLaunchResult backgroundSink title)
            root
            parentID
            kind
            title
            buildPrompt
            aiSettings
            registry

    member _.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<KnowledgeGraphProjection> =
        if sessionID = "" then Promise.lift Map.empty
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    match Map.tryFind sessionID state.sessionSnapshots with
                    | Some projection -> return projection
                    | None ->
                        let! projection = readProjectionForRoot (effectiveWorkspaceRoot directory)
                        applyCmd (CacheSnapshotCmd (sessionID, projection))
                        return projection
                })

    member _.RegisterJob(_sessionID: string, _ctx: KnowledgeGraphJobContext) : unit =
        registeredJobs <- Map.add _sessionID _ctx registeredJobs

    member _.TakeJob(_sessionID: string) : KnowledgeGraphJobContext option =
        Map.tryFind _sessionID registeredJobs

    member _.DeleteJob(sessionID: string) : unit =
        registry.UnregisterChildAgent(sessionID)
        registeredJobs <- Map.remove sessionID registeredJobs

    member this.Submit(sessionID: string, drafts: KnowledgeGraphDraft list) : JS.Promise<string> =
        this.SubmitFromHistory(sessionID, "", drafts)

    member this.SubmitFromHistory(sessionID: string, directory: string, drafts: KnowledgeGraphDraft list) : JS.Promise<string> =
        let root = effectiveWorkspaceRoot directory
        if not (knowledgeGraphDirExists root) then Promise.lift "Knowledge graph directory not found."
        else
            promise {
                // Early idempotency gate: if chat history already contains a completed
                // return_bookkeeper tool call, reject immediately without any IO.
                let! historyMessages = loadSessionMessages client root sessionID
                if historyHasCompletedReturnBookkeeper historyMessages then
                    return rejectSecondReturnBookkeeperMessage
                else
                    let! result, kindOpt, parentID =
                        commandQueue.Enqueue(fun () ->
                            promise {
                                let! reconstructed = tryResolveJobContext client root sessionID
                                match reconstructed |> Option.orElseWith (fun () -> Map.tryFind sessionID registeredJobs) with
                                | None -> return "No active knowledge graph job for this session.", None, None
                                | Some ctx ->
                                    let parentID = registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)
                                    try
                                        let! result = runWorkspace ctx.workspaceRoot (fun () -> submitForKind portLockTimeoutMs portLockRetryDelayMs (today ()) ctx.workspaceRoot ctx.kind drafts)
                                        return result, Some ctx.kind, parentID
                                    finally
                                        registry.UnregisterChildAgent(sessionID)
                                        registeredJobs <- Map.remove sessionID registeredJobs
                            })
                    match kindOpt with
                    | Some (DailyRewrite _) ->
                        this.StartMaintenanceIfDue(root, ?parentSessionID = parentID) |> ignore
                    | _ -> ()
                    return result
            }

    member this.BuildPreludeForSession(sessionID: string, directory: string) : JS.Promise<string option> =
        promise {
            if not (knowledgeGraphDirExists (effectiveWorkspaceRoot directory)) then return None
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                return buildPreludeSection projection
        }

    member this.FetchFromSessionSnapshot(sessionID: string, directory: string, entity: string) : JS.Promise<string> =
        promise {
            if not (knowledgeGraphDirExists (effectiveWorkspaceRoot directory)) then
                return "Knowledge graph directory not found."
            elif sessionID = "" then
                return "Knowledge graph snapshot unavailable for this session."
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                match fetchAnswer projection entity with
                | Ok answer -> return answer
                | Error message -> return message
        }
    member _.StartMaintenanceIfDue(workspaceRoot: string, ?parentSessionID: string) : JS.Promise<unit> =
        let root = effectiveWorkspaceRoot workspaceRoot
        runMaintenanceIfDue commandQueue
            { WorkspaceRoot = root
              GetState = fun () -> state
              SetState = fun s -> state <- s
              Now = nowUtc
              TryLaunch =
                fun date files projection ->
                    let _, launch = bookkeeperMaintenanceLaunch root date
                    launchBg root parentSessionID (DailyRewrite date) launch.title
                        (fun () -> Promise.lift (buildDailyPrompt date files projection))
                        emptySettings }

    member _.RecordBookkeeperLaunch(agent: string, title: string, prompt: string, result: string) : unit =
        applyCmd (RecordLaunchCmd { agent = agent; title = title; prompt = prompt; result = result })

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, ?parentSessionID: string, ?aiSettings: DelegatedAiSettings) : unit =
        let root = effectiveWorkspaceRoot workspaceRoot
        if not (knowledgeGraphDirExists root) then ()
        else
            this.RecordBookkeeperLaunch("bookkeeper", title, prompt, result)
            let settings = defaultArg aiSettings emptySettings
            this.StartMaintenanceIfDue(root, ?parentSessionID = parentSessionID) |> ignore
            launchBg root parentSessionID AppendAfterWork title (fun () ->
                promise {
                    let! projection = readProjectionForRoot root
                    return buildAppendPrompt title prompt result projection
                }) settings

    member internal _.CreateTestPorts() : KnowledgeGraphRuntimeTestPorts = testPorts
