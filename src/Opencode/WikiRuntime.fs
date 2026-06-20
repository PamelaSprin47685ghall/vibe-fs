module VibeFs.Opencode.WikiRuntime

open Fable.Core
open Fable.Core.JsInterop
open System.Collections.Generic
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.WikiPrompts
open VibeFs.Kernel.WikiMaintenance
open VibeFs.Shell.WikiFiles
open VibeFs.Shell.WikiPortLock
open VibeFs.Shell.PromiseQueue

type BookkeeperLaunch =
    { agent: string
      title: string
      prompt: string
      result: string
      rwSummary: string }

type WikiJobKind =
    | AppendAfterWork
    | DailyRewrite of date: string
    | WeeklyRewrite of throughDate: string

type WikiJobContext =
    { workspaceRoot: string
      kind: WikiJobKind }

/// Per-workspace serial queue: serializes wiki writes (append/daily/weekly) so
/// concurrent jobs on the same workspace never interleave. Replaces the legacy
/// F# Agent actor with a Promise-chain SerialQueue.
type WikiActor() =
    let queues = Dictionary<string, SerialQueue>()

    let getWorkspaceQueue (workspaceRoot: string) =
        match queues.TryGetValue workspaceRoot with
        | true, queue -> queue
        | false, _ ->
            let queue = SerialQueue()
            queues.[workspaceRoot] <- queue
            queue

    member _.Post(workspaceRoot: string, work: unit -> JS.Promise<unit>) : unit =
        getWorkspaceQueue workspaceRoot |> fun queue -> queue.Enqueue(work) |> Promise.start

    member _.Run(workspaceRoot: string, work: unit -> JS.Promise<string>) : JS.Promise<string> =
        getWorkspaceQueue workspaceRoot |> fun queue -> queue.Enqueue(work)

let private invoke1 (target: obj) (methodName: string) (arg: obj) : JS.Promise<obj> =
    unbox (target?(methodName)(arg))

