module Wanxiangshu.Tests.ReviewSessionEffectsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession.Effects
open Wanxiangshu.Kernel.ReviewSession.Types

let emptyEffectsHasEmptyMaps () =
    let e = emptyEffects
    check "pendingResolutions empty" e.pendingResolutions.IsEmpty
    check "abortSuppressors empty" e.abortSuppressors.IsEmpty

let setPendingAddsEntry () =
    let e = setPending emptyEffects "s1" (fun _ -> ())
    check "setPending adds one" (e.pendingResolutions.Count = 1)
    check "setPending key s1" (Map.containsKey "s1" e.pendingResolutions)
    check "setPending does not touch suppressors" e.abortSuppressors.IsEmpty

let resolvePendingFiresCallback () =
    let mutable resolved = false
    let mutable suppressed = false
    let e =
        { emptyEffects with
            abortSuppressors = Map.add "s1" (fun () -> suppressed <- true) Map.empty }
        |> fun eff -> setPending eff "s1" (fun _ -> resolved <- true)
    let next, fired = resolvePending e "s1" (Accepted "")
    check "resolvePending fired true" fired
    check "resolvePending called resolver" resolved
    check "resolvePending called suppressor" suppressed
    check "resolvePending cleared pending" (not (Map.containsKey "s1" next.pendingResolutions))
    check "resolvePending cleared suppressor" (not (Map.containsKey "s1" next.abortSuppressors))

let resolvePendingUnknownIdReturnsFalse () =
    let e = setPending emptyEffects "s1" (fun _ -> ())
    let next, fired = resolvePending e "ghost" (Accepted "")
    check "unknown id fired false" (not fired)
    equal "pending count unchanged" e.pendingResolutions.Count next.pendingResolutions.Count
    equal "suppressor count unchanged" e.abortSuppressors.Count next.abortSuppressors.Count

let disposeSessionTreeTerminatesAll () =
    let mutable count = 0
    let mutable suppressed = 0
    let resolver _ = count <- count + 1
    let suppressor () = suppressed <- suppressed + 1
    let e =
        { emptyEffects with
            abortSuppressors =
                Map.empty
                |> Map.add "a" suppressor
                |> Map.add "b" suppressor }
        |> fun eff -> setPending eff "a" resolver
        |> fun eff -> setPending eff "b" resolver
        |> fun eff -> setPending eff "c" resolver
    let next = disposeSessionTree e [ "a"; "b"; "c" ]
    check "all 3 resolvers fired" (count = 3)
    check "2 suppressors fired" (suppressed = 2)
    check "no pending remain" next.pendingResolutions.IsEmpty
    check "no suppressors remain" next.abortSuppressors.IsEmpty
    let next2 = disposeSessionTree next [ "ghost" ]
    check "ghost leaves pending empty" next2.pendingResolutions.IsEmpty
    check "ghost leaves suppressors empty" next2.abortSuppressors.IsEmpty

let run () : unit =
    emptyEffectsHasEmptyMaps ()
    setPendingAddsEntry ()
    resolvePendingFiresCallback ()
    resolvePendingUnknownIdReturnsFalse ()
    disposeSessionTreeTerminatesAll ()
