module Wanxiangshu.Tests.ReviewSessionStateMachineTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession.StateMachine
open Wanxiangshu.Kernel.ReviewSession.Types

// --- initialState ---

let initialStateIsInactive () =
    equal "initialState is Inactive" ReviewState.Inactive initialState

// --- isActive ---

let isActiveInactive () =
    check "isActive Inactive = false" (not (isActive ReviewState.Inactive))

let isActiveActive () =
    check "isActive Active = true" (isActive (ReviewState.Active "task"))

let isActiveLocked () =
    check "isActive Locked = true" (isActive (ReviewState.Locked("t", "r")))

let isActiveAccepted () =
    check "isActive Accepted = false" (not (isActive ReviewState.Accepted))

let isActiveRejected () =
    check "isActive Rejected = true" (isActive (ReviewState.Rejected "fb"))

// --- transition (all branches) ---

let transitionInactiveActivate () =
    let s, ev = transition ReviewState.Inactive (Activate "t")
    equal "state→Active" (ReviewState.Active "t") s
    equal "event→Activated" (Some (ReviewEvent.Activated "t")) ev

let transitionActiveSubmit () =
    let s, ev = transition (ReviewState.Active "t") Submit
    equal "state stays Active" (ReviewState.Active "t") s
    equal "event→Submitted" (Some ReviewEvent.Submitted) ev

let transitionActiveLock () =
    let s, ev = transition (ReviewState.Active "t") (Lock "rid")
    equal "state→Locked" (ReviewState.Locked("t", "rid")) s
    equal "event→LockAcquired" (Some (ReviewEvent.LockAcquired "rid")) ev

let transitionActiveAccept () =
    let s, ev = transition (ReviewState.Active "t") Accept
    equal "state→Accepted" ReviewState.Accepted s
    equal "event→Accepted" (Some ReviewEvent.Accepted) ev

let transitionActiveReject () =
    let s, ev = transition (ReviewState.Active "t") (Reject "fb")
    equal "state→Rejected" (ReviewState.Rejected "fb") s
    equal "event→Rejected" (Some (ReviewEvent.Rejected "fb")) ev

let transitionLockedUnlock () =
    let s, ev = transition (ReviewState.Locked("t", "rid")) Unlock
    equal "state→Active" (ReviewState.Active "t") s
    equal "event→LockReleased" (Some ReviewEvent.LockReleased) ev

let transitionLockedAccept () =
    let s, ev = transition (ReviewState.Locked("t", "rid")) Accept
    equal "state→Accepted" ReviewState.Accepted s
    equal "event→Accepted" (Some ReviewEvent.Accepted) ev

let transitionLockedReject () =
    let s, ev = transition (ReviewState.Locked("t", "rid")) (Reject "fb")
    equal "state→Rejected" (ReviewState.Rejected "fb") s
    equal "event→Rejected" (Some (ReviewEvent.Rejected "fb")) ev

let transitionNoopInactiveSubmit () =
    let s, ev = transition ReviewState.Inactive Submit
    equal "noop state" ReviewState.Inactive s
    equal "noop event" None ev

let transitionNoopAcceptedLock () =
    let s, ev = transition ReviewState.Accepted (Lock "rid")
    equal "noop state" ReviewState.Accepted s
    equal "noop event" None ev

let transitionNoopRejectedActivate () =
    let s, ev = transition (ReviewState.Rejected "x") (Activate "t")
    equal "noop state" (ReviewState.Rejected "x") s
    equal "noop event" None ev

// --- applyCommand ---

let applyCommandIncrementsVersion () =
    let session = { empty "s" 10L with state = ReviewState.Active "t"; version = 3 }
    let next = applyCommand session (Lock "rid")
    equal "version increments" 4 next.version
    equal "state→Locked" (ReviewState.Locked("t", "rid")) next.state

let applyCommandNoop () =
    let session = { empty "s" 10L with state = ReviewState.Inactive; version = 3 }
    let next = applyCommand session Submit
    equal "noop version unchanged" 3 next.version
    equal "noop state unchanged" ReviewState.Inactive next.state

// --- decideAfterRound ---

let decideAfterRoundResolvedAccepted () =
    equal "Resolved Accepted→Finish Accepted"
        (Finish (ReviewResult.Accepted "ok"))
        (decideAfterRound 0 (Resolved (ReviewResult.Accepted "ok")) 3)

let decideAfterRoundResolvedRejected () =
    equal "Resolved Rejected→Finish Rejected"
        (Finish (ReviewResult.Rejected "bad"))
        (decideAfterRound 0 (Resolved (ReviewResult.Rejected "bad")) 3)

let decideAfterRoundPromptFailed () =
    equal "PromptFailed→Finish Terminated"
        (Finish ReviewResult.Terminated)
        (decideAfterRound 0 PromptFailed 3)

let decideAfterRoundNoResultBelowMax () =
    equal "NoResult below max→Nudge 1"
        (Nudge 1)
        (decideAfterRound 0 NoResult 3)

let decideAfterRoundNoResultAtMax () =
    equal "NoResult at max→Finish Terminated"
        (Finish ReviewResult.Terminated)
        (decideAfterRound 2 NoResult 3)

// --- promptParts ---

let promptPartsZero () =
    let parts = promptParts 0 [ "init-a"; "init-b" ] "nudge-now"
    equal "nudgeCount=0 returns initialParts" [ "init-a"; "init-b" ] parts

let promptPartsNonZero () =
    let parts = promptParts 1 [ "init-a" ] "nudge-now"
    equal "nudgeCount=1 returns nudgePrompt only" [ "nudge-now" ] parts

// --- run ---

let run () =
    initialStateIsInactive ()
    isActiveInactive ()
    isActiveActive ()
    isActiveLocked ()
    isActiveAccepted ()
    isActiveRejected ()
    transitionInactiveActivate ()
    transitionActiveSubmit ()
    transitionActiveLock ()
    transitionActiveAccept ()
    transitionActiveReject ()
    transitionLockedUnlock ()
    transitionLockedAccept ()
    transitionLockedReject ()
    transitionNoopInactiveSubmit ()
    transitionNoopAcceptedLock ()
    transitionNoopRejectedActivate ()
    applyCommandIncrementsVersion ()
    applyCommandNoop ()
    decideAfterRoundResolvedAccepted ()
    decideAfterRoundResolvedRejected ()
    decideAfterRoundPromptFailed ()
    decideAfterRoundNoResultBelowMax ()
    decideAfterRoundNoResultAtMax ()
    promptPartsZero ()
    promptPartsNonZero ()
