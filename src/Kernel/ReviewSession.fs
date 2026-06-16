module VibeFs.Kernel.ReviewSession

open VibeFs.Kernel.Review

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
