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
open VibeFs.Shell.ChildAgentRegistry

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

/// Per-turn accumulator for RW-tool summaries captured during one assistant
/// turn. Immutable: each MarkRwTool derives a fresh record.
type DirectWriteTurn = { rwSummaries: string list; dirty: bool }

/// Single-responsibility immutable aggregate of all wiki host state (P52).
/// Every field was previously a naked Dictionary/ResizeArray/HashSet scattered
/// across the class; they now live in one record updated by the pure transition
/// functions below. `scheduledMaintenance` is an eternal dedup set: a
/// workspace+kind+value triple queues a background rewrite at most once per
/// process (this is intended deduplication, not a leak).
type WikiState =
    { sessionSnapshots: Map<string, WikiProjection>
      jobContexts: Map<string, WikiJobContext>
      bookkeeperLaunches: BookkeeperLaunch list
      directWriteTurns: Map<string, DirectWriteTurn>
      scheduledMaintenance: Set<string> }

let private initialWikiState : WikiState =
    { sessionSnapshots = Map.empty
      jobContexts = Map.empty
      bookkeeperLaunches = []
      directWriteTurns = Map.empty
      scheduledMaintenance = Set.empty }

/// Pure reducer transitions (P53): each takes the current state and returns the
/// next state, with no IO and no mutation of the input.
let private registerJob (state: WikiState) (sessionID: string) (ctx: WikiJobContext) : WikiState =
    { state with jobContexts = Map.add sessionID ctx state.jobContexts }

let private removeJob (state: WikiState) (sessionID: string) : WikiState =
    { state with jobContexts = Map.remove sessionID state.jobContexts }

let private tryJob (state: WikiState) (sessionID: string) : WikiJobContext option =
    Map.tryFind sessionID state.jobContexts

let private cacheSnapshot (state: WikiState) (sessionID: string) (projection: WikiProjection) : WikiState =
    { state with sessionSnapshots = Map.add sessionID projection state.sessionSnapshots }

let private markRwTool (state: WikiState) (sessionID: string) (entry: string) : WikiState =
    let turn = Map.tryFind sessionID state.directWriteTurns |> Option.defaultValue { rwSummaries = []; dirty = false }
    { state with directWriteTurns = Map.add sessionID { turn with rwSummaries = entry :: turn.rwSummaries; dirty = true } state.directWriteTurns }

/// Return the flushed RW summary when the turn is dirty, plus the state with
/// the turn consumed.
let private consumeDirtyTurn (state: WikiState) (sessionID: string) : string option * WikiState =
    match Map.tryFind sessionID state.directWriteTurns with
    | Some turn when turn.dirty ->
        let summary = String.concat "\n" (List.rev turn.rwSummaries)
        Some summary, { state with directWriteTurns = Map.remove sessionID state.directWriteTurns }
    | _ -> None, state

let private recordLaunch (state: WikiState) (launch: BookkeeperLaunch) : WikiState =
    { state with bookkeeperLaunches = state.bookkeeperLaunches @ [ launch ] }

/// Dedup by workspace+kind+value triple: returns whether this is the first
/// launch for the triple (caller queues the job only then) and the next state.
let private recordLaunchOnce (state: WikiState) (key: string) (launch: BookkeeperLaunch) : bool * WikiState =
    if Set.contains key state.scheduledMaintenance then false, state
    else true, { state with scheduledMaintenance = Set.add key state.scheduledMaintenance; bookkeeperLaunches = state.bookkeeperLaunches @ [ launch ] }

let private drainLaunches (state: WikiState) : BookkeeperLaunch list * WikiState =
    state.bookkeeperLaunches, { state with bookkeeperLaunches = [] }

/// Unified command type covering every state change in the runtime (P53). The
/// reducer is the single pure dispatch; multi-value transitions
/// (consumeDirtyTurn/recordLaunchOnce/drainLaunches) stay available as standalone
/// functions so callers needing their extra return value read it in the same tick.
type WikiCommand =
    | RegisterJobCmd of sessionID: string * ctx: WikiJobContext
    | RemoveJobCmd of sessionID: string
    | CacheSnapshotCmd of sessionID: string * projection: WikiProjection
    | MarkRwToolCmd of sessionID: string * entry: string
    | RecordLaunchCmd of launch: BookkeeperLaunch
    | RecordLaunchOnceCmd of key: string * launch: BookkeeperLaunch
    | DrainLaunchesCmd
    | ConsumeTurnCmd of sessionID: string

let private reducer (state: WikiState) (cmd: WikiCommand) : WikiState =
    match cmd with
    | RegisterJobCmd (sessionID, ctx) -> registerJob state sessionID ctx
    | RemoveJobCmd sessionID -> removeJob state sessionID
    | CacheSnapshotCmd (sessionID, projection) -> cacheSnapshot state sessionID projection
    | MarkRwToolCmd (sessionID, entry) -> markRwTool state sessionID entry
    | RecordLaunchCmd launch -> recordLaunch state launch
    | RecordLaunchOnceCmd (key, launch) -> recordLaunchOnce state key launch |> snd
    | DrainLaunchesCmd -> drainLaunches state |> snd
    | ConsumeTurnCmd sessionID -> consumeDirtyTurn state sessionID |> snd

let private invoke1 (target: obj) (methodName: string) (arg: obj) : JS.Promise<obj> =
    unbox (target?(methodName)(arg))

