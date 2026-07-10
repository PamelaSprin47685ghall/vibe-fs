module Wanxiangshu.Kernel.ReviewSession.StateMachine

open Wanxiangshu.Kernel.ReviewSession.Types

let transition (state: ReviewState) (command: ReviewCommand) : ReviewState * ReviewEvent option =
    match state, command with
    | ReviewState.Inactive, Activate task -> ReviewState.Active task, Some(ReviewEvent.Activated task)
    | ReviewState.Active task, Submit -> ReviewState.Active task, Some ReviewEvent.Submitted
    | ReviewState.Active task, Lock reviewerId ->
        ReviewState.Locked(task, reviewerId), Some(ReviewEvent.LockAcquired reviewerId)
    | ReviewState.Active _, Accept -> ReviewState.Accepted, Some ReviewEvent.Accepted
    | ReviewState.Active _, RequestRevision feedback ->
        ReviewState.NeedsRevision feedback, Some(ReviewEvent.NeedsRevision feedback)
    | ReviewState.Locked(task, _), Unlock -> ReviewState.Active task, Some ReviewEvent.LockReleased
    | ReviewState.Locked _, Accept -> ReviewState.Accepted, Some ReviewEvent.Accepted
    | ReviewState.Locked _, RequestRevision feedback ->
        ReviewState.NeedsRevision feedback, Some(ReviewEvent.NeedsRevision feedback)
    // Inactive: only Activate is valid (matched above)
    | ReviewState.Inactive, Submit
    | ReviewState.Inactive, Lock _
    | ReviewState.Inactive, Unlock
    | ReviewState.Inactive, Accept
    | ReviewState.Inactive, RequestRevision _ -> state, None
    // Active: Submit/Lock/Accept/RequestRevision valid (above), Activate/Unlock invalid
    | ReviewState.Active _, Activate _
    | ReviewState.Active _, Unlock -> state, None
    // Locked: Unlock/Accept/RequestRevision valid (above), Activate/Submit/Lock invalid
    | ReviewState.Locked _, Activate _
    | ReviewState.Locked _, Submit
    | ReviewState.Locked _, Lock _ -> state, None
    // Accepted: all commands invalid
    | ReviewState.Accepted, Activate _
    | ReviewState.Accepted, Submit
    | ReviewState.Accepted, Lock _
    | ReviewState.Accepted, Unlock
    | ReviewState.Accepted, Accept
    | ReviewState.Accepted, RequestRevision _ -> state, None
    // NeedsRevision: all commands invalid
    | ReviewState.NeedsRevision _, Activate _
    | ReviewState.NeedsRevision _, Submit
    | ReviewState.NeedsRevision _, Lock _
    | ReviewState.NeedsRevision _, Unlock
    | ReviewState.NeedsRevision _, Accept
    | ReviewState.NeedsRevision _, RequestRevision _ -> state, None

let isActive (state: ReviewState) : bool =
    match state with
    | ReviewState.Inactive -> false
    | ReviewState.Accepted -> false
    | ReviewState.Active _ -> true
    | ReviewState.Locked _ -> true
    | ReviewState.NeedsRevision _ -> true

let initialState = ReviewState.Inactive

let applyCommand (session: ReviewSession) (command: ReviewCommand) : ReviewSession =
    let nextState, event = transition session.state command

    match event with
    | None -> session
    | Some _ ->
        { session with
            state = nextState
            version = session.version + 1 }

let decideAfterRound nudgeCount outcome maxNudges : LoopDecision =
    match outcome with
    | Resolved result -> Finish result
    | PromptFailed -> Finish Terminated
    | NoResult ->
        if nudgeCount + 1 >= maxNudges then
            Finish Terminated
        else
            Nudge(nudgeCount + 1)

let promptParts (nudgeCount: int) (initialParts: string list) (nudgePrompt: string) : string list =
    if nudgeCount = 0 then initialParts else [ nudgePrompt ]
