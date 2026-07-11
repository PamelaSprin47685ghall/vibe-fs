module Wanxiangshu.Tests.ContextBudgetSpecs

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ContextBudget

let spec_F_N3_exactBoundary () =
    let bEff = 150000L
    let P = 0L
    let N = 3
    let threshold = bEff / int64 (N + 1)
    check "F N=3 at exact boundary returns true" (F threshold bEff P N 0)
    check "F N=3 just below boundary returns false" (not (F (threshold - 1L) bEff P N 0))

let spec_F_N2_exactBoundary () =
    let bEff = 150000L
    let P = 0L
    let N = 2
    let threshold = bEff / int64 (N + 1)
    check "F N=2 at exact boundary returns true" (F threshold bEff P N 0)
    check "F N=2 just below boundary returns false" (not (F (threshold - 1L) bEff P N 0))

let spec_F_N3_withPhaseBase () =
    let bEff = 150000L
    let P = 60000L
    let N = 3
    let threshold = (bEff + int64 N * P) / int64 (N + 1)
    check "F N=3 with P=60k triggers at threshold" (F threshold bEff P N 0)
    check "F N=3 with P=60k below threshold" (not (F (threshold - 1L) bEff P N 0))

let spec_F_highContext () =
    check "F high context triggers" (F 140000L 150000L 0L 3 0)

let spec_F_lowContext () =
    check "F low context does not trigger" (not (F 30000L 150000L 0L 3 0))

let spec_F_int64_noOverflow () =
    let big = 2147483647L

    check
        "F does not overflow at int32 max"
        (F big big big 3 0 |> ignore
         true)

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
    let state =
        { phaseBaseTokens = 30000L
          backlogTokensAtPhaseStart = 0L }

    equal "pressure emergency at 120k/150k" RequireTodoWriteEmergency (classifyPressure 150000 false 120000L state 0)

let spec_classifyPressure_below () =
    let state =
        { phaseBaseTokens = 30000L
          backlogTokensAtPhaseStart = 0L }

    equal "pressure below threshold" BelowThreshold (classifyPressure 150000 false 30000L state 0)

let spec_classifyPressure_compacting () =
    let bEff = effectiveMaxInputTokens 150000
    let phaseBase = (bEff * 8L) / 10L

    let state =
        { phaseBaseTokens = phaseBase
          backlogTokensAtPhaseStart = 0L }

    equal "pressure compacting" Compacting (classifyPressure 150000 false phaseBase state 0)

/// Bug repro: phase reset 后旧逻辑把 phaseBaseTokens 设成 stableTokens
/// (≈ currentTokens)，导致 F 阈值退化到 ~bEff（100%）。修复后继承旧 P。
///
/// N=3, bEff=150000. P=30000 → threshold = (150000+90000)/4 = 60000.
/// P=120000 (bug) → threshold = (150000+360000)/4 = 127500 (85%).
/// At a=120000: bug → BelowThreshold; correct → RequireTodoWriteEmergency.
let spec_phaseReset_degradesThreshold () =
    let maxInputTokens = 150000
    let currentTokens = 100000L

    let buggyState =
        { phaseBaseTokens = currentTokens
          backlogTokensAtPhaseStart = 10000L }

    let correctState =
        { phaseBaseTokens = 30000L
          backlogTokensAtPhaseStart = 10000L }

    let buggyResult = classifyPressure maxInputTokens false currentTokens buggyState 0

    let correctResult =
        classifyPressure maxInputTokens false currentTokens correctState 0

    equal "bug suppresses nudge at 67pct" BelowThreshold buggyResult
    equal "correct fires nudge at 67pct" RequireTodoWriteEmergency correctResult

/// N parameter: foldAfterFirst=true (N=2) triggers earlier than false (N=3).
/// N=3 threshold = bEff/4 = 37500. N=2 threshold = bEff/3 = 50000.
/// At a=40000: N=3 triggers (4*40000=160000 >= 150000), N=2 does not
/// (3*40000=120000 < 150000).
let spec_foldAfterFirst_triggersEarlier () =
    let maxInputTokens = 150000

    let state =
        { phaseBaseTokens = 0L
          backlogTokensAtPhaseStart = 0L }

    let a = 40000L

    check "N=3 triggers at 27pct" (classifyPressure maxInputTokens false a state 0 = RequireTodoWriteEmergency)
    check "N=2 does not trigger at 27pct" (classifyPressure maxInputTokens true a state 0 = BelowThreshold)

let spec_nudgeTrack_transitions () =
    equal "phase reset" Idle (afterPhaseBoundaryReset EmergencySignaled)
    equal "after emergency" EmergencySignaled (afterEmergencyNudge Idle)

let test_scenario1_N3_R0 () =
    let bEff = 150000L
    let P = 30000L
    let N = 3
    let R = 0
    let threshold = (bEff + int64 N * P) / int64 (N + 1)
    check "Scenario 1: N=3 R=0 triggers at threshold" (F threshold bEff P N R)
    check "Scenario 1: N=3 R=0 does not trigger below threshold" (not (F (threshold - 1L) bEff P N R))

