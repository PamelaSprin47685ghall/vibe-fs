module VibeFs.Opencode.KnowledgeGraphRuntime

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphPrompts
open VibeFs.Kernel.KnowledgeGraphMaintenance
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Opencode.KnowledgeGraphRuntimeIO
open VibeFs.Mux.AiSettings
open VibeFs.Shell.Dyn

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
    let backgroundJobs = ResizeArray<JS.Promise<unit>>()
    let mutable writeQueues = Map.empty<string, SerialQueue>
    let mutable registeredJobs = Map.empty<string, KnowledgeGraphJobContext>
    let workspaceRoot = initialWorkspaceRoot

    let today () = (nowUtc ()).ToString("yyyy-MM-dd")

    let applyCmd (cmd: KnowledgeGraphCommand) : unit = state <- reducer state cmd

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

    let recordBackgroundResult title result =
        applyCmd (UpdateLatestLaunchResultCmd (title, result))

    let startBackgroundJob (job: JS.Promise<unit>) : unit =
        backgroundJobs.Add(job)
        job |> Promise.start

    let launchBg root parentID kind title buildPrompt aiSettings =
        queueBackgroundLaunch client startBackgroundJob (recordBackgroundResult title) root parentID kind title buildPrompt aiSettings registry

    member _.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<KnowledgeGraphProjection> =
        if sessionID = "" then Promise.lift Map.empty
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    match Map.tryFind sessionID state.sessionSnapshots with
                    | Some projection -> return projection
                    | None ->
                        let! projection = readProjection (effectiveWorkspaceRoot directory)
                        applyCmd (CacheSnapshotCmd (sessionID, projection))
                        return projection
                })

    member _.RegisterJob(_sessionID: string, _ctx: KnowledgeGraphJobContext) : unit =
        registeredJobs <- Map.add _sessionID _ctx registeredJobs

    member this.RegisterJobForTesting(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        let payloadObj: obj = payload

        let readRequiredField (fieldName: string) : string =
            let value = str payloadObj fieldName
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
        if not (knowledgeGraphDirExists root) then Promise.lift ()
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    let! files = readKnowledgeGraphFiles root
                    let projection = projectLatestWins files
                    let dailyDue = dueMaintenance files (nowUtc ())

                    let launchIfDue (due: string list) kind title resultPrefix promptInfix buildPrompt =
                        due
                        |> List.iter (fun value ->
                            let key = root + "|" + resultPrefix + "|" + value
                            let launch = { agent = "bookkeeper"; title = title; prompt = $"{resultPrefix} maintenance due {promptInfix} {value}"; result = $"{resultPrefix}:{value}" }
                            let first, nextState = recordLaunchOnce state key launch
                            state <- nextState
                            if first then
                                launchBg root parentSessionID (kind value) title (fun () -> Promise.lift (buildPrompt value files projection)) emptySettings)

                    launchIfDue dailyDue DailyRewrite "Daily knowledge graph rewrite" "daily" "for" buildDailyPrompt
                })

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
                    let! projection = readProjection root
                    return buildAppendPrompt title prompt result projection
                }) settings

    /// Test-only projection: drains recorded bookkeeper launches so integration
    /// tests can assert what the runtime would have fired. Kept on the production
    /// type because IntegrationToolTests reaches it through the duck-typed
    /// __knowledgeGraphRuntime surface; it performs no IO and mutates only the test buffer.
    member _.TakeBookkeeperLaunchesForTesting() : obj array =
        let launches, nextState = drainLaunches state
        state <- nextState
        launches |> List.map box |> List.toArray

    member _.WaitForBackgroundJobsForTesting() : JS.Promise<unit> =
        promise {
            do! commandQueue.Enqueue(fun () -> Promise.lift ())
            let jobs = backgroundJobs |> Seq.toArray
            backgroundJobs.Clear()
            if jobs.Length > 0 then
                let! _ = Promise.all jobs
                return ()
        }
