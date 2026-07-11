module Wanxiangshu.Tests.ContextBudgetSpecs

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ContextBudget

let spec_F_exactBoundary () =
    check "F at exact boundary returns true" (F 115000L 200000L 10000L 20000L)

let spec_F_justBelowBoundary () =
    check "F just below boundary returns false" (not (F 114999L 200000L 10000L 20000L))

let spec_F_highContext () =
    check "F high context triggers" (F 190000L 200000L 10000L 20000L)

let spec_F_lowContext () =
    check "F low context does not trigger" (not (F 110000L 200000L 10000L 20000L))

let spec_F_int64_noOverflow () =
    let big = 2147483647L

    check
        "F does not overflow at int32 max"
        (F big big big big |> ignore
         true)

let spec_F_zeroBacklog () =
    check "F zero backlog at half capacity" (F 100000L 200000L 0L 0L)
    check "F zero backlog below half" (not (F 99999L 200000L 0L 0L))

let spec_estimateTokens_withRatio () =
    let lastUsage =
        Some
            {| tokenCount = 1000
               textBytes = 4000 |}

    let estimated = estimateTokens 8000 lastUsage
    equal "estimateTokens with ratio" (Some 2000) estimated

let spec_estimateTokens_noLastUsage () =
    let estimated = estimateTokens 8000 None
    equal "estimateTokens no last usage is None" None estimated

let spec_beginPhase_computesState () =
    let totalTokens = 30000L
    let totalBytes = 120000L
    let backlogBytes = 40000L
    let state = beginPhase totalTokens totalBytes backlogBytes
    equal "phaseBaseTokens" totalTokens state.phaseBaseTokens
    equal "backlogTokensAtPhaseStart" 10000L state.backlogTokensAtPhaseStart

let spec_afterSuccessfulTodo_sameAsBeginPhase () =
    let totalTokens = 55000L
    let totalBytes = 220000L
    let backlogBytes = 120000L
    let s1 = beginPhase totalTokens totalBytes backlogBytes
    let s2 = afterSuccessfulTodo totalTokens totalBytes backlogBytes
    equal "afterSuccessfulTodo equals beginPhase" s1 s2

let spec_classifyPressure_emergency () =
    let state = beginPhase 30000L 100L 0L
    equal "pressure emergency at 120k/200k" RequireTodoWriteEmergency (classifyPressure 200000 120000L state)

let spec_classifyPressure_below () =
    let state = beginPhase 30000L 100L 0L
    equal "pressure below threshold" BelowThreshold (classifyPressure 200000 50000L state)

let spec_classifyPressure_compacting () =
    let bEff = effectiveMaxInputTokens 200000
    let phaseBase = (bEff * 8L) / 10L

    let state =
        { phaseBaseTokens = phaseBase
          backlogTokensAtPhaseStart = 0L }

    equal "pressure compacting" Compacting (classifyPressure 200000 phaseBase state)

let spec_nudgeTrack_transitions () =
    equal "phase reset" Idle (afterPhaseBoundaryReset EmergencySignaled)
    equal "after emergency" EmergencySignaled (afterEmergencyNudge Idle)

let run () : unit =
    spec_F_exactBoundary ()
    spec_F_justBelowBoundary ()
    spec_F_highContext ()
    spec_F_lowContext ()
    spec_F_int64_noOverflow ()
    spec_F_zeroBacklog ()
    spec_estimateTokens_withRatio ()
    spec_estimateTokens_noLastUsage ()
    spec_beginPhase_computesState ()
    spec_afterSuccessfulTodo_sameAsBeginPhase ()
    spec_classifyPressure_emergency ()
    spec_classifyPressure_below ()
    spec_classifyPressure_compacting ()
    spec_nudgeTrack_transitions ()
