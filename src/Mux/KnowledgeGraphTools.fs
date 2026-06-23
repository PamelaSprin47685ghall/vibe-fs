module VibeFs.Mux.KnowledgeGraphTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Kernel.KnowledgeGraphMaintenance
open VibeFs.Kernel.KnowledgeGraphPrompts
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphPortLock
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.Dyn
open VibeFs.Mux.KnowledgeGraphRuntimeIO

type MuxKnowledgeGraphRuntime(?deps: obj) as this =
    let mutable registeredJobs = Map.empty<string, KnowledgeGraphJobContext>
    let writeQueue = SerialQueue()
    let commandQueue = SerialQueue()
    let backgroundJobs = ResizeArray<JS.Promise<unit>>()
    let mutable state = initialKnowledgeGraphState
    let mutable latestConfig : obj option = None

    let getChatHistory =
        match deps with
        | Some d when not (Dyn.isNullish d) ->
            let getHistory = Dyn.get d "getChatHistory"
            if not (Dyn.isNullish getHistory) then
                Some (fun (sid: string) -> unbox<JS.Promise<obj array>> (getHistory $ sid))
            else None
        | _ -> None

    let recordBackgroundResult title result =
        state <- reducer state (UpdateLatestLaunchResultCmd (title, result))

    let startBackgroundJob (job: JS.Promise<unit>) : unit =
        backgroundJobs.Add(job)
        job |> Promise.start

    let launchBg root kind title promptText =
        match latestConfig with
        | Some cfg ->
            let options = Some (box {| aiSettingsAgentId = "bookkeeper" |})
            promise {
                try
                    let! _ = delegateToSubAgent deps cfg "bookkeeper" promptText title options
                    recordBackgroundResult title "success"
                with ex ->
                    recordBackgroundResult title (string ex)
            }
            |> startBackgroundJob
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
                        let! projection = readProjection directory
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
        if not (knowledgeGraphDirExists workspaceRoot) then Promise.lift ()
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    let! files = readKnowledgeGraphFiles workspaceRoot
                    let projection = projectLatestWins files
                    let dailyDue = dueMaintenance files System.DateTime.UtcNow

                    let launchIfDue (due: string list) kind title resultPrefix promptInfix buildPrompt =
                        due
                        |> List.iter (fun value ->
                            let key = workspaceRoot + "|" + resultPrefix + "|" + value
                            let launch = { agent = "bookkeeper"; title = title; prompt = $"{resultPrefix} maintenance due {promptInfix} {value}"; result = $"{resultPrefix}:{value}" }
                            let first, nextState = recordLaunchOnce state key launch
                            state <- nextState
                            if first then
                                let promptText = prependJobMarker { workspaceRoot = workspaceRoot; kind = kind value } (buildPrompt value files projection)
                                launchBg workspaceRoot (kind value) title promptText)

                    launchIfDue dailyDue DailyRewrite "Daily knowledge graph rewrite" "daily" "for" buildDailyPrompt
                })

    member this.Submit(sessionID: string, directory: string, drafts: KnowledgeGraphDraft list, ?config: obj) : JS.Promise<string> =
        if not (knowledgeGraphDirExists directory) then Promise.lift "Knowledge graph directory not found."
        else
            promise {
                match config with
                | Some cfg -> latestConfig <- Some cfg
                | None -> ()

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
                let dir = Dyn.str cfg "directory"
                if dir <> "" then dir else defaultArg (strField cfg "cwd") ""
            | _ -> ""
        if root = "" || not (knowledgeGraphDirExists root) then ()
        else
            state <- reducer state (RecordLaunchCmd { agent = "bookkeeper"; title = title; prompt = prompt; result = result })
            match config with
            | Some cfg when not (Dyn.isNullish cfg) -> latestConfig <- Some cfg
            | _ -> ()
            this.StartMaintenanceIfDue(root) |> ignore
            if not (Dyn.isNullish deps) then
                match latestConfig with
                | Some cfg when not (Dyn.isNullish cfg) ->
                    promise {
                        try
                            let! projection = readProjection root
                            let promptText = prependJobMarker { workspaceRoot = root; kind = AppendAfterWork } (buildAppendPrompt title prompt result projection)
                            let options = Some (box {| aiSettingsAgentId = "bookkeeper" |})
                            let! _ = delegateToSubAgent deps cfg "bookkeeper" promptText title options
                            recordBackgroundResult title "success"
                        with ex ->
                            recordBackgroundResult title (string ex)
                    }
                    |> startBackgroundJob
                | _ -> ()

    member _.TakeBookkeeperLaunchesForTesting() : obj array =
        let launches, nextState = drainLaunches state
        state <- nextState
        launches
        |> List.map (fun l ->
            box (createObj [
                "agent", box l.agent
                "title", box l.title
                "prompt", box l.prompt
                "result", box l.result
            ]))
        |> List.toArray

    member _.WaitForBackgroundJobsForTesting() : JS.Promise<unit> =
        promise {
            do! commandQueue.Enqueue(fun () -> Promise.lift ())
            let jobs = backgroundJobs |> Seq.toArray
            backgroundJobs.Clear()
            if jobs.Length > 0 then
                let! _ = Promise.all jobs
                return ()
        }

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
                let! projection = readProjection directory
                match fetchAnswer projection entity with
                | Ok answer -> return answer
                | Error message -> return message
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                match fetchAnswer projection entity with
                | Ok answer -> return answer
                | Error message -> return message
        }

    member this.RegisterJobForTesting(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        if System.String.IsNullOrWhiteSpace workspaceRoot then
            failwith "Knowledge graph job workspaceRoot must be a non-empty directory path."

        let payloadObj = payload

        let readRequiredField (fieldName: string) : string =
            let value = Dyn.str payloadObj fieldName
            if value.Trim() = "" then failwith $"Knowledge graph job payload missing required field '{fieldName}'"
            else value.Trim()

        let kind =
            let normalizedTag = kindTag.Trim().ToLowerInvariant()
            let builders =
                Map [
                    "append", fun () -> AppendAfterWork
                    "daily", fun () -> DailyRewrite(readRequiredField "date")
                ]
            match Map.tryFind normalizedTag builders with
            | Some build -> build ()
            | None -> failwith $"Unknown knowledge graph job kind: {normalizedTag}"

        this.RegisterJob(sessionID, { workspaceRoot = workspaceRoot; kind = kind })

    member val startMaintenanceIfDue = System.Func<string, JS.Promise<unit>>(fun workspaceRoot -> this.StartMaintenanceIfDue(workspaceRoot)) with get, set
    member val takeBookkeeperLaunchesForTesting = System.Func<obj array>(fun () -> this.TakeBookkeeperLaunchesForTesting()) with get, set
    member val waitForBackgroundJobsForTesting = System.Func<JS.Promise<unit>>(fun () -> this.WaitForBackgroundJobsForTesting()) with get, set
