module VibeFs.Opencode.WikiRuntime

open Fable.Core
open Fable.Core.JsInterop
open System.Collections.Generic
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.WikiPrompts
open VibeFs.Kernel.WikiMaintenance
open VibeFs.Kernel.WikiRuntimeState
open VibeFs.Shell.WikiFiles
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Opencode.WikiRuntimeIO

/// Wiki host IO shell (P53/P72): holds the single mutable state cell and
/// serializes every state+IO change through `commandQueue`. All pure state
/// transitions live in `Kernel.WikiRuntimeState`; the stateless IO orchestration
/// (prompt building, file IO, background session launch) lives in
/// `WikiRuntimeIO`. Synchronous methods (RegisterJob/DeleteJob/MarkRwTool/...)
/// run to completion in a single tick and cannot dangle a JS.Promise mid
/// state-change; the only async-with-await method that holds a job context
/// across an await is `Submit`, which caches `ctx` before awaiting and uses an
/// idempotent `RemoveJobCmd` on exit so a synchronous `DeleteJob` raised by a
/// stream-abort/session.delete event during the await cannot corrupt it.
type WikiRuntime(client: obj, initialWorkspaceRoot: string, nowUtc: unit -> System.DateTime, registry: ChildAgentRegistry) =
    let mutable state = initialWikiState
    let commandQueue = SerialQueue()
    let writeQueues = Dictionary<string, SerialQueue>()
    let workspaceRoot = initialWorkspaceRoot

    let today () = (nowUtc ()).ToString("yyyy-MM-dd")

    let applyCmd (cmd: WikiCommand) : unit = state <- reducer state cmd

    let getWorkspaceQueue (root: string) =
        match writeQueues.TryGetValue root with
        | true, queue -> queue
        | false, _ ->
            let queue = SerialQueue()
            writeQueues.[root] <- queue
            queue

    let runWorkspace (root: string) (work: unit -> JS.Promise<string>) : JS.Promise<string> =
        getWorkspaceQueue root |> fun queue -> queue.Enqueue(work)

    let effectiveWorkspaceRoot (value: string) : string =
        if System.String.IsNullOrWhiteSpace value then workspaceRoot else value

    let launchBg root kind title buildPrompt maintenanceKey =
        queueBackgroundLaunch client commandQueue applyCmd root kind title buildPrompt maintenanceKey registry

    member _.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<WikiProjection> =
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

    member _.RegisterJob(sessionID: string, ctx: WikiJobContext) : unit =
        applyCmd (RegisterJobCmd (sessionID, ctx))

    member this.RegisterJobForTesting(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        let payloadObj: obj = payload

        let readRequiredField (fieldName: string) : string =
            let value = str payloadObj fieldName
            if value.Trim() = "" then failwith $"Wiki job payload missing required field '{fieldName}'"
            else value.Trim()

        let kind =
            match kindTag.Trim().ToLowerInvariant() with
            | "append" -> AppendAfterWork
            | "daily" -> DailyRewrite(readRequiredField "date")
            | "weekly" -> WeeklyRewrite(readRequiredField "through")
            | other -> failwith $"Unknown wiki job kind: {other}"
        this.RegisterJob(sessionID, { workspaceRoot = workspaceRoot; kind = kind })

    member _.TakeJob(sessionID: string) : WikiJobContext option =
        tryJob state sessionID

    member _.DeleteJob(sessionID: string) : unit =
        registry.UnregisterChildAgent(sessionID)
        applyCmd (RemoveJobCmd sessionID)

    member _.Submit(sessionID: string, drafts: WikiDraft list) : JS.Promise<string> =
        commandQueue.Enqueue(fun () ->
            promise {
                match tryJob state sessionID with
                | None -> return "No active wiki job for this session."
                | Some ctx ->
                    try
                        let! result = runWorkspace ctx.workspaceRoot (fun () -> submitForKind (today ()) ctx.workspaceRoot ctx.kind drafts)
                        return result
                    finally
                        registry.UnregisterChildAgent(sessionID)
                        applyCmd (RemoveJobCmd sessionID)
            })

    member this.BuildPreludeForSession(sessionID: string, directory: string) : JS.Promise<string option> =
        promise {
            let! projection = this.EnsureSessionSnapshot(sessionID, directory)
            return buildPreludeSection projection
        }

    member this.FetchFromSessionSnapshot(sessionID: string, directory: string, id: string) : JS.Promise<string> =
        promise {
            if sessionID = "" then
                return "Wiki snapshot unavailable for this session."
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                match fetchAnswer projection id with
                | Ok answer -> return answer
                | Error message -> return message
        }

    member _.MarkRwTool(sessionID: string, tool: string, summary: string) : unit =
        let trimmed = summary.Trim()
        if sessionID <> "" && trimmed <> "" then
            applyCmd (MarkRwToolCmd (sessionID, $"{tool}: {trimmed}"))

    member this.FlushTurnIfNeeded(sessionID: string, assistantText: string) : unit =
        let flushed, nextState = consumeDirtyTurn state sessionID
        state <- nextState
        match flushed with
        | Some rwSummary -> this.StartBookkeeperAppend(rwSummary, assistantText, "Direct write tools", rwSummary)
        | None -> ()

    member _.StartMaintenanceIfDue(workspaceRoot: string) : JS.Promise<unit> =
        commandQueue.Enqueue(fun () ->
            promise {
                let root = effectiveWorkspaceRoot workspaceRoot
                let! files = readWikiFiles root
                let projection = projectLatestWins files
                let dailyDue, weeklyDue = dueMaintenance files (nowUtc ())

                dailyDue
                |> Option.iter (fun date ->
                    let key = root + "|daily|" + date
                    let launch = { agent = "bookkeeper"; title = "Daily wiki rewrite"; prompt = $"daily maintenance due for {date}"; result = $"daily:{date}"; rwSummary = "" }
                    let first, nextState = recordLaunchOnce state key launch
                    state <- nextState
                    if first then
                        launchBg root (DailyRewrite date) "Daily wiki rewrite" (fun () -> Promise.lift (buildDailyPrompt date files projection)) (Some key))

                weeklyDue
                |> Option.iter (fun cutoff ->
                    let key = root + "|weekly|" + cutoff
                    let launch = { agent = "bookkeeper"; title = "Weekly wiki snapshot rewrite"; prompt = $"weekly maintenance due through {cutoff}"; result = $"weekly:{cutoff}"; rwSummary = "" }
                    let first, nextState = recordLaunchOnce state key launch
                    state <- nextState
                    if first then
                        launchBg root (WeeklyRewrite cutoff) "Weekly wiki snapshot rewrite" (fun () -> Promise.lift (buildWeeklyPrompt cutoff files projection)) (Some key))
            })

    member _.RecordBookkeeperLaunch(agent: string, title: string, prompt: string, result: string, rwSummary: string) : unit =
        applyCmd (RecordLaunchCmd { agent = agent; title = title; prompt = prompt; result = result; rwSummary = rwSummary })

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, rwSummary: string) : unit =
        this.RecordBookkeeperLaunch("bookkeeper", title, prompt, result, rwSummary)
        let root = effectiveWorkspaceRoot workspaceRoot
        launchBg root AppendAfterWork title (fun () ->
            promise {
                let! projection = readProjection root
                return buildAppendPrompt title prompt result rwSummary projection
            }) None

    /// Test-only projection: drains recorded bookkeeper launches so integration
    /// tests can assert what the runtime would have fired. Kept on the production
    /// type because IntegrationToolTests reaches it through the duck-typed
    /// __wikiRuntime surface; it performs no IO and mutates only the test buffer.
    member _.TakeBookkeeperLaunchesForTesting() : obj array =
        let launches, nextState = drainLaunches state
        state <- nextState
        launches |> List.map box |> List.toArray

    member _.WaitForBackgroundJobsForTesting() : JS.Promise<unit> =
        commandQueue.Enqueue(fun () -> Promise.lift ())
