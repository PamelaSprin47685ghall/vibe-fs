module VibeFs.Kernel.ReviewSession

[<RequireQualifiedAccess>]
type ReviewState =
    | Inactive
    | Active of task: string
    | Locked of task: string * reviewerId: string
    | Accepted
    | Rejected of feedback: string

type ReviewCommand =
    | Activate of task: string
    | Submit
    | Lock of reviewerId: string
    | Unlock
    | Accept
    | Reject of feedback: string

[<RequireQualifiedAccess>]
type ReviewEvent =
    | Activated of task: string
    | Submitted
    | LockAcquired of reviewerId: string
    | LockReleased
    | Accepted
    | Rejected of feedback: string

let transition (state: ReviewState) (command: ReviewCommand) : ReviewState * ReviewEvent option =
    match state with
    | ReviewState.Inactive ->
        match command with
        | Activate task -> ReviewState.Active task, Some(ReviewEvent.Activated task)
        | _ -> state, None
    | ReviewState.Active task ->
        match command with
        | Submit -> state, Some ReviewEvent.Submitted
        | Lock reviewerId -> ReviewState.Locked(task, reviewerId), Some(ReviewEvent.LockAcquired reviewerId)
        | Accept -> ReviewState.Accepted, Some ReviewEvent.Accepted
        | Reject feedback -> ReviewState.Rejected feedback, Some(ReviewEvent.Rejected feedback)
        | _ -> state, None
    | ReviewState.Locked(task, _) ->
        match command with
        | Unlock -> ReviewState.Active task, Some ReviewEvent.LockReleased
        | Accept -> ReviewState.Accepted, Some ReviewEvent.Accepted
        | Reject feedback -> ReviewState.Rejected feedback, Some(ReviewEvent.Rejected feedback)
        | _ -> state, None
    | ReviewState.Accepted -> state, None
    | ReviewState.Rejected _ -> state, None

let isActive (state: ReviewState) : bool =
    match state with
    | ReviewState.Inactive -> false
    | ReviewState.Active _ -> true
    | ReviewState.Locked _ -> true
    | ReviewState.Accepted -> false
    | ReviewState.Rejected _ -> false

let initialState = ReviewState.Inactive

/// The terminal outcome of a review round, distinguishable from the ongoing
/// state machine: Accepted and Rejected echo the verdict, Terminated means the
/// session was disposed without a verdict.
type ReviewResult = Accepted | Rejected of feedback: string | Terminated

/// A session is the review state machine plus the provenance the host needs to
/// render and link sessions.  Immutable: every mutation produces a new record.
type ReviewSession =
    { id: string
      version: int
      state: ReviewState
      createdAt: int64
      originalTask: string option
      lastFeedback: string option
      parentId: string option
      childIds: string list }

let empty id createdAt : ReviewSession =
    { id = id; version = 0; state = ReviewState.Inactive; createdAt = createdAt
      originalTask = None; lastFeedback = None; parentId = None; childIds = [] }

/// Apply a command through the pure transition, returning a new session when the
/// state actually changes.  Structural equality lets us skip no-op writes.
let applyCommand (session: ReviewSession) (command: ReviewCommand) : ReviewSession =
    let nextState, _ = transition session.state command
    if nextState = session.state then session
    else { session with state = nextState; version = session.version + 1 }

let withTask (task: string) (session: ReviewSession) : ReviewSession =
    if session.originalTask = Some task then session
    else { session with originalTask = Some task; version = session.version + 1 }
let withFeedback (session: ReviewSession) (feedback: string) : ReviewSession =
    if session.lastFeedback = Some feedback then session
    else { session with lastFeedback = Some feedback; version = session.version + 1 }
let addChild session childId =
    if List.contains childId session.childIds then session
    else { session with childIds = session.childIds @ [ childId ]; version = session.version + 1 }

/// The registry is a fold over session-level actions — the event-sourcing
/// reduce.  Every action is data; `reduce` is the single interpreter.
[<RequireQualifiedAccess>]
type RegistryAction =
    | Activate of id: string * task: string * createdAt: int64
    | Deactivate of id: string
    | Evict of cutoff: int64
    | Lock of id: string * reviewerId: string
    | Unlock of id: string
    | Accept of id: string
    | Reject of id: string * feedback: string
    | AddChild of parentId: string * childId: string
    | Clear

type Registry = Map<string, ReviewSession>

let emptyRegistry : Registry = Map.empty

let private set registry id session = Map.add id session registry

let private transitionIn registry id command (extra: ReviewSession -> ReviewSession) =
    match Map.tryFind id registry with
    | None -> registry
    | Some session ->
        let updated = applyCommand session command
        if updated = session then registry else set registry id (extra updated)

let private patch registry id f =
    match Map.tryFind id registry with
    | None -> registry
    | Some session -> set registry id (f session)

let private evictStale registry cutoff : Registry =
    let changed =
        registry
        |> Map.exists (fun _ s -> s.createdAt < cutoff)
    if not changed then registry
    else registry |> Map.filter (fun _ s -> s.createdAt >= cutoff)

