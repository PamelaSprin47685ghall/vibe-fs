module VibeFs.Tests.ReviewTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.ReviewSession
open VibeFs.Shell.ReviewRuntime

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

/// P2-3: pure reviewer-loop primitives.  promptParts must hand out the initial
/// task prompt on the first attempt and the short nudge prompt on every retry.
let promptPartsBranches () =
    let initial = [ "task body"; "extra detail" ]
    let nudge = "please answer"
    let first = promptParts 0 initial nudge
    equal "first attempt uses initial parts" initial first
    let retry1 = promptParts 1 initial nudge
    equal "retry 1 uses nudge" [ nudge ] retry1
    let retry5 = promptParts 5 initial nudge
    equal "retry 5 uses nudge" [ nudge ] retry5

/// resolvePending must (a) fire the resolver, (b) clear the abort suppressor,
/// and (c) report whether anything was actually waiting.  The suppressor side
/// effect is what guarantees an aborted reviewer doesn't double-fire.
let resolvePendingClearsSuppressor () =
    let mutable resolved : ReviewResult option = None
    let mutable suppressed = 0
    let effects =
        emptyEffects
        |> fun e -> setPending e "child-1" (fun result -> resolved <- Some result)
        |> fun e -> { e with abortSuppressors = Map.add "child-1" (fun () -> suppressed <- suppressed + 1) e.abortSuppressors }

    let next, fired = resolvePending effects "child-1" Accepted
    check "fired flag true" fired
    equal "resolver received verdict" (Some Accepted) resolved
    equal "suppressor invoked exactly once" 1 suppressed
    check "pending cleared" (not (Map.containsKey "child-1" next.pendingResolutions))
    check "suppressor cleared" (not (Map.containsKey "child-1" next.abortSuppressors))

    // Resolving an unknown id is a no-op that reports false, never throws.
    let next2, fired2 = resolvePending next "nonexistent" Accepted
    check "unknown id → fired false" (not fired2)
    equal "pending count untouched on unknown id" next.pendingResolutions.Count next2.pendingResolutions.Count
    equal "suppressor count untouched on unknown id" next.abortSuppressors.Count next2.abortSuppressors.Count

/// disposeSessionTree must terminate every listed id, fire each suppressor, and
/// remove the entries — even when only some ids carry pending resolvers.  This
/// is the single path the host uses to clean up a whole reviewer subtree on
/// abort.
let disposeSessionTreeTerminatesAll () =
    let mutable verdicts : (string * ReviewResult) list = []
    let mutable suppressedOrder : string list = []
    let resolverFor id = fun result -> verdicts <- (id, result) :: verdicts
    let suppressorFor id = fun () -> suppressedOrder <- id :: suppressedOrder
    let effects =
        emptyEffects
        |> fun e -> setPending e "root" (resolverFor "root")
        |> fun e -> setPending e "child-a" (resolverFor "child-a")
        |> fun e -> setPending e "child-b" (resolverFor "child-b")
        |> fun e ->
            { e with
                abortSuppressors =
                    e.abortSuppressors
                    |> Map.add "root" (suppressorFor "root")
                    |> Map.add "child-a" (suppressorFor "child-a") }

    // Note: child-b has no suppressor — disposal must still remove it cleanly.
    let next = disposeSessionTree effects [ "root"; "child-a"; "child-b" ]
    check "all resolvers fired" (verdicts |> List.length = 3)
    check "all verdicts are Terminated" (verdicts |> List.forall (fun (_, r) -> r = Terminated))
    check "suppressors fired only where present" (suppressedOrder |> List.length = 2)
    check "no pending resolvers remain" next.pendingResolutions.IsEmpty
    check "no suppressors remain" next.abortSuppressors.IsEmpty

    // Ids that were never registered are ignored, not faulted.
    let next2 = disposeSessionTree next [ "ghost-1"; "ghost-2" ]
    check "disposing absent ids leaves pending empty" next2.pendingResolutions.IsEmpty
    check "disposing absent ids leaves suppressors empty" next2.abortSuppressors.IsEmpty
