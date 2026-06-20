module VibeFs.Opencode.WikiRuntime

open Fable.Core
open Fable.Core.JsInterop
open System.Collections.Generic
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Shell.WikiFiles
open VibeFs.Shell.WikiPortLock

type BookkeeperLaunch =
    { agent: string
      title: string
      prompt: string
      result: string }

type WikiJobKind =
    | AppendAfterWork
    | DailyRewrite of date: string
    | WeeklyRewrite of throughDate: string

type WikiJobContext =
    { workspaceRoot: string
      kind: WikiJobKind }

type WikiWork =
    { workspaceRoot: string
      work: unit -> Async<unit> }

type private WikiActorMessage =
    | FireAndForget of WikiWork
    | AwaitResult of workspaceRoot: string * work: (unit -> Async<string>) * reply: AsyncReplyChannel<Result<string, exn>>

type WikiActor() =
    let actors = Dictionary<string, MailboxProcessor<WikiActorMessage>>()

    let createWorkspaceAgent () =
        MailboxProcessor.Start(fun inbox ->
            let rec loop () =
                async {
                    let! message = inbox.Receive()
                    match message with
                    | FireAndForget work ->
                        let! _ = Async.Catch (work.work ())
                        return! loop ()
                    | AwaitResult(_, work, reply) ->
                        let! outcome = Async.Catch (work ())
                        match outcome with
                        | Choice1Of2 result -> reply.Reply (Ok result)
                        | Choice2Of2 error -> reply.Reply (Error error)
                        return! loop ()
                }

            loop ())

    let getWorkspaceAgent (workspaceRoot: string) =
        match actors.TryGetValue workspaceRoot with
        | true, agent -> agent
        | false, _ ->
            let agent = createWorkspaceAgent ()
            actors.[workspaceRoot] <- agent
            agent

    member _.Post(workspaceRoot: string, work: unit -> Async<unit>) : unit =
        getWorkspaceAgent workspaceRoot |> fun agent -> agent.Post (FireAndForget { workspaceRoot = workspaceRoot; work = work })

    member _.Run(workspaceRoot: string, work: unit -> Async<string>) : JS.Promise<string> =
        let agent = getWorkspaceAgent workspaceRoot
        async {
            let! result = agent.PostAndAsyncReply(fun reply -> AwaitResult(workspaceRoot, work, reply))
            match result with
            | Ok value -> return value
            | Error error -> return raise error
        }
        |> Async.StartAsPromise

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

    let lastSunday () =
        let now = nowUtc ()
        let daysSinceSunday = int now.DayOfWeek
        now.AddDays(-daysSinceSunday).ToString("yyyy-MM-dd")

    let parseDate (s: string) : System.DateTime option =
        match s.Split('-') with
        | [| yearStr; monthStr; dayStr |] ->
            match System.Int32.TryParse yearStr, System.Int32.TryParse monthStr, System.Int32.TryParse dayStr with
            | (true, year), (true, month), (true, day) when year >= 1 && year <= 9999 && month >= 1 && month <= 12 && day >= 1 && day <= 31 ->
                try Some (System.DateTime(year, month, day)) with _ -> None
            | _ -> None
        | _ -> None

    let dateRangeInclusive (start: string) (endDate: string) : string list =
        match parseDate start, parseDate endDate with
        | Some startDate, Some stopDate when startDate <= stopDate ->
            let rec loop (current: System.DateTime) (acc: string list) =
                if current > stopDate then List.rev acc
                else loop (current.AddDays(1.0)) (current.ToString("yyyy-MM-dd") :: acc)
            loop startDate []
        | _ -> []

    let addOneDay (date: string) : string =
        match parseDate date with
        | Some d -> d.AddDays(1.0).ToString("yyyy-MM-dd")
        | None -> date

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

    let projectionLines (projection: WikiProjection) : string list =
        projection |> Map.toList |> List.map (fun (_, entry) -> renderEntry entry)

    let projectionText (projection: WikiProjection) : string =
        let lines = projectionLines projection
        if lines.IsEmpty then "(empty)" else String.concat "\n" lines

    let filesText (entries: WikiEntry list) : string =
        let lines = entries |> List.map renderEntry
        if lines.IsEmpty then "(empty)" else String.concat "\n" lines

    let entriesForDay (files: WikiFile list) (date: string) : WikiEntry list =
        files
        |> List.tryPick (fun file ->
            match file.header with
            | DayHeader(day, _) when day = date -> Some file.entries
            | _ -> None)
        |> Option.defaultValue []

    let entriesThroughCutoff (files: WikiFile list) (throughDate: string) : WikiEntry list =
        files
        |> List.collect (fun file ->
            match file.header with
            | SnapshotHeader _ -> file.entries
            | DayHeader(day, _) when day <= throughDate -> file.entries
            | _ -> [])

    let buildAppendPrompt (title: string) (workInput: string) (workOutput: string) (projection: WikiProjection) : string =
        String.concat "\n\n"
            [ "You are the project wiki bookkeeper."
              "Submit exactly one `submit_wiki` call. Reuse existing ids when facts update, omit ids for new durable facts, and submit `[]` if nothing durable should be recorded."
              "=== Existing Wiki ==="
              projectionText projection
              "=== Work Title ==="
              title
              "=== Work Input ==="
              workInput
              "=== Work Output ==="
              workOutput
              "=== Output Rules ==="
              "Record only stable project knowledge. Do not record temporary errors, progress chatter, or command noise." ]

    let buildDailyPrompt (date: string) (files: WikiFile list) (projection: WikiProjection) : string =
        String.concat "\n\n"
            [ "You are rewriting one day of the project wiki."
              "Submit exactly one `submit_wiki` call. Replace the target day with durable canonical entries. It is valid to submit `[]`."
              "=== Current Wiki ==="
              projectionText projection
              "=== Target Day ==="
              filesText (entriesForDay files date) ]

    let buildWeeklyPrompt (throughDate: string) (files: WikiFile list) (projection: WikiProjection) : string =
        let snapshotEntries =
            files
            |> List.tryPick (fun file ->
                match file.header with
                | SnapshotHeader _ -> Some file.entries
                | _ -> None)
            |> Option.defaultValue []
            |> filesText
        let dayEntries = filesText (entriesThroughCutoff files throughDate)

        String.concat "\n\n"
            [ "You are rewriting the project wiki snapshot."
              "Submit exactly one `submit_wiki` call. Preserve surviving ids when possible, merge duplicates, omit ids only for genuinely new facts, and submit `[]` if nothing durable remains."
              "=== Current Wiki ==="
              projectionText projection
              "=== Previous Snapshot ==="
              snapshotEntries
              "=== Day Files Through Cutoff ==="
              dayEntries ]

    let launchBackgroundSession (root: string) (kind: WikiJobKind) (title: string) (promptText: string) : Async<unit> =
        async {
            match sessionApi () with
            | None -> ()
            | Some session ->
                let mutable sessionId = ""
                try
                    let createBody =
                        box {| query = box {| directory = root |}
                               body = box {| parentID = box null; title = title |} |}
                    let! created = invoke1 session "create" createBody |> Async.AwaitPromise
                    let childId = str (get created "data") "id"
                    if childId <> "" then
                        sessionId <- childId
                        jobContexts.[childId] <- { workspaceRoot = root; kind = kind }
                        let promptBody =
                            box {| path = box {| id = childId |}
                                   body = box {| agent = "bookkeeper"
                                                 parts = [| box {| ``type`` = "text"; text = promptText |} |]
                                                 tools = box (createObj [ "submit_wiki", box true ]) |} |}
                        do! invoke1 session "prompt" promptBody |> Async.AwaitPromise |> Async.Ignore
                with _ ->
                    if sessionId <> "" then
                        jobContexts.Remove sessionId |> ignore
        }

    let queueBackgroundLaunch (root: string) (kind: WikiJobKind) (title: string) (buildPrompt: unit -> Async<string>) : unit =
        match sessionApi () with
        | None -> ()
        | Some _ -> actor.Post(root, fun () -> async {
            try
                let! promptText = buildPrompt ()
                do! launchBackgroundSession root kind title promptText
            with _ -> ()
        })

    let normalizeDraftIds (projection: WikiProjection) (drafts: WikiDraft list) : WikiDraft list =
        drafts
        |> List.map (fun draft ->
            match draft.id |> Option.bind tryParseId with
            | Some wikiId when Map.containsKey wikiId projection -> draft
            | _ -> { draft with id = None })

    let buildEntries (root: string) (drafts: WikiDraft list) : JS.Promise<WikiEntry list> =
        async {
            let! projection = readProjection root |> Async.AwaitPromise
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
        |> Async.StartAsPromise

    let recordLaunchOnce (root: string) (kind: string) (value: string) (title: string) (prompt: string) (result: string) : bool =
        let key = root + "|" + kind + "|" + value
        if scheduledMaintenance.Add key then
            bookkeeperLaunches.Add { agent = "bookkeeper"; title = title; prompt = prompt; result = result }
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

    let submitForKind (root: string) (kind: WikiJobKind) (drafts: WikiDraft list) : Async<string> =
        withWikiPortLock root (
            async {
                let! entries = buildEntries root drafts |> Async.AwaitPromise
                match kind with
                | AppendAfterWork ->
                    do! appendEntries root (today ()) entries |> Async.AwaitPromise
                    return $"Appended {entries.Length} wiki entries."
                | DailyRewrite date ->
                    do! rewriteDay root date entries |> Async.AwaitPromise
                    return $"Rewrote wiki day {date}."
                | WeeklyRewrite throughDate ->
                    do! rewriteSnapshot root throughDate entries |> Async.AwaitPromise
                    do! deleteDayFilesThrough root throughDate |> Async.AwaitPromise
                    return $"Rewrote wiki snapshot through {throughDate}."
            })

    member _.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<WikiProjection> =
        async {
            if sessionID = "" then
                return Map.empty
            else
                match sessionSnapshots.TryGetValue sessionID with
                | true, projection -> return projection
                | false, _ ->
                    let! projection = readProjection (effectiveWorkspaceRoot directory) |> Async.AwaitPromise
                    sessionSnapshots.[sessionID] <- projection
                    return projection
        }
        |> Async.StartAsPromise

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

    member this.Submit(sessionID: string, drafts: WikiDraft list) : JS.Promise<string> =
        match this.TakeJob sessionID with
        | None -> async { return "No active wiki job for this session." } |> Async.StartAsPromise
        | Some ctx ->
            async {
                try
                    return! actor.Run(ctx.workspaceRoot, fun () -> submitForKind ctx.workspaceRoot ctx.kind drafts) |> Async.AwaitPromise
                finally
                    jobContexts.Remove sessionID |> ignore
            }
            |> Async.StartAsPromise

    member this.BuildPreludeForSession(sessionID: string, directory: string) : JS.Promise<string option> =
        async {
            let! projection = this.EnsureSessionSnapshot(sessionID, directory) |> Async.AwaitPromise
            return buildPreludeSection projection
        }
        |> Async.StartAsPromise

    member this.FetchFromSessionSnapshot(sessionID: string, directory: string, id: string) : JS.Promise<string> =
        async {
            if sessionID = "" then
                return "Wiki snapshot unavailable for this session."
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory) |> Async.AwaitPromise
                match fetchAnswer projection id with
                | Ok answer -> return answer
                | Error message -> return message
        }
        |> Async.StartAsPromise

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
            this.StartBookkeeperAppend(String.concat "\n" (summaries |> Seq.toList), assistantText, "Direct write tools")
        | _ -> ()

    member _.StartMaintenanceIfDue(workspaceRoot: string) : JS.Promise<unit> =
        async {
            let root = effectiveWorkspaceRoot workspaceRoot
            let! files = readWikiFiles root |> Async.AwaitPromise
            let projection = projectLatestWins files
            let todayValue = today ()
            let dayFiles =
                files
                |> List.choose (fun file ->
                    match file.header with
                    | DayHeader(date, rewritten) -> Some(date, rewritten)
                    | _ -> None)
                |> List.sortBy fst

            dayFiles
            |> List.tryFind (fun (date, rewritten) -> date < todayValue && not rewritten)
            |> Option.iter (fun (date, _) ->
                if recordLaunchOnce root "daily" date "Daily wiki rewrite" ($"daily maintenance due for {date}") ($"daily:{date}") then
                    queueBackgroundLaunch root (DailyRewrite date) "Daily wiki rewrite" (fun () -> async { return buildDailyPrompt date files projection })
                else
                    ())

            let snapshotThrough =
                files
                |> List.tryPick (fun file ->
                    match file.header with
                    | SnapshotHeader through -> through
                    | _ -> None)

            match snapshotThrough with
            | None -> ()
            | Some through ->
                let cutoff = lastSunday ()
                if cutoff > through then
                    let requiredDays = dateRangeInclusive (addOneDay through) cutoff
                    let dayRewritten (date: string) : bool =
                        dayFiles |> List.exists (fun (date2, rewritten) -> date2 = date && rewritten)
                    let allDaysRewritten = not requiredDays.IsEmpty && List.forall dayRewritten requiredDays
                    if allDaysRewritten then
                        if recordLaunchOnce root "weekly" cutoff "Weekly wiki snapshot rewrite" ($"weekly maintenance due through {cutoff}") ($"weekly:{cutoff}") then
                            queueBackgroundLaunch root (WeeklyRewrite cutoff) "Weekly wiki snapshot rewrite" (fun () -> async { return buildWeeklyPrompt cutoff files projection })
                    else
                        ()
        }
        |> Async.StartAsPromise

    member _.RecordBookkeeperLaunch(agent: string, title: string, prompt: string, result: string) : unit =
        bookkeeperLaunches.Add { agent = agent; title = title; prompt = prompt; result = result }

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string) : unit =
        this.RecordBookkeeperLaunch("bookkeeper", title, prompt, result)
        let root = effectiveWorkspaceRoot workspaceRoot
        queueBackgroundLaunch root AppendAfterWork title (fun () -> async {
            let! projection = readProjection root |> Async.AwaitPromise
            return buildAppendPrompt title prompt result projection
        })

    member _.TakeBookkeeperLaunchesForTesting() : obj array =
        let launches = bookkeeperLaunches |> Seq.map box |> Seq.toArray
        bookkeeperLaunches.Clear()
        launches

    member _.WaitForBackgroundJobsForTesting() : JS.Promise<unit> =
        async {
            let! _ = actor.Run(workspaceRoot, fun () -> async { return "" }) |> Async.AwaitPromise
            let! _ = async { return () } |> Async.StartAsPromise |> Async.AwaitPromise
            return ()
        }
        |> Async.StartAsPromise
