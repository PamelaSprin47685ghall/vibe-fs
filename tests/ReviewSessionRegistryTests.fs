module Wanxiangshu.Tests.ReviewSessionRegistryTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.Registry
open Wanxiangshu.Kernel.ReviewSession.StateMachine

let run () =
    // Activate creates
    let r1 = reduce emptyRegistry (RegistryAction.Activate("s1", "do thing", 100L))
    check "Activate creates" (Map.containsKey "s1" r1)
    check "Activate active" (isActive r1.["s1"].state)
    equal "Activate task" (Some "do thing") r1.["s1"].originalTask

    // Lock
    let r2 = reduce r1 (RegistryAction.Lock("s1", "rev1"))

    match Map.tryFind "s1" r2 with
    | Some s ->
        match s.state with
        | ReviewState.Locked(t, r) ->
            equal "Lock task" "do thing" t
            equal "Lock reviewer" "rev1" r
        | _ -> check "Lock->Locked" false

        check "Locked still active" (isActive s.state)
    | None -> check "s1 exists after Lock" false

    // Accept
    let r3 = reduce r2 (RegistryAction.Accept "s1")

    match Map.tryFind "s1" r3 with
    | Some s -> equal "Accept->Accepted" ReviewState.Accepted s.state
    | None -> check "s1 exists after Accept" false

    check "Accepted not active" (not (isActive r3.["s1"].state))

    // RequestRevision
    let r4 = reduce r2 (RegistryAction.RequestRevision("s1", "fix it"))

    match Map.tryFind "s1" r4 with
    | Some s -> equal "RequestRevision->NeedsRevision" (ReviewState.NeedsRevision "fix it") s.state
    | None -> check "s1 exists after RequestRevision" false

    check "NeedsRevision still active" (isActive r4.["s1"].state)

    // Deactivate
    let r5d = reduce r1 (RegistryAction.Deactivate "s1")
    check "Deactivate removes" (not (Map.containsKey "s1" r5d))

    // Evict
    let oldS = Map.add "old" (empty "old" 50L) emptyRegistry
    let mixed = Map.add "fresh" (empty "fresh" 200L) oldS
    let evicted = reduce mixed (RegistryAction.Evict 100L)
    check "Evict keeps fresh" (Map.containsKey "fresh" evicted)
    check "Evict removes old" (not (Map.containsKey "old" evicted))

    // AddChild
    let r6 = reduce r1 (RegistryAction.AddChild("s1", "c1"))

    match Map.tryFind "s1" r6 with
    | Some s -> equal "AddChild" [ "c1" ] s.childIds
    | None -> check "AddChild session exists" false

    // Clear
    let r7 = reduce r1 RegistryAction.Clear
    check "Clear empties" r7.IsEmpty

    // actionFor
    equal "Accepted->Accept" (RegistryAction.Accept "s1") (actionFor "s1" (Accepted ""))

    equal
        "NeedsRevision->RequestRevision"
        (RegistryAction.RequestRevision("s1", "bad"))
        (actionFor "s1" (NeedsRevision "bad"))

    equal "Terminated->NoOp" RegistryAction.NoOp (actionFor "s1" Terminated)