/// The single reducer.  Pattern match is exhaustive over RegistryAction —
/// adding a case is a compile error until it is handled here.
let reduce (registry: Registry) (action: RegistryAction) : Registry =
    match action with
    | RegistryAction.Activate (id, task, createdAt) ->
        let seed =
            Map.tryFind id registry
            |> Option.defaultValue (empty id createdAt)
            |> withTask task
        set registry id (applyCommand seed (ReviewCommand.Activate task))
    | RegistryAction.Lock (id, reviewerId) ->
        transitionIn registry id (ReviewCommand.Lock reviewerId) (fun s -> s)
    | RegistryAction.Unlock id ->
        transitionIn registry id ReviewCommand.Unlock (fun s -> s)
    | RegistryAction.Accept id ->
        transitionIn registry id ReviewCommand.Accept (fun s -> s)
    | RegistryAction.Reject (id, feedback) ->
        transitionIn registry id (ReviewCommand.Reject feedback) (fun s -> withFeedback s feedback)
    | RegistryAction.Deactivate id -> Map.remove id registry
    | RegistryAction.Evict cutoff -> evictStale registry cutoff
    | RegistryAction.AddChild (parentId, childId) ->
        patch registry parentId (fun s -> addChild s childId)
    | RegistryAction.Clear -> emptyRegistry

// ── Queries ──────────────────────────────────────────────────────────────────

let actionFor (id: string) (result: ReviewResult) : RegistryAction =
    match result with
    | Accepted -> RegistryAction.Accept id
    | Rejected feedback -> RegistryAction.Reject(id, feedback)
    | Terminated -> RegistryAction.Deactivate id

let sessionIsActive registry id =
    Map.tryFind id registry |> Option.map (fun s -> isActive s.state) |> Option.defaultValue false

let taskOf registry id = Map.tryFind id registry |> Option.bind (fun s -> s.originalTask)

let stateOf registry id =
    Map.tryFind id registry |> Option.map (fun s -> s.state)

let canTransition registry id command =
    match Map.tryFind id registry with
    | None -> false
    | Some session ->
        let nextState, _ = transition session.state command
        nextState <> session.state

let versionOf registry id =
    Map.tryFind id registry |> Option.map (fun session -> session.version)

let reduceIfVersionMatches (registry: Registry) (id: string) (expectedVersion: int) (action: RegistryAction) : Registry option =
    match Map.tryFind id registry with
    | Some session when session.version = expectedVersion -> Some(reduce registry action)
    | _ -> None

// ── Reviewer loop ─────────────────────────────────────────────────────────────

/// What happened in a single reviewer round.
type RoundOutcome =
    | Resolved of result: ReviewResult
    | PromptFailed
    | NoResult

/// What the orchestrator should do after a round: stop with a result, or nudge.
type LoopDecision =
    | Finish of result: ReviewResult
    | Nudge of nudgeCount: int

/// Decide the next move after a round.  Resolved ends the loop; a failed prompt
/// terminates; running out of nudges terminates; otherwise nudge and retry.
let decideAfterRound (nudgeCount: int) (outcome: RoundOutcome) (maxNudges: int) : LoopDecision =
    match outcome with
    | Resolved result -> Finish result
    | PromptFailed -> Finish Terminated
    | NoResult ->
        let next = nudgeCount + 1
        if next >= maxNudges then Finish Terminated else Nudge next

/// Initial round uses the task prompt; every retry uses the short nudge prompt.
let promptParts (nudgeCount: int) (initialParts: string list) (nudgePrompt: string) : string list =
    if nudgeCount = 0 then initialParts else [ nudgePrompt ]

/// Pending review resolutions and their abort suppressors, keyed by session.
type SessionEffects =
    { pendingResolutions: Map<string, ReviewResult -> unit>
      abortSuppressors: Map<string, unit -> unit> }

let emptyEffects : SessionEffects =
    { pendingResolutions = Map.empty; abortSuppressors = Map.empty }

let setPending effects sessionId resolve =
    { effects with pendingResolutions = Map.add sessionId resolve effects.pendingResolutions }

/// Fire the pending resolver for a session, then clean up its suppressor.
/// Returns true when a resolver was actually waiting.
let resolvePending (effects: SessionEffects) sessionId result : SessionEffects * bool =
    match Map.tryFind sessionId effects.pendingResolutions with
    | None -> effects, false
    | Some resolve ->
        resolve result
        let without = { effects with pendingResolutions = Map.remove sessionId effects.pendingResolutions }
        match Map.tryFind sessionId effects.abortSuppressors with
        | Some suppress -> suppress(); { without with abortSuppressors = Map.remove sessionId without.abortSuppressors }, true
        | None -> without, true

/// Resolve every pending session in a tree as Terminated, firing suppressors.
let disposeSessionTree (effects: SessionEffects) sessionIds : SessionEffects =
    sessionIds
    |> List.fold (fun acc id ->
        match Map.tryFind id acc.pendingResolutions with
        | Some resolve ->
            resolve Terminated
            let cleared = { acc with pendingResolutions = Map.remove id acc.pendingResolutions }
            match Map.tryFind id cleared.abortSuppressors with
            | Some suppress -> suppress(); { cleared with abortSuppressors = Map.remove id cleared.abortSuppressors }
            | None -> cleared
        | None -> acc) effects