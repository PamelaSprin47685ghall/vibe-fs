module VibeFs.Kernel.Review

/// The lifecycle of a single review, expressed as states that carry only the
/// data meaningful at that moment.  Illegal transitions are unrepresentable:
/// you cannot ask for the reviewerId of an Inactive review because the type
/// carries none.  RequireQualifiedAccess keeps state and event cases from
/// colliding — the compiler refuses to confuse a state with a fact.

[<RequireQualifiedAccess>]
type ReviewState =
    | Inactive
    | Active of task: string
    | Locked of task: string * reviewerId: string
    | Accepted
    | Rejected of feedback: string

/// A command is an intent the system may refuse.  No-op transitions return the
/// state unchanged with no event.
type ReviewCommand =
    | Activate of task: string
    | Submit
    | Lock of reviewerId: string
    | Unlock
    | Accept
    | Reject of feedback: string

/// An event is an irrefutable fact.  Replaying events must reproduce the same
/// state regardless of when it happens.
[<RequireQualifiedAccess>]
type ReviewEvent =
    | Activated of task: string
    | Submitted
    | LockAcquired of reviewerId: string
    | LockReleased
    | Accepted
    | Rejected of feedback: string

/// Pure state transition: given the current state and a command, return the
/// next state and an optional event.  Nested matches keep each state's
/// command-table readable and compiler-checked for exhaustiveness.
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

/// A review is "in play" while it can still be acted upon.
let isActive (state: ReviewState) : bool =
    match state with
    | ReviewState.Inactive -> false
    | ReviewState.Active _ -> true
    | ReviewState.Locked _ -> true
    | ReviewState.Accepted -> false
    | ReviewState.Rejected _ -> false

let initialState = ReviewState.Inactive
