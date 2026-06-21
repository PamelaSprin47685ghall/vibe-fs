module VibeFs.Mux.WikiTools

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.WikiRuntimeState
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.WikiMaintenance
open VibeFs.Kernel.WikiPrompts
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Shell.WikiFiles
open VibeFs.Shell.WikiPortLock
open VibeFs.Shell.PromiseQueue

let private buildEntries (root: string) (drafts: WikiDraft list) : JS.Promise<WikiEntry list> =
    promise {
        let! files = readWikiFiles root
        let projection = projectLatestWins files
        let normalizedDrafts = normalizeDraftIds projection drafts
        let allocate (knownIds: Set<string>) : string =
            let random = System.Random()
            match Wiki.allocateRandomHexId (fun () -> random.Next(0, 65536)) knownIds with
            | Ok id -> id
            | Error message -> failwith message
        match applyDrafts allocate projection normalizedDrafts with
        | Ok entries -> return entries
        | Error error -> return raise (exn error)
    }

let private extractTexts (item: obj) : string list =
    if Dyn.typeIs item "string" then [ string item ]
    else
        let texts = ResizeArray<string>()
        let content = Dyn.str item "content"
        if content <> "" then texts.Add(content)
        let text = Dyn.str item "text"
        if text <> "" then texts.Add(text)
        let parts = Dyn.get item "parts"
        if not (Dyn.isNullish parts) && Dyn.isArray parts then
            for p in (parts :?> obj array) do
                let partText = Dyn.str p "text"
                if partText <> "" then texts.Add(partText)
        List.ofSeq texts

type MuxWikiRuntime(?deps: obj) as this =
    let registeredJobs = System.Collections.Generic.Dictionary<string, WikiJobContext>()
    let writeQueue = SerialQueue()
    let commandQueue = SerialQueue()
    let launchQueue = SerialQueue()
    let mutable state = initialWikiState
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

    let launchBg root kind title promptText =
        match latestConfig with
        | Some cfg ->
            let options = Some (box {| aiSettingsAgentId = "bookkeeper" |})
            launchQueue.Enqueue(fun () ->
                promise {
                    try
                        let! _ = delegateToSubAgent deps cfg "bookkeeper" promptText title options
                        recordBackgroundResult title "success"
                    with ex ->
                        recordBackgroundResult title (string ex)
                }) |> Promise.start
        | None -> ()

    member _.TryResolveJobContext(sessionID: string) : JS.Promise<WikiJobContext option> =
        promise {
            if System.String.IsNullOrWhiteSpace sessionID then return None
            else
                match getChatHistory with
                | None -> return None
                | Some getHistory ->
                    try
                        let! history = getHistory sessionID
                        let texts =
                            history
                            |> Array.toList
                            |> List.collect extractTexts
                        return texts |> List.tryPick tryParseJobMarker
                    with _ -> return None
        }

    member _.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<WikiProjection> =
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

    member this.BuildPreludeForSession(sessionID: string, directory: string) : JS.Promise<string option> =
        promise {
            if not (wikiDirExists directory) then return None
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                return buildPreludeSection projection
        }

    member this.FetchFromSessionSnapshot(sessionID: string, directory: string, id: string) : JS.Promise<string> =
        promise {
            if System.String.IsNullOrWhiteSpace directory then
                return "No wiki directory provided."
            elif not (wikiDirExists directory) then
                return "Wiki directory not found."
            elif sessionID = "" then
                let! projection = readProjection directory
                match fetchAnswer projection id with
                | Ok answer -> return answer
                | Error message -> return message
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                match fetchAnswer projection id with
                | Ok answer -> return answer
                | Error message -> return message
        }

    member _.RegisterJob(_sessionID: string, _ctx: WikiJobContext) : unit =
        registeredJobs.[_sessionID] <- _ctx

    member this.RegisterJobForTesting(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        if System.String.IsNullOrWhiteSpace workspaceRoot then
            failwith "Wiki job workspaceRoot must be a non-empty directory path."

        let payloadObj = payload

        let readRequiredField (fieldName: string) : string =
            let value = str payloadObj fieldName
            if value.Trim() = "" then failwith $"Wiki job payload missing required field '{fieldName}'"
            else value.Trim()

        let kind =
            let normalizedTag = kindTag.Trim().ToLowerInvariant()
            let builders =
                Map [
                    "append", fun () -> AppendAfterWork
                    "daily", fun () -> DailyRewrite(readRequiredField "date")
                    "weekly", fun () -> WeeklyRewrite(readRequiredField "through")
                ]
            match Map.tryFind normalizedTag builders with
            | Some build -> build ()
            | None -> failwith $"Unknown wiki job kind: {normalizedTag}"

        this.RegisterJob(sessionID, { workspaceRoot = workspaceRoot; kind = kind })

    member _.TakeJob(sessionID: string) : WikiJobContext option =
        match registeredJobs.TryGetValue sessionID with
        | true, ctx -> Some ctx
        | false, _ -> None

    member _.DeleteJob(sessionID: string) : unit =
        registeredJobs.Remove(sessionID) |> ignore

    member this.StartMaintenanceIfDue(workspaceRoot: string) : JS.Promise<unit> =
        if not (wikiDirExists workspaceRoot) then Promise.lift ()
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    let! files = readWikiFiles workspaceRoot
                    let projection = projectLatestWins files
                    let dailyDue, weeklyDue = dueMaintenance files System.DateTime.UtcNow

                    let launchIfDue (due: string list) kind title resultPrefix promptInfix buildPrompt =
                        due
                        |> List.iter (fun value ->
                            let key = workspaceRoot + "|" + resultPrefix + "|" + value
                            let launch = { agent = "bookkeeper"; title = title; prompt = $"{resultPrefix} maintenance due {promptInfix} {value}"; result = $"{resultPrefix}:{value}" }
                            let first, nextState = recordLaunchOnce state key launch
                            state <- nextState
                            if first then
                                let promptText = buildPrompt value files projection
                                launchBg workspaceRoot (kind value) title promptText)

                    launchIfDue dailyDue DailyRewrite "Daily wiki rewrite" "daily" "for" buildDailyPrompt
                    launchIfDue (Option.toList weeklyDue) WeeklyRewrite "Weekly wiki snapshot rewrite" "weekly" "through" buildWeeklyPrompt
                })

    member this.Submit(sessionID: string, directory: string, drafts: WikiDraft list, ?config: obj) : JS.Promise<string> =
        if not (wikiDirExists directory) then Promise.lift "Wiki directory not found."
        else
            promise {
                match config with
                | Some cfg -> latestConfig <- Some cfg
                | None -> ()

                let! reconstructed = this.TryResolveJobContext(sessionID)
                let jobCtxOpt =
                    reconstructed |> Option.orElseWith (fun () ->
                        match registeredJobs.TryGetValue sessionID with
                        | true, ctx -> Some ctx
                        | false, _ -> None)

                match jobCtxOpt with
                | None -> return "No active wiki job for this session."
                | Some ctx ->
                    let root = ctx.workspaceRoot
                    let todayStr = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
                    return! writeQueue.Enqueue(fun () ->
                        promise {
                            let! entries = buildEntries root drafts
                            let kind = ctx.kind
                            let! result =
                                withWikiPortLock 30000L 1000 root (fun () ->
                                    match kind with
                                    | AppendAfterWork ->
                                        promise {
                                            do! appendEntries root todayStr entries
                                            registeredJobs.Remove(sessionID) |> ignore
                                            return $"Appended {entries.Length} wiki entries."
                                        }
                                    | DailyRewrite date ->
                                        promise {
                                            do! rewriteDay root date entries
                                            registeredJobs.Remove(sessionID) |> ignore
                                            return $"Rewrote wiki day {date}."
                                        }
                                    | WeeklyRewrite throughDate ->
                                        promise {
                                            do! rewriteSnapshot root throughDate entries
                                            do! deleteDayFilesThrough root throughDate
                                            registeredJobs.Remove(sessionID) |> ignore
                                            return $"Rewrote wiki snapshot through {throughDate}."
                                        })
                            return result
                        })
            }

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, ?config: obj) : unit =
        let root =
            match config with
            | Some cfg when not (Dyn.isNullish cfg) ->
                let dir = Dyn.str cfg "directory"
                if dir <> "" then dir else defaultArg (strField cfg "cwd") ""
            | _ -> ""
        if root = "" || not (wikiDirExists root) then ()
        else
            state <- reducer state (RecordLaunchCmd { agent = "bookkeeper"; title = title; prompt = prompt; result = result })
            match config with
            | Some cfg when not (Dyn.isNullish cfg) -> latestConfig <- Some cfg
            | _ -> ()
            this.StartMaintenanceIfDue(root) |> ignore
            if not (Dyn.isNullish deps) then
                match latestConfig with
                | Some cfg when not (Dyn.isNullish cfg) ->
                    launchQueue.Enqueue(fun () ->
                        promise {
                            try
                                let! projection = readProjection root
                                let promptText = buildAppendPrompt title prompt result projection
                                let options = Some (box {| aiSettingsAgentId = "bookkeeper" |})
                                let! _ = delegateToSubAgent deps cfg "bookkeeper" promptText title options
                                recordBackgroundResult title "success"
                            with ex ->
                                recordBackgroundResult title (string ex)
                        }) |> Promise.start
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
            do! launchQueue.Enqueue(fun () -> Promise.lift ())
        }

    member val startMaintenanceIfDue = System.Func<string, JS.Promise<unit>>(fun workspaceRoot -> this.StartMaintenanceIfDue(workspaceRoot)) with get, set
    member val takeBookkeeperLaunchesForTesting = System.Func<obj array>(fun () -> this.TakeBookkeeperLaunchesForTesting()) with get, set
    member val waitForBackgroundJobsForTesting = System.Func<JS.Promise<unit>>(fun () -> this.WaitForBackgroundJobsForTesting()) with get, set

