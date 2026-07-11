module Wanxiangshu.Tests.SessionLoopTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.SessionGateDemand
open Wanxiangshu.Kernel.SessionLoop

let decideAllOpenFallbackFirst () =
    let mode = gateModeFromDemand (resolveGateDemand true true true)
    equal "all open → FallbackContinue" FallbackContinue (decide mode)

let decideFallbackClosedTodoOpen () =
    let mode = gateModeFromDemand (resolveGateDemand false true true)
    equal "fallback closed, todo+review open → TodoNudge" TodoNudge (decide mode)

let decideOnlyReviewOpen () =
    let mode = gateModeFromDemand (resolveGateDemand false false true)
    equal "only review open → ReviewNudge" ReviewNudge (decide mode)

let decideAllClosedResolve () =
    let mode = gateModeFromDemand (resolveGateDemand false false false)
    equal "all closed → Resolve" Resolve (decide mode)

let driveProducesPrioritySequence () =
    let transitions =
        [ gateModeFromDemand (resolveGateDemand true true true)
          gateModeFromDemand (resolveGateDemand false true true)
          gateModeFromDemand (resolveGateDemand false false true)
          gateModeFromDemand (resolveGateDemand false false false) ]

    let mutable i = 1
    let trace = ResizeArray<GateAction>()

    let step (_: SessionGateMode) (_: GateAction) : SessionGateMode =
        let mode = transitions.[i]
        i <- i + 1
        mode

    drive step (fun action -> trace.Add action) transitions.[0]

    let expected: GateAction list =
        [ FallbackContinue; TodoNudge; ReviewNudge; Resolve ]

    let label = "drive trace = " + "[FallbackContinue; TodoNudge; ReviewNudge; Resolve]"
    equal label expected (trace |> Seq.toList)

let driveStopsAfterResolve () =
    let mutable count = 0
    let mutable afterResolve = 0

    let step (_: SessionGateMode) (_: GateAction) : SessionGateMode =
        count <- count + 1
        gateModeFromDemand (resolveGateDemand false false false)

    drive
        step
        (fun action ->
            if action = Resolve then
                afterResolve <- afterResolve + 1
            elif afterResolve > 0 then
                afterResolve <- afterResolve + 100)
        (gateModeFromDemand (resolveGateDemand true true true))

    equal "Resolve emitted exactly once" 1 afterResolve
    equal "step called exactly once (no loop after Resolve)" 1 count

let run () =
    decideAllOpenFallbackFirst ()
    decideFallbackClosedTodoOpen ()
    decideOnlyReviewOpen ()
    decideAllClosedResolve ()
    driveProducesPrioritySequence ()
    driveStopsAfterResolve ()
