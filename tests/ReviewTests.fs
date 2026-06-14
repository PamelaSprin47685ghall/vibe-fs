module VibeFs.Tests.ReviewTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.Review
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.ReviewerLoop
open VibeFs.Kernel.ReviewRuntime

let transition' () =
    let task = "review-task"
    let reviewerId = "reviewer-1"
    let feedback = "rejected-feedback"
    let states =
        [ ReviewState.Inactive; ReviewState.Active task; ReviewState.Locked(task, reviewerId)
          ReviewState.Accepted; ReviewState.Rejected feedback ]
    let commands = [ Activate task; Submit; Lock reviewerId; Unlock; Accept; Reject feedback ]
    states |> List.iter (fun state ->
        commands |> List.iter (fun command ->
            let nextState, event = transition state command
            match state, command with
            | ReviewState.Inactive, Activate t ->
                equal "Inactive+Activate→Active" (ReviewState.Active t) nextState
            | ReviewState.Active _, Submit -> check "Active+Submit event" (event = Some ReviewEvent.Submitted)
            | ReviewState.Active t, Lock rid -> equal "Active+Lock→Locked" (ReviewState.Locked(t, rid)) nextState
            | ReviewState.Active _, Accept -> equal "Active+Accept→Accepted" ReviewState.Accepted nextState
            | ReviewState.Active _, Reject fb -> equal "Active+Reject→Rejected" (ReviewState.Rejected fb) nextState
            | ReviewState.Locked _, Unlock -> check "Locked+Unlock→Active" (match nextState with ReviewState.Active _ -> true | _ -> false)
            | ReviewState.Locked _, Accept -> equal "Locked+Accept→Accepted" ReviewState.Accepted nextState
            | ReviewState.Locked _, Reject fb -> equal "Locked+Reject→Rejected" (ReviewState.Rejected fb) nextState
            | ReviewState.Accepted, _ -> check "Accepted no-op" (nextState = state && event.IsNone)
            | ReviewState.Rejected _, _ -> check "Rejected no-op" (nextState = state && event.IsNone)
            | _ -> check "no-op state+event" (nextState = state && event.IsNone)))

let registry () =
    let activated = reduce emptyRegistry (RegistryAction.Activate("s1", "do thing", 100))
    check "activate creates session" (Map.containsKey "s1" activated)
    check "active session is active" (sessionIsActive activated "s1")
    equal "task recorded" (Some "do thing") (taskOf activated "s1")
    let locked = reduce activated (RegistryAction.Lock("s1", "rev1"))
    check "locked active" (sessionIsActive locked "s1")
    let accepted = reduce locked (RegistryAction.Accept "s1")
    check "accepted not active" (not (sessionIsActive accepted "s1"))
    check "clear empties" ((reduce accepted RegistryAction.Clear).IsEmpty)

let resultMapping () =
    equal "Accepted→Accept" (RegistryAction.Accept "s1") (actionFor "s1" Accepted)
    equal "Rejected→Reject" (RegistryAction.Reject("s1", "bad")) (actionFor "s1" (Rejected "bad"))
    equal "Terminated→Deactivate" (RegistryAction.Deactivate "s1") (actionFor "s1" Terminated)

let reviewerLoop () =
    check "resolved finishes" (match decideAfterRound 0 (Resolved Accepted) 3 with Finish _ -> true | _ -> false)
    check "prompt-failed terminates" (match decideAfterRound 0 PromptFailed 3 with Finish Terminated -> true | _ -> false)
    check "no-result nudges" (match decideAfterRound 0 NoResult 3 with Nudge 1 -> true | _ -> false)
    check "exhausted nudges finish" (match decideAfterRound 2 NoResult 3 with Finish Terminated -> true | _ -> false)

let runtime () =
    let store = createReviewStore ()
    store.activateReview ("w1", "task A", 100)
    check "store active" (store.isReviewActive "w1")
    equal "store task" (Some "task A") (store.getReviewTask "w1")
    check "store lock" (store.tryLockReview "w1")
    store.unlockReview "w1"
    let mutable fired = false
    store.setPendingReview ("w1", fun _ -> fired <- true)
    check "resolve fires" (store.resolvePendingReview ("w1", Accepted))
    check "callback called" fired
    store.clearReviewSessions ()
    check "cleared" (not (store.isReviewActive "w1"))