type WikiRuntime(client: obj, initialWorkspaceRoot: string, nowUtc: unit -> System.DateTime, registry: ChildAgentRegistry) =
    let mutable state = initialWikiState
    let commandQueue = SerialQueue()
    let writeQueues = Dictionary<string, SerialQueue>()
    let client = client
    let workspaceRoot = initialWorkspaceRoot

    let today () = (nowUtc ()).ToString("yyyy-MM-dd")

    /// Per-workspace write serialization (P51: the WikiActor class wrapper is
    /// gone; one flat map of queues lives on the runtime and serializes wiki
    /// writes so concurrent jobs on the same workspace never interleave).
    let getWorkspaceQueue (root: string) =
        match writeQueues.TryGetValue root with
        | true, queue -> queue
        | false, _ ->
            let queue = SerialQueue()
            writeQueues.[root] <- queue
            queue

    let postWorkspace (root: string) (work: unit -> JS.Promise<unit>) : unit =
        getWorkspaceQueue root |> fun queue -> queue.Enqueue(work) |> Promise.start

    let runWorkspace (root: string) (work: unit -> JS.Promise<string>) : JS.Promise<string> =
        getWorkspaceQueue root |> fun queue -> queue.Enqueue(work)

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

    /// Runs inside a commandQueue task: the create/prompt IO and the
    /// register/remove-job state updates are serialized against every other
    /// async method so no JS.Promise dangles mid state change.
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
                        state <- reducer state (RegisterJobCmd (childId, { workspaceRoot = root; kind = kind }))
                        registry.RegisterChildAgent(childId, "bookkeeper", None)
                        let promptBody =
                            box {| path = box {| id = childId |}
                                   body = box {| agent = "bookkeeper"
                                                 parts = [| box {| ``type`` = "text"; text = promptText |} |]
                                                 tools = box (createObj [ "submit_wiki", box true ]) |} |}
                        do! invoke1 session "prompt" promptBody |> Promise.map ignore
                with _ ->
                    if sessionId <> "" then
                        registry.UnregisterChildAgent(sessionId)
                        state <- reducer state (RemoveJobCmd sessionId)
        }

    let queueBackgroundLaunch (root: string) (kind: WikiJobKind) (title: string) (buildPrompt: unit -> JS.Promise<string>) : unit =
        match sessionApi () with
        | None -> ()
        | Some _ ->
            commandQueue.Enqueue(fun () ->
                promise {
                    try
                        let! promptText = buildPrompt ()
                        do! launchBackgroundSession root kind title promptText
                    with _ -> ()
                }) |> Promise.start

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
        if sessionID = "" then Promise.lift Map.empty
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    match Map.tryFind sessionID state.sessionSnapshots with
                    | Some projection -> return projection
                    | None ->
                        let! projection = readProjection (effectiveWorkspaceRoot directory)
                        state <- reducer state (CacheSnapshotCmd (sessionID, projection))
                        return projection
                })

    member _.RegisterJob(sessionID: string, ctx: WikiJobContext) : unit =
        state <- reducer state (RegisterJobCmd (sessionID, ctx))

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
        state <- reducer state (RemoveJobCmd sessionID)

    member _.Submit(sessionID: string, drafts: WikiDraft list) : JS.Promise<string> =
        commandQueue.Enqueue(fun () ->
            promise {
                match tryJob state sessionID with
                | None -> return "No active wiki job for this session."
                | Some ctx ->
                    try
                        let! result = runWorkspace ctx.workspaceRoot (fun () -> submitForKind ctx.workspaceRoot ctx.kind drafts)
                        return result
                    finally
                        registry.UnregisterChildAgent(sessionID)
                        state <- reducer state (RemoveJobCmd sessionID)
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
            state <- reducer state (MarkRwToolCmd (sessionID, $"{tool}: {trimmed}"))

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
                    if first then queueBackgroundLaunch root (DailyRewrite date) "Daily wiki rewrite" (fun () -> Promise.lift (buildDailyPrompt date files projection))
                    else ())

                weeklyDue
                |> Option.iter (fun cutoff ->
                    let key = root + "|weekly|" + cutoff
                    let launch = { agent = "bookkeeper"; title = "Weekly wiki snapshot rewrite"; prompt = $"weekly maintenance due through {cutoff}"; result = $"weekly:{cutoff}"; rwSummary = "" }
                    let first, nextState = recordLaunchOnce state key launch
                    state <- nextState
                    if first then queueBackgroundLaunch root (WeeklyRewrite cutoff) "Weekly wiki snapshot rewrite" (fun () -> Promise.lift (buildWeeklyPrompt cutoff files projection))
                    else ())
            })

    member _.RecordBookkeeperLaunch(agent: string, title: string, prompt: string, result: string, rwSummary: string) : unit =
        state <- reducer state (RecordLaunchCmd { agent = agent; title = title; prompt = prompt; result = result; rwSummary = rwSummary })

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, rwSummary: string) : unit =
        this.RecordBookkeeperLaunch("bookkeeper", title, prompt, result, rwSummary)
        let root = effectiveWorkspaceRoot workspaceRoot
        queueBackgroundLaunch root AppendAfterWork title (fun () ->
            promise {
                let! projection = readProjection root
                return buildAppendPrompt title prompt result rwSummary projection
            })

    member _.TakeBookkeeperLaunchesForTesting() : obj array =
        let launches, nextState = drainLaunches state
        state <- nextState
        launches |> List.map box |> List.toArray

    member _.WaitForBackgroundJobsForTesting() : JS.Promise<unit> =
        commandQueue.Enqueue(fun () -> Promise.lift ())
