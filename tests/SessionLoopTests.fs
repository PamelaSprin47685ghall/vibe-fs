module Wanxiangshu.Tests.SessionLoopTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.SessionLoop

// --- decide: priority-ordered gate selection ---

let decideAllOpenFallbackFirst () =
    let gates =
        { NeedFallbackContinue = true
          NeedTodoNudge = true
          NeedReviewNudge = true }

    equal "all open → FallbackContinue" FallbackContinue (decide gates)

let decideFallbackClosedTodoOpen () =
    let gates =
        { NeedFallbackContinue = false
          NeedTodoNudge = true
          NeedReviewNudge = true }

    equal "fallback closed, todo+review open → TodoNudge" TodoNudge (decide gates)

let decideOnlyReviewOpen () =
    let gates =
        { NeedFallbackContinue = false
          NeedTodoNudge = false
          NeedReviewNudge = true }

    equal "only review open → ReviewNudge" ReviewNudge (decide gates)

let decideAllClosedResolve () =
    let gates =
        { NeedFallbackContinue = false
          NeedTodoNudge = false
          NeedReviewNudge = false }

    equal "all closed → Resolve" Resolve (decide gates)

// --- drive: finite trace produces expected sequence, halts ---

let driveProducesPrioritySequence () =
    let transitions =
        { NeedFallbackContinue = true
          NeedTodoNudge = true
          NeedReviewNudge = true }
        :: [ { NeedFallbackContinue = false
               NeedTodoNudge = true
               NeedReviewNudge = true }
             { NeedFallbackContinue = false
               NeedTodoNudge = false
               NeedReviewNudge = true }
             { NeedFallbackContinue = false
               NeedTodoNudge = false
               NeedReviewNudge = false } ]

    let mutable i = 1
    let trace = ResizeArray<GateAction>()

    let step (_: GateState) (_: GateAction) : GateState =
        let state = transitions.[i]
        i <- i + 1
        state

    drive step (fun action -> trace.Add action) transitions.[0]

    let expected: GateAction list =
        [ FallbackContinue; TodoNudge; ReviewNudge; Resolve ]

    let label = "drive trace = " + "[FallbackContinue; TodoNudge; ReviewNudge; Resolve]"
    equal label expected (trace |> Seq.toList)

let driveStopsAfterResolve () =
    let mutable count = 0
    let mutable afterResolve = 0

    let step (_: GateState) (_: GateAction) : GateState =
        count <- count + 1

        { NeedFallbackContinue = false
          NeedTodoNudge = false
          NeedReviewNudge = false }

    drive
        step
        (fun action ->
            if action = Resolve then
                afterResolve <- afterResolve + 1
            else if afterResolve > 0 then
                afterResolve <- afterResolve + 100)
        { NeedFallbackContinue = true
          NeedTodoNudge = true
          NeedReviewNudge = true }

    equal "Resolve emitted exactly once" 1 afterResolve
    equal "step called exactly once (no loop after Resolve)" 1 count

// --- run ---

let run () =
    decideAllOpenFallbackFirst ()
    decideFallbackClosedTodoOpen ()
    decideOnlyReviewOpen ()
    decideAllClosedResolve ()
    driveProducesPrioritySequence ()
    driveStopsAfterResolve ()
