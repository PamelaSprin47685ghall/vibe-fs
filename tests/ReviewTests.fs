module Wanxiangshu.Tests.ReviewTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Shell.ReviewRuntime

open Wanxiangshu.Tests.ReviewTestsReplay
open Wanxiangshu.Tests.ReviewTestsPrompts

let transition' () =
    let task = "review-task"
    let reviewerId = "reviewer-1"
    let feedback = "needs-revision-feedback"

    let states =
        [ ReviewState.Inactive
          ReviewState.Active task
          ReviewState.Locked(task, reviewerId)
          ReviewState.Accepted
          ReviewState.NeedsRevision feedback ]

    let commands =
        [ Activate task
          Submit
          Lock reviewerId
          Unlock
          Accept
          RequestRevision feedback ]

    states
    |> List.iter (fun state ->
        commands
        |> List.iter (fun command ->
            let nextState, event = transition state command

            match state, command with
            | ReviewState.Inactive, Activate t -> equal "Inactive+Activate→Active" (ReviewState.Active t) nextState
            | ReviewState.Active _, Submit -> check "Active+Submit event" (event = Some ReviewEvent.Submitted)
            | ReviewState.Active t, Lock rid -> equal "Active+Lock→Locked" (ReviewState.Locked(t, rid)) nextState
            | ReviewState.Active _, Accept -> equal "Active+Accept→Accepted" ReviewState.Accepted nextState
            | ReviewState.Active _, RequestRevision fb ->
                equal "Active+RequestRevision→NeedsRevision" (ReviewState.NeedsRevision fb) nextState
            | ReviewState.Locked _, Unlock ->
                check
                    "Locked+Unlock→Active"
                    (match nextState with
                     | ReviewState.Active _ -> true
                     | _ -> false)
            | ReviewState.Locked _, Accept -> equal "Locked+Accept→Accepted" ReviewState.Accepted nextState
            | ReviewState.Locked _, RequestRevision fb ->
                equal "Locked+RequestRevision→NeedsRevision" (ReviewState.NeedsRevision fb) nextState
            | ReviewState.Accepted, _ -> check "Accepted no-op" (nextState = state && event.IsNone)
            | ReviewState.NeedsRevision _, _ -> check "NeedsRevision no-op" (nextState = state && event.IsNone)
            | _ -> check "no-op state+event" (nextState = state && event.IsNone)))

let registry () =
    let activated =
        reduce emptyRegistry (RegistryAction.Activate("s1", "do thing", 100))

    check "activate creates session" (Map.containsKey "s1" activated)
    check "active review state is active" (hasActiveReviewState activated "s1")
    equal "task recorded" (Some "do thing") (taskOf activated "s1")
    let locked = reduce activated (RegistryAction.Lock("s1", "rev1"))
    check "locked state remains active" (hasActiveReviewState locked "s1")
    let accepted = reduce locked (RegistryAction.Accept "s1")
    check "accepted state is inactive" (not (hasActiveReviewState accepted "s1"))
    let needsRevision = reduce locked (RegistryAction.RequestRevision("s1", "fix it"))
    check "needs_revision state remains active for With-Review nudge" (hasActiveReviewState needsRevision "s1")
    check "clear empties" ((reduce accepted RegistryAction.Clear).IsEmpty)

let resultMapping () =
    equal "Accepted→Accept" (RegistryAction.Accept "s1") (actionFor "s1" (Accepted ""))

    equal
        "NeedsRevision→RequestRevision"
        (RegistryAction.RequestRevision("s1", "bad"))
        (actionFor "s1" (NeedsRevision "bad"))

    equal "Terminated→Deactivate" (RegistryAction.Deactivate "s1") (actionFor "s1" Terminated)

let reviewerLoop () =
    check
        "resolved finishes"
        (match decideAfterRound 0 (Resolved(Accepted "")) 3 with
         | Finish _ -> true
         | _ -> false)

    check
        "prompt-failed terminates"
        (match decideAfterRound 0 PromptFailed 3 with
         | Finish Terminated -> true
         | _ -> false)

    check
        "no-result nudges"
        (match decideAfterRound 0 NoResult 3 with
         | Nudge 1 -> true
         | _ -> false)

    check
        "exhausted nudges finish"
        (match decideAfterRound 2 NoResult 3 with
         | Finish Terminated -> true
         | _ -> false)

let runtime () =
    let store = createReviewStore ()
    store.activateReview ("w1", "task A", 100)
    check "store active" (store.getReviewState "w1" |> Option.isSome)
    equal "store task" (Some "task A") (store.getReviewTask "w1")
    check "store lock" (store.tryLockReview "w1")
    store.unlockReview "w1"
    let mutable fired = false
    store.setPendingReview ("w1", (fun _ -> fired <- true))
    check "resolve fires" (store.resolvePendingReview ("w1", Accepted ""))
    check "callback called" fired
    store.clearReviewSessions ()
    check "cleared" (store.getReviewState "w1" |> Option.isNone)

let promptPartsBranches () =
    let initial = [ "task body"; "extra detail" ]
    let nudge = "please answer"
    equal "first attempt uses initial parts" initial (promptParts 0 initial nudge)
    equal "retry 1 uses nudge" [ nudge ] (promptParts 1 initial nudge)
    equal "retry 5 uses nudge" [ nudge ] (promptParts 5 initial nudge)

let resolvePendingClearsSuppressor () =
    let mutable resolved: ReviewResult option = None
    let mutable suppressed = 0

    let effects =
        emptyEffects
        |> fun e -> setPending e "child-1" (fun result -> resolved <- Some result)
        |> fun e ->
            { e with
                abortSuppressors = Map.add "child-1" (fun () -> suppressed <- suppressed + 1) e.abortSuppressors }

    let next, fired = resolvePending effects "child-1" (Accepted "")
    check "fired flag true" fired
    equal "resolver received verdict" (Some(Accepted "")) resolved
    equal "suppressor invoked exactly once" 1 suppressed
    check "pending cleared" (not (Map.containsKey "child-1" next.pendingResolutions))
    check "suppressor cleared" (not (Map.containsKey "child-1" next.abortSuppressors))
    let next2, fired2 = resolvePending next "nonexistent" (Accepted "")
    check "unknown id → fired false" (not fired2)
    equal "pending count untouched on unknown id" next.pendingResolutions.Count next2.pendingResolutions.Count
    equal "suppressor count untouched on unknown id" next.abortSuppressors.Count next2.abortSuppressors.Count
