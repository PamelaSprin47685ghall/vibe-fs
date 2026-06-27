module Wanxiangshu.Tests.ReviewSessionQueryTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.Registry
open Wanxiangshu.Kernel.ReviewSession.StateMachine

let private nowVal : int64 = 1_700_000_000_000L
let private mkReviewSession id st ver =
    { empty id nowVal with state = st; version = ver }

let private regFromList sessions =
    sessions |> List.fold (fun r (s: ReviewSession) -> Map.add s.id s r) emptyRegistry

// sessionIsActive
let sessionIsActiveActive () =
    check "Active is active" (Wanxiangshu.Kernel.ReviewSession.Query.sessionIsActive
        (regFromList [ mkReviewSession "s" (ReviewState.Active "t") 0 ]) "s")

let sessionIsActiveLocked () =
    check "Locked is active" (Wanxiangshu.Kernel.ReviewSession.Query.sessionIsActive
        (regFromList [ mkReviewSession "s" (ReviewState.Locked("t", "r")) 0 ]) "s")

let sessionIsActiveInactive () =
    check "Inactive not active" (not (Wanxiangshu.Kernel.ReviewSession.Query.sessionIsActive
        (regFromList [ mkReviewSession "s" ReviewState.Inactive 0 ]) "s"))

let sessionIsActiveAccepted () =
    check "Accepted not active" (not (Wanxiangshu.Kernel.ReviewSession.Query.sessionIsActive
        (regFromList [ mkReviewSession "s" ReviewState.Accepted 0 ]) "s"))

let sessionIsActiveRejected () =
    check "Rejected is active" (Wanxiangshu.Kernel.ReviewSession.Query.sessionIsActive
        (regFromList [ mkReviewSession "s" (ReviewState.Rejected "bad") 0 ]) "s")

let sessionIsActiveMissing () =
    check "missing not active" (not (Wanxiangshu.Kernel.ReviewSession.Query.sessionIsActive emptyRegistry "nope"))

// taskOf
let taskOfPresent () =
    let task = "task-alpha"
    let s = { empty "s" nowVal with state = ReviewState.Active task; originalTask = Some task }
    equal "taskOf returns task" (Some task) (Wanxiangshu.Kernel.ReviewSession.Query.taskOf (regFromList [ s ]) "s")

let taskOfMissing () =
    equal "taskOf missing None" None (Wanxiangshu.Kernel.ReviewSession.Query.taskOf emptyRegistry "nope")

// stateOf
let stateOfPresent () =
    match Wanxiangshu.Kernel.ReviewSession.Query.stateOf (regFromList [ mkReviewSession "s" (ReviewState.Locked("t", "r")) 0 ]) "s" with
    | Some (ReviewState.Locked(task, rev)) ->
        equal "stateOf task" "t" task
        equal "stateOf rev" "r" rev
    | _ -> check "stateOf Locked" false

let stateOfMissing () =
    equal "stateOf missing None" None (Wanxiangshu.Kernel.ReviewSession.Query.stateOf emptyRegistry "nope")

// canTransition
let canTransitionTrue () =
    check "Inactive+Activate valid" (Wanxiangshu.Kernel.ReviewSession.Query.canTransition
        (regFromList [ mkReviewSession "s" ReviewState.Inactive 0 ]) "s" (ReviewCommand.Activate "t"))

let canTransitionFalseForNoop () =
    check "Active+Submit noop" (not (Wanxiangshu.Kernel.ReviewSession.Query.canTransition
        (regFromList [ mkReviewSession "s" (ReviewState.Active "t") 0 ]) "s" ReviewCommand.Submit))

let canTransitionMissing () =
    check "missing can't transition" (not (Wanxiangshu.Kernel.ReviewSession.Query.canTransition emptyRegistry "nope" (ReviewCommand.Activate "t")))

// versionOf
let versionOfPresent () =
    equal "versionOf 5" (Some 5) (Wanxiangshu.Kernel.ReviewSession.Query.versionOf
        (regFromList [ mkReviewSession "s" ReviewState.Accepted 5 ]) "s")

let versionOfMissing () =
    equal "versionOf missing None" None (Wanxiangshu.Kernel.ReviewSession.Query.versionOf emptyRegistry "nope")

// reduceIfVersionMatches
let reduceIfVersionMatchApply () =
    let s = { empty "s" nowVal with state = ReviewState.Active "t"; version = 3 }
    match Wanxiangshu.Kernel.ReviewSession.Query.reduceIfVersionMatches (regFromList [ s ]) "s" 3 (RegistryAction.Accept "s") with
    | Some r ->
        check "match Some" true
        match Map.tryFind "s" r with
        | Some s2 -> equal "accept applied" ReviewState.Accepted s2.state
        | None -> check "session in result" false
    | None -> check "match Some" false

let reduceIfVersionMismatchReturnsNone () =
    let s = { empty "s" nowVal with state = ReviewState.Active "t"; version = 3 }
    match Wanxiangshu.Kernel.ReviewSession.Query.reduceIfVersionMatches (regFromList [ s ]) "s" 99 (RegistryAction.Accept "s") with
    | Some _ -> check "mismatch None" false
    | None -> check "mismatch None" true

let reduceIfVersionMissingSessionReturnsNone () =
    match Wanxiangshu.Kernel.ReviewSession.Query.reduceIfVersionMatches emptyRegistry "nope" 1 (RegistryAction.Activate("nope", "t", nowVal)) with
    | Some _ -> check "missing None" false
    | None -> check "missing None" true

let run () =
    sessionIsActiveActive ()
    sessionIsActiveLocked ()
    sessionIsActiveInactive ()
    sessionIsActiveAccepted ()
    sessionIsActiveRejected ()
    sessionIsActiveMissing ()
    taskOfPresent ()
    taskOfMissing ()
    stateOfPresent ()
    stateOfMissing ()
    canTransitionTrue ()
    canTransitionFalseForNoop ()
    canTransitionMissing ()
    versionOfPresent ()
    versionOfMissing ()
    reduceIfVersionMatchApply ()
    reduceIfVersionMismatchReturnsNone ()
    reduceIfVersionMissingSessionReturnsNone ()