let private wikiDraftEntrySchema : obj =
    createObj
        [ "type", box "object"
          "properties",
          box
              (createObj
                  [ "id", box (createObj [ "type", box "string"; "description", box "Existing wiki entry id to update" ])
                    "q", box (createObj [ "type", box "string"; "description", box "Question" ])
                    "a", box (createObj [ "type", box "string"; "description", box "Answer" ]) ])
          "required", box [| "q"; "a" |]
          "additionalProperties", box false ]

let fetchWikiTool (wikiRuntime: MuxWikiRuntime) : ToolDefinition =
    { name = "fetch_wiki"
      description = description "fetch_wiki"
      parameters = mkSchema (createObj [ "id", box (strProp Params.fetchWikiId) ]) [| "id" |]
      execute =
          fun config args ->
              let sessionID = Dyn.str config "sessionID"
              let directory =
                  let current = Dyn.str config "directory"
                  if current = "" then defaultArg (strField config "cwd") "" else current
              wikiRuntime.FetchFromSessionSnapshot(sessionID, directory, Dyn.str args "id")
      condition = None }

let returnBookkeeperTool (wikiRuntime: MuxWikiRuntime) : ToolDefinition =
    { name = "return_bookkeeper"
      description = description "return_bookkeeper"
      parameters =
          mkSchema
              (createObj
                  [ "entries",
                    box
                        (createObj
                            [ "type", box "array"
                              "items", box wikiDraftEntrySchema
                              "description", box Params.submitWikiEntries ]) ])
              [| "entries" |]
      execute =
          fun config args ->
              let sessionID = Dyn.str config "sessionID"
              let directory =
                  let current = Dyn.str config "directory"
                  if current = "" then defaultArg (strField config "cwd") "" else current
              match parseDraftArray (Dyn.get args "entries") with
              | Error message -> resolveStr message
              | Ok drafts -> wikiRuntime.Submit(sessionID, directory, drafts, config)
      condition = None }