let test_scenario2_N3_R1 () =
    let bEff = 150000L
    let P = 30000L
    let N = 3
    let R = 1
    let threshold = (2L * bEff + 2L * P) / int64 (N + 1)
    check "Scenario 2: N=3 R=1 triggers at threshold" (F threshold bEff P N R)
    check "Scenario 2: N=3 R=1 does not trigger below threshold" (not (F (threshold - 1L) bEff P N R))

let test_scenario3_N3_R2 () =
    let bEff = 150000L
    let P = 30000L
    let N = 3
    let R = 2
    let threshold = (3L * bEff + P) / int64 (N + 1)
    check "Scenario 3: N=3 R=2 triggers at threshold" (F threshold bEff P N R)
    check "Scenario 3: N=3 R=2 does not trigger below threshold" (not (F (threshold - 1L) bEff P N R))

let test_scenario4_N2_R0 () =
    let bEff = 150000L
    let P = 30000L
    let N = 2
    let R = 0
    let threshold = (bEff + 2L * P) / int64 (N + 1)
    check "Scenario 4: N=2 R=0 triggers at threshold" (F threshold bEff P N R)
    check "Scenario 4: N=2 R=0 does not trigger below threshold" (not (F (threshold - 1L) bEff P N R))

let test_scenario5_N2_R1 () =
    let bEff = 150000L
    let P = 30000L
    let N = 2
    let R = 1
    let threshold = (2L * bEff + P) / int64 (N + 1)
    check "Scenario 5: N=2 R=1 triggers at threshold" (F threshold bEff P N R)
    check "Scenario 5: N=2 R=1 does not trigger below threshold" (not (F (threshold - 1L) bEff P N R))

let test_scenario6_N3_R3_clamping () =
    let bEff = 150000L
    let P = 30000L

    let state =
        { phaseBaseTokens = P
          backlogTokensAtPhaseStart = 0L }

    let pressureWithR3 = classifyPressure 150000 false 120000L state 3
    let pressureWithR2 = classifyPressure 150000 false 120000L state 2
    equal "Scenario 6: R=3 clamps to R=2" pressureWithR2 pressureWithR3

let test_scenario7_R_negative_clamping () =
    let bEff = 150000L
    let P = 30000L

    let state =
        { phaseBaseTokens = P
          backlogTokensAtPhaseStart = 0L }

    let pressureWithRNeg = classifyPressure 150000 false 60000L state -1
    let pressureWithR0 = classifyPressure 150000 false 60000L state 0
    equal "Scenario 7: R=-1 clamps to R=0" pressureWithR0 pressureWithRNeg

let test_scenario8_P_large_compacting () =
    let bEff = effectiveMaxInputTokens 150000
    let P = (bEff * 8L) / 10L

    let state =
        { phaseBaseTokens = P
          backlogTokensAtPhaseStart = 0L }

    let pressure = classifyPressure 150000 false 120000L state 0
    equal "Scenario 8: P >= 80% bEff triggers Compacting" Compacting pressure

let test_scenario9_maxTokens_disabled () =
    let state =
        { phaseBaseTokens = 30000L
          backlogTokensAtPhaseStart = 0L }

    let pressure = classifyPressure 0 false 60000L state 0
    equal "Scenario 9: maxTokens <= 0 disables pressure nudge" Disabled pressure

let test_scenario10_nudge_frequency_progression () =
    let bEff = 150000L
    let P = 30000L

    let state =
        { phaseBaseTokens = P
          backlogTokensAtPhaseStart = 0L }

    let pressureR0 = classifyPressure 150000 false 70000L state 0
    equal "Scenario 10: R=0 triggers nudge at 70k" RequireTodoWriteEmergency pressureR0
    let pressureR1 = classifyPressure 150000 false 70000L state 1
    equal "Scenario 10: R=1 does not trigger nudge at 70k" BelowThreshold pressureR1
    let pressureR2 = classifyPressure 150000 false 70000L state 2
    equal "Scenario 10: R=2 does not trigger nudge at 70k" BelowThreshold pressureR2

let run () : unit =
    spec_F_N3_exactBoundary ()
    spec_F_N2_exactBoundary ()
    spec_F_N3_withPhaseBase ()
    spec_F_highContext ()
    spec_F_lowContext ()
    spec_F_int64_noOverflow ()
    spec_estimateTokens_withRatio ()
    spec_estimateTokens_noLastUsage ()
    spec_beginPhase_computesState ()
    spec_afterSuccessfulTodo_sameAsBeginPhase ()
    spec_classifyPressure_emergency ()
    spec_classifyPressure_below ()
    spec_classifyPressure_compacting ()
    spec_phaseReset_degradesThreshold ()
    spec_foldAfterFirst_triggersEarlier ()
    spec_nudgeTrack_transitions ()
    test_scenario1_N3_R0 ()
    test_scenario2_N3_R1 ()
    test_scenario3_N3_R2 ()
    test_scenario4_N2_R0 ()
    test_scenario5_N2_R1 ()
    test_scenario6_N3_R3_clamping ()
    test_scenario7_R_negative_clamping ()
    test_scenario8_P_large_compacting ()
    test_scenario9_maxTokens_disabled ()
    test_scenario10_nudge_frequency_progression ()