type WikiRuntime(client: obj, initialWorkspaceRoot: string, nowUtc: unit -> System.DateTime) =
    let sessionSnapshots = Dictionary<string, WikiProjection>()
    let jobContexts = Dictionary<string, WikiJobContext>()
    let bookkeeperLaunches = ResizeArray<BookkeeperLaunch>()
    let directWriteTurns = Dictionary<string, ResizeArray<string> * bool>()
    let scheduledMaintenance = HashSet<string>()
    let actor = WikiActor()
    let client = client
    let workspaceRoot = initialWorkspaceRoot

    let today () = (nowUtc ()).ToString("yyyy-MM-dd")

    let sessionApi () =
        if isNullish client then None
        else
            let session = get client "session"
            if isNullish session then None
            elif not (typeIs (get session "create") "function") then None
            elif not (typeIs (get session "prompt") "function") then None
            else Some session

    let effectiveWorkspaceRoot (value: string) : string =
        if System.String.IsNullOrWhiteSpace value then workspaceRoot else value

    let launchBackgroundSession (root: string) (kind: WikiJobKind) (title: string) (promptText: string) : JS.Promise<unit> =
        promise {
            match sessionApi () with
            | None -> ()
            | Some session ->
                let mutable sessionId = ""
                try
                    let createBody =
                        box {| query = box {| directory = root |}
                               body = box {| parentID = box null; title = title |} |}
                    let! created = invoke1 session "create" createBody
                    let childId = str (get created "data") "id"
                    if childId <> "" then
                        sessionId <- childId
                        jobContexts.[childId] <- { workspaceRoot = root; kind = kind }
                        let promptBody =
                            box {| path = box {| id = childId |}
                                   body = box {| agent = "bookkeeper"
                                                 parts = [| box {| ``type`` = "text"; text = promptText |} |]
                                                 tools = box (createObj [ "submit_wiki", box true ]) |} |}
                        do! invoke1 session "prompt" promptBody |> Promise.map ignore
                with _ ->
                    if sessionId <> "" then
                        jobContexts.Remove sessionId |> ignore
        }

    let queueBackgroundLaunch (root: string) (kind: WikiJobKind) (title: string) (buildPrompt: unit -> JS.Promise<string>) : unit =
        match sessionApi () with
        | None -> ()
        | Some _ -> actor.Post(root, fun () ->
            promise {
                try
                    let! promptText = buildPrompt ()
                    launchBackgroundSession root kind title promptText |> Promise.start
                with _ -> ()
            })

    let normalizeDraftIds (projection: WikiProjection) (drafts: WikiDraft list) : WikiDraft list =
        drafts
        |> List.map (fun draft ->
            match draft.id |> Option.bind tryParseId with
            | Some wikiId when Map.containsKey wikiId projection -> draft
            | _ -> { draft with id = None })

    let buildEntries (root: string) (drafts: WikiDraft list) : JS.Promise<WikiEntry list> =
        promise {
            let! projection = readProjection root
            let normalizedDrafts = normalizeDraftIds projection drafts
            match applyDrafts (fun knownIds ->
                      let random = System.Random()
                      let rec loop attempts =
                          if attempts > 65536 then failwith "wiki id space exhausted"
                          else
                              let candidate = sprintf "%04x" (random.Next(0, 65536))
                              if Set.contains candidate knownIds then loop (attempts + 1) else candidate
                      loop 0) projection normalizedDrafts with
            | Ok entries -> return entries
            | Error error -> return raise (exn error)
        }

    let recordLaunchOnce (root: string) (kind: string) (value: string) (title: string) (prompt: string) (result: string) : bool =
        let key = root + "|" + kind + "|" + value
        if scheduledMaintenance.Add key then
            bookkeeperLaunches.Add { agent = "bookkeeper"; title = title; prompt = prompt; result = result; rwSummary = "" }
            true
        else
            false

    let turnFor (sessionID: string) : ResizeArray<string> * bool =
        match directWriteTurns.TryGetValue sessionID with
        | true, turn -> turn
        | false, _ ->
            let turn = ResizeArray<string>(), false
            directWriteTurns.[sessionID] <- turn
            turn

    let submitForKind (root: string) (kind: WikiJobKind) (drafts: WikiDraft list) : JS.Promise<string> =
        withWikiPortLock root (fun () ->
            promise {
                let! entries = buildEntries root drafts
                match kind with
                | AppendAfterWork ->
                    do! appendEntries root (today ()) entries
                    return $"Appended {entries.Length} wiki entries."
                | DailyRewrite date ->
                    do! rewriteDay root date entries
                    return $"Rewrote wiki day {date}."
                | WeeklyRewrite throughDate ->
                    do! rewriteSnapshot root throughDate entries
                    do! deleteDayFilesThrough root throughDate
                    return $"Rewrote wiki snapshot through {throughDate}."
            })

    member _.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<WikiProjection> =
        promise {
            if sessionID = "" then
                return Map.empty
            else
                match sessionSnapshots.TryGetValue sessionID with
                | true, projection -> return projection
                | false, _ ->
                    let! projection = readProjection (effectiveWorkspaceRoot directory)
                    sessionSnapshots.[sessionID] <- projection
                    return projection
        }

    member _.RegisterJob(sessionID: string, ctx: WikiJobContext) : unit =
        jobContexts.[sessionID] <- ctx

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
        match jobContexts.TryGetValue sessionID with
        | true, ctx -> Some ctx
        | false, _ -> None

    member _.DeleteJob(sessionID: string) : unit =
        jobContexts.Remove sessionID |> ignore

    member this.Submit(sessionID: string, drafts: WikiDraft list) : JS.Promise<string> =
        match this.TakeJob sessionID with
        | None -> Promise.lift "No active wiki job for this session."
        | Some ctx ->
            promise {
                try
                    return! actor.Run(ctx.workspaceRoot, fun () -> submitForKind ctx.workspaceRoot ctx.kind drafts)
                finally
                    jobContexts.Remove sessionID |> ignore
            }

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
            let summaries, _ = turnFor sessionID
            summaries.Add($"{tool}: {trimmed}")
            directWriteTurns.[sessionID] <- summaries, true

    member this.FlushTurnIfNeeded(sessionID: string, assistantText: string) : unit =
        match directWriteTurns.TryGetValue sessionID with
        | true, (summaries, true) ->
            directWriteTurns.Remove sessionID |> ignore
            let rwSummary = String.concat "\n" (summaries |> Seq.toList)
            this.StartBookkeeperAppend(rwSummary, assistantText, "Direct write tools", rwSummary)
        | _ -> ()

    member _.StartMaintenanceIfDue(workspaceRoot: string) : JS.Promise<unit> =
        promise {
            let root = effectiveWorkspaceRoot workspaceRoot
            let! files = readWikiFiles root
            let projection = projectLatestWins files
            let dailyDue, weeklyDue = dueMaintenance files (nowUtc ())

            dailyDue
            |> Option.iter (fun date ->
                if recordLaunchOnce root "daily" date "Daily wiki rewrite" ($"daily maintenance due for {date}") ($"daily:{date}") then
                    queueBackgroundLaunch root (DailyRewrite date) "Daily wiki rewrite" (fun () -> Promise.lift (buildDailyPrompt date files projection))
                else ())

            weeklyDue
            |> Option.iter (fun cutoff ->
                if recordLaunchOnce root "weekly" cutoff "Weekly wiki snapshot rewrite" ($"weekly maintenance due through {cutoff}") ($"weekly:{cutoff}") then
                    queueBackgroundLaunch root (WeeklyRewrite cutoff) "Weekly wiki snapshot rewrite" (fun () -> Promise.lift (buildWeeklyPrompt cutoff files projection))
                else ())
        }

    member _.RecordBookkeeperLaunch(agent: string, title: string, prompt: string, result: string, rwSummary: string) : unit =
        bookkeeperLaunches.Add { agent = agent; title = title; prompt = prompt; result = result; rwSummary = rwSummary }

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, rwSummary: string) : unit =
        this.RecordBookkeeperLaunch("bookkeeper", title, prompt, result, rwSummary)
        let root = effectiveWorkspaceRoot workspaceRoot
        queueBackgroundLaunch root AppendAfterWork title (fun () ->
            promise {
                let! projection = readProjection root
                return buildAppendPrompt title prompt result rwSummary projection
            })

    member _.TakeBookkeeperLaunchesForTesting() : obj array =
        let launches = bookkeeperLaunches |> Seq.map box |> Seq.toArray
        bookkeeperLaunches.Clear()
        launches

    member _.WaitForBackgroundJobsForTesting() : JS.Promise<unit> =
        promise {
            let! _ = actor.Run(workspaceRoot, fun () -> Promise.lift "")
            return ()
        }
