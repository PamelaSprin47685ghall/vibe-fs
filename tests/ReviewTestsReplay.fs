module Wanxiangshu.Tests.ReviewTestsReplay

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.Effects
open Wanxiangshu.Runtime.LoopMessages

let disposeSessionTreeTerminatesAll () =
    let mutable verdicts: (string * ReviewResult) list = []
    let mutable suppressedOrder: string list = []

    let resolverFor id =
        fun result -> verdicts <- (id, result) :: verdicts

    let suppressorFor id =
        fun () -> suppressedOrder <- id :: suppressedOrder

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

    let next = disposeSessionTree effects [ "root"; "child-a"; "child-b" ]
    check "all resolvers fired" (verdicts |> List.length = 3)
    check "all verdicts are Terminated" (verdicts |> List.forall (fun (_, r) -> r = Terminated))
    check "suppressors fired only where present" (suppressedOrder |> List.length = 2)
    check "no pending resolvers remain" next.pendingResolutions.IsEmpty
    check "no suppressors remain" next.abortSuppressors.IsEmpty
    let next2 = disposeSessionTree next [ "ghost-1"; "ghost-2" ]
    check "disposing absent ids leaves pending empty" next2.pendingResolutions.IsEmpty
    check "disposing absent ids leaves suppressors empty" next2.abortSuppressors.IsEmpty
