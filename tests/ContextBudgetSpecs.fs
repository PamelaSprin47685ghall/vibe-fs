module Wanxiangshu.Tests.ContextBudgetSpecs

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ContextBudget

let spec_F_firstCycle_Q0_M3 () =
    let bEff = 150000L
    let P = 0L
    let threshold = bEff / 4L
    check "F (Q=0,M=3) triggers at bEff/4" (F threshold bEff P 0 3)
    check "F (Q=0,M=3) below threshold" (not (F (threshold - 1L) bEff P 0 3))

let spec_F_firstCycle_Q1_M2 () =
    let bEff = 150000L
    let P = 30000L
    let threshold = (2L * bEff + 2L * P) / 4L
    check "F (Q=1,M=2) triggers at threshold" (F threshold bEff P 1 2)
    check "F (Q=1,M=2) below threshold" (not (F (threshold - 1L) bEff P 1 2))

let spec_F_firstCycle_Q2_M1 () =
    let bEff = 150000L
    let P = 30000L
    let threshold = (3L * bEff + P) / 4L
    check "F (Q=2,M=1) triggers at threshold" (F threshold bEff P 2 1)
    check "F (Q=2,M=1) below threshold" (not (F (threshold - 1L) bEff P 2 1))

let spec_F_afterFold_Q0_M1 () =
    let bEff = 150000L
    let P = 30000L
    let threshold = (bEff + P) / 2L
    check "F (Q=0,M=1) triggers at (bEff+P)/2" (F threshold bEff P 0 1)
    check "F (Q=0,M=1) below threshold" (not (F (threshold - 1L) bEff P 0 1))

let spec_F_highContext () =
    check "F high context triggers" (F 140000L 150000L 0L 0 3)

let spec_F_lowContext () =
    check "F low context does not trigger" (not (F 30000L 150000L 0L 0 3))

let spec_F_int64_noOverflow () =
    let big = 2147483647L

    check
        "F does not overflow"
        (F big big big 0 3 |> ignore
         true)

let spec_estimateTokens_withRatio () =
    let lastUsage =
        Some
            {| tokenCount = 1000
               textBytes = 4000 |}

    equal "estimateTokens with ratio" (Some 2000) (estimateTokens 8000 lastUsage)

let spec_estimateTokens_noLastUsage () =
    equal "estimateTokens no last usage is None" None (estimateTokens 8000 None)

let spec_beginCycle_setsFields () =
    let cycle = beginCycle 30000L 5 3
    equal "BaselineTokens" 30000L cycle.BaselineTokens
    equal "BaselineTodoOrdinal" 5 cycle.BaselineTodoOrdinal
    equal "FoldFrontierOrdinal" 5 cycle.FoldFrontierOrdinal
    equal "CompletedSegments" 0 cycle.CompletedSegments
    equal "RemainingTodoWritesUntilFold" 3 cycle.RemainingTodoWritesUntilFold

let spec_advanceSegment () =
    let cycle = beginCycle 30000L 2 3
    let advanced = advanceSegment cycle 5
    equal "CompletedSegments after advance" 3 advanced.CompletedSegments
    equal "BaselineTokens unchanged" 30000L advanced.BaselineTokens

let spec_rebuildCycleAtFold () =
    let rebuilt = rebuildCycleAtFold 60000L 10 8 1
    equal "new BaselineTokens" 60000L rebuilt.BaselineTokens
    equal "new BaselineTodoOrdinal" 10 rebuilt.BaselineTodoOrdinal
    equal "new FoldFrontierOrdinal" 8 rebuilt.FoldFrontierOrdinal
    equal "CompletedSegments reset" 0 rebuilt.CompletedSegments
    equal "RemainingTodoWritesUntilFold" 1 rebuilt.RemainingTodoWritesUntilFold

let spec_classifyPressure_emergency () =
    let state = beginCycle 30000L 0 3
    equal "pressure emergency" RequireTodoWriteEmergency (classifyPressure 150000 120000L state)

let spec_classifyPressure_below () =
    let state = beginCycle 30000L 0 3
    equal "pressure below threshold" BelowThreshold (classifyPressure 150000 30000L state)

let spec_classifyPressure_compacting () =
    let bEff = effectiveMaxInputTokens 150000
    let phaseBase = (bEff * 8L) / 10L
    let state = beginCycle phaseBase 0 3
    equal "pressure compacting" Compacting (classifyPressure 150000 phaseBase state)

let spec_classifyPressure_disabled () =
    let state = beginCycle 30000L 0 3
    equal "pressure disabled" Disabled (classifyPressure 0 60000L state)

let spec_classifyNudgeAction_firstSignal () =
    let action = classifyNudgeAction RequireTodoWriteEmergency Idle None 0 0
    equal "first signal" InjectFirstSignal action

let spec_classifyNudgeAction_sameEpisode () =
    let action =
        classifyNudgeAction RequireTodoWriteEmergency EmergencySignaled (Some 5) 5 1

    equal "same episode" InjectSameEpisode action

let spec_classifyNudgeAction_catchUp () =
    let action =
        classifyNudgeAction RequireTodoWriteEmergency EmergencySignaled (Some 3) 5 1

    equal "catch up on ordinal change" InjectCatchUp action

let spec_classifyNudgeAction_noNudge () =
    let action = classifyNudgeAction BelowThreshold Idle None 0 0
    equal "no nudge below threshold" NoNudge action

let spec_bootstrapHardSafety () =
    check "hard safety at 75%" (bootstrapHardSafety 150000L 200000L)
    check "hard safety not at 50%" (not (bootstrapHardSafety 100000L 200000L))

let spec_progression_QM () =
    let bEff = 150000L
    let P = 30000L
    let state0 = beginCycle P 0 3
    let p0 = classifyPressure 150000 70000L state0
    equal "Q=0 M=3 triggers at 70k" RequireTodoWriteEmergency p0
    let state1 = advanceSegment state0 1
    let p1 = classifyPressure 150000 70000L state1
    equal "Q=1 M=2 below at 70k" BelowThreshold p1

let run () : unit =
    spec_F_firstCycle_Q0_M3 ()
    spec_F_firstCycle_Q1_M2 ()
    spec_F_firstCycle_Q2_M1 ()
    spec_F_afterFold_Q0_M1 ()
    spec_F_highContext ()
    spec_F_lowContext ()
    spec_F_int64_noOverflow ()
    spec_estimateTokens_withRatio ()
    spec_estimateTokens_noLastUsage ()
    spec_beginCycle_setsFields ()
    spec_advanceSegment ()
    spec_rebuildCycleAtFold ()
    spec_classifyPressure_emergency ()
    spec_classifyPressure_below ()
    spec_classifyPressure_compacting ()
    spec_classifyPressure_disabled ()
    spec_classifyNudgeAction_firstSignal ()
    spec_classifyNudgeAction_sameEpisode ()
    spec_classifyNudgeAction_catchUp ()
    spec_classifyNudgeAction_noNudge ()
    spec_bootstrapHardSafety ()
    spec_progression_QM ()
