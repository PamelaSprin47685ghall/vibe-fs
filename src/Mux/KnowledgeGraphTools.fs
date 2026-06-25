module VibeFs.Mux.KnowledgeGraphTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.Messaging
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Kernel.KnowledgeGraphMaintenance
open VibeFs.Kernel.KnowledgeGraphPrompts
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.MessagingCodec
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphStorage
open VibeFs.Shell.KnowledgeGraphWorkflow
open VibeFs.Shell.KnowledgeGraphMaintenanceRun
open VibeFs.Shell.KnowledgeGraphBookkeeperLaunch
open VibeFs.Shell.KnowledgeGraphRuntimeTestPorts
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.ToolContextCodec
open VibeFs.Shell.Dyn
open VibeFs.Mux.KnowledgeGraphRuntimeIO

type MuxKnowledgeGraphRuntime(?deps: obj) as this =
    let mutable registeredJobs = Map.empty<string, KnowledgeGraphJobContext>
    let writeQueue = SerialQueue()
    let commandQueue = SerialQueue()
    let mutable state = initialKnowledgeGraphState
    let backgroundSink =
        createSink (fun title result ->
            state <- reducer state (UpdateLatestLaunchResultCmd (title, result)))
    let mutable latestConfig : obj option = None

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

    member this.StartMaintenanceIfDue(workspaceRoot: string) : JS.Promise<unit> =
        runMaintenanceIfDue commandQueue
            { WorkspaceRoot = workspaceRoot
              GetState = fun () -> state
              SetState = fun s -> state <- s
              Now = fun () -> System.DateTime.UtcNow
              TryLaunch =
                fun date files projection ->
                    let _, launch = bookkeeperMaintenanceLaunch workspaceRoot date
                    launchBg workspaceRoot (DailyRewrite date) launch.title (fun () ->
                        Promise.lift (
                            prependJobMarker { workspaceRoot = workspaceRoot; kind = DailyRewrite date }
                                (buildDailyPrompt date files projection)))
            }

    member this.Submit(sessionID: string, directory: string, drafts: KnowledgeGraphDraft list, ?config: obj) : JS.Promise<string> =
        if not (knowledgeGraphDirExists directory) then Promise.lift "Knowledge graph directory not found."
        else
            promise {
                match config with
                | Some cfg -> latestConfig <- Some cfg
                | None -> ()

                let! earlyReject =
                    match getChatHistory with
                    | Some getHistory when sessionID <> "" ->
                        promise {
                            try
                                let! history = getHistory sessionID
                                let messages = MessagingCodec.decodeMessages sessionID history
                                return
                                    if historyHasCompletedReturnBookkeeper messages then
                                        Some rejectSecondReturnBookkeeperMessage
                                    else
                                        None
                            with _ ->
                                return None
                        }
                    | _ -> Promise.lift None

                match earlyReject with
                | Some msg -> return msg
                | None ->
                    let! reconstructed = this.TryResolveJobContext(sessionID)
                    let jobCtxOpt =
                        reconstructed |> Option.orElseWith (fun () ->
                            Map.tryFind sessionID registeredJobs)

                    match jobCtxOpt with
                    | None -> return "No active knowledge graph job for this session."
                    | Some ctx ->
                        let root = ctx.workspaceRoot
                        let todayStr = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
                        let! result = writeQueue.Enqueue(fun () ->
                            promise {
                                let! entriesResult = buildEntries root drafts
                                match entriesResult with
                                | Error e -> return e
                                | Ok entries ->
                                    let! result = submitForKind root todayStr entries ctx.kind
                                    registeredJobs <- Map.remove sessionID registeredJobs
                                    return result
                            })

                        match ctx.kind with
                        | DailyRewrite _ -> this.StartMaintenanceIfDue(root) |> ignore
                        | _ -> ()

                        return result
            }

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, ?config: obj) : unit =
        let root =
            match config with
            | Some cfg when not (Dyn.isNullish cfg) ->
                match fromMuxConfig cfg with
                | Ok runtime -> runtime.Execution.Directory
                | Error _ -> muxConfigDirectoryFallback cfg
            | _ -> ""
        if root = "" || not (knowledgeGraphDirExists root) then ()
        else
            state <- reducer state (RecordLaunchCmd { agent = "bookkeeper"; title = title; prompt = prompt; result = result })
            match config with
            | Some cfg when not (Dyn.isNullish cfg) -> latestConfig <- Some cfg
            | _ -> ()
            this.StartMaintenanceIfDue(root) |> ignore
            match latestConfig with
            | Some cfg when not (Dyn.isNullish deps) && not (Dyn.isNullish cfg) ->
                let options = Some (box {| aiSettingsAgentId = "bookkeeper" |})
                queueMuxBackgroundLaunch
                    deps
                    cfg
                    "bookkeeper"
                    title
                    options
                    (trackBackgroundJob backgroundSink)
                    (recordLaunchResult backgroundSink title)
                    (fun () ->
                        promise {
                            let! projection = readProjectionForRoot root
                            return
                                prependJobMarker { workspaceRoot = root; kind = AppendAfterWork }
                                    (buildAppendPrompt title prompt result projection)
                        })
                    delegateToSubAgent
            | _ -> ()

    member internal _.CreateTestPorts() : KnowledgeGraphRuntimeTestPorts = testPorts

    member this.BuildPreludeForSession(sessionID: string, directory: string) : JS.Promise<string option> =
        promise {
            if not (knowledgeGraphDirExists directory) then return None
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                return buildPreludeSection projection
        }

    member this.FetchFromSessionSnapshot(sessionID: string, directory: string, entity: string) : JS.Promise<string> =
        promise {
            if System.String.IsNullOrWhiteSpace directory then
                return "No knowledge graph directory provided."
            elif not (knowledgeGraphDirExists directory) then
                return "Knowledge graph directory not found."
            elif sessionID = "" then
                let! projection = readProjectionForRoot directory
                match fetchAnswer projection entity with
                | Ok answer -> return answer
                | Error message -> return message
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                match fetchAnswer projection entity with
                | Ok answer -> return answer
                | Error message -> return message
        }

    member val startMaintenanceIfDue = System.Func<string, JS.Promise<unit>>(fun workspaceRoot -> this.StartMaintenanceIfDue(workspaceRoot)) with get, set
