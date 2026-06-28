module Wanxiangshu.Mux.KnowledgeGraphRuntimeMux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Kernel.KnowledgeGraph.Maintenance
open Wanxiangshu.Kernel.KnowledgeGraph.Prompts
open Wanxiangshu.Mux.Delegate
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Mux.MessagingCodec
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.KnowledgeGraphStorage
open Wanxiangshu.Shell.KnowledgeGraphWorkflow
open Wanxiangshu.Shell.KnowledgeGraphMaintenanceRun
open Wanxiangshu.Shell.KnowledgeGraphBookkeeperLaunch
open Wanxiangshu.Shell.KnowledgeGraphRuntimeTestPorts
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.ToolContextCodec
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Mux.KnowledgeGraphRuntimeIO

type MuxKnowledgeGraphRuntime(?deps: obj) as this =
    let mutable registeredJobs = Map.empty<string, KnowledgeGraphJobContext>
    let writeQueue = SerialQueue()
    let commandQueue = SerialQueue()
    let mutable state = initialKnowledgeGraphState
    let backgroundSink =
        createSink (fun title result ->
            state <- reducer state (UpdateLatestLaunchResultCmd (title, result)))
    let mutable latestConfig : obj option = None

    let nowUtc =
        match deps with
        | Some d when not (Dyn.isNullish d) ->
            let getNow = Dyn.get d "nowUtc"
            if not (Dyn.isNullish getNow) && Dyn.typeIs getNow "function" then
                fun () -> unbox<System.DateTime> (getNow $ null)
            else fun () -> Clock.nowUtc ()
        | _ -> fun () -> Clock.nowUtc ()

    let testPorts =
        createFromStateQueueSink
            (fun () -> state)
            (fun s -> state <- s)
            (fun work -> commandQueue.Enqueue work)
            backgroundSink

    let getChatHistory =
        match deps with
        | Some d when not (Dyn.isNullish d) ->
            let getHistory = Dyn.get d "getChatHistory"
            if not (Dyn.isNullish getHistory) then
                Some (fun (sid: string) -> unbox<JS.Promise<obj array>> (getHistory $ sid))
            else None
        | _ -> None

    let launchBg root _kind title buildPrompt =
        match latestConfig with
        | Some cfg ->
            let options = Some (box {| aiSettingsAgentId = "bookkeeper" |})
            queueMuxBackgroundLaunch
                deps
                cfg
                "bookkeeper"
                title
                options
                (trackBackgroundJob backgroundSink)
                (recordLaunchResult backgroundSink title)
                buildPrompt
                delegateToSubAgent
        | None -> ()

    member internal _.GetLatestConfig() = latestConfig
    member internal _.SetLatestConfig(cfg) = latestConfig <- cfg
    member internal _.GetChatHistory() = getChatHistory
    member internal _.GetNowUtc() = nowUtc()
    member internal _.EnqueueWrite(work) = writeQueue.Enqueue(work)
    member internal _.GetDeps() = deps
    member internal _.GetBackgroundSink() = backgroundSink
    member internal _.RecordLaunch(cmd) = state <- reducer state (RecordLaunchCmd cmd)

    member _.TryResolveJobContext(sessionID: string) : JS.Promise<KnowledgeGraphJobContext option> =
        tryResolveJobContext getChatHistory sessionID

    member _.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<KnowledgeGraphProjection> =
        if sessionID = "" then Promise.lift Map.empty
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    match Map.tryFind sessionID state.sessionSnapshots with
                    | Some projection -> return projection
                    | None ->
                        let! projection = readProjectionForRoot directory
                        state <- reducer state (CacheSnapshotCmd (sessionID, projection))
                        return projection
                })

    member _.RegisterJob(sessionID: string, ctx: KnowledgeGraphJobContext) : unit =
        registeredJobs <- Map.add sessionID ctx registeredJobs

    member _.TakeJob(sessionID: string) : KnowledgeGraphJobContext option =
        Map.tryFind sessionID registeredJobs

    member _.DeleteJob(sessionID: string) : unit =
        registeredJobs <- Map.remove sessionID registeredJobs

    member _.HasJobForTest(sessionID: string) : bool =
        Map.containsKey sessionID registeredJobs

    member this.StartMaintenanceIfDue(workspaceRoot: string) : JS.Promise<unit> =
        runMaintenanceIfDue commandQueue
            { WorkspaceRoot = workspaceRoot
              GetState = fun () -> state
              SetState = fun s -> state <- s
              Now = nowUtc
              TryLaunch =
                fun date files projection ->
                    let _, launch = bookkeeperMaintenanceLaunch workspaceRoot date
                    launchBg workspaceRoot (DailyRewrite date) launch.title (fun () ->
                        Promise.lift (
                            prependJobMarker { workspaceRoot = workspaceRoot; kind = DailyRewrite date }
                                (buildDailyPrompt date files projection)))
            }

    member internal _.CreateTestPorts() : KnowledgeGraphRuntimeTestPorts = testPorts

    member val startMaintenanceIfDue = System.Func<string, JS.Promise<unit>>(fun workspaceRoot -> this.StartMaintenanceIfDue(workspaceRoot)) with get, set
