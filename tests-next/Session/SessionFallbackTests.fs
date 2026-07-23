namespace Wanxiangshu.Next.Tests

open Xunit
open Wanxiangshu.Next.Session

module SessionFallbackTests =

    [<Fact>]
    let ``Fallback_sequence_outcome_1_A_retry`` () =
        let state0 = Fallback.initial
        Assert.Equal(ModelSide.A, state0.Side)
        Assert.Equal(0, state0.Failures)

        let decision1 = Fallback.nextAttempt state0

        match decision1 with
        | FallbackDecision.NextAttempt state1 ->
            Assert.Equal(ModelSide.A, state1.Side)
            Assert.Equal(1, state1.Failures)
        | _ -> Assert.Fail("Expected NextAttempt with Side A and Failures 1")

    [<Fact>]
    let ``Fallback_sequence_outcome_2_switch_B`` () =
        let state1 = { Side = ModelSide.A; Failures = 1 }

        let decision2 = Fallback.nextAttempt state1

        match decision2 with
        | FallbackDecision.NextAttempt state2 ->
            Assert.Equal(ModelSide.B, state2.Side)
            Assert.Equal(0, state2.Failures)
        | _ -> Assert.Fail("Expected NextAttempt with Side B and Failures 0")

    [<Fact>]
    let ``Fallback_sequence_outcome_3_B_retry`` () =
        let state2 = { Side = ModelSide.B; Failures = 0 }

        let decision3 = Fallback.nextAttempt state2

        match decision3 with
        | FallbackDecision.NextAttempt state3 ->
            Assert.Equal(ModelSide.B, state3.Side)
            Assert.Equal(1, state3.Failures)
        | _ -> Assert.Fail("Expected NextAttempt with Side B and Failures 1")

    [<Fact>]
    let ``Fallback_sequence_outcome_4_Dead`` () =
        let state3 = { Side = ModelSide.B; Failures = 1 }

        let decision4 = Fallback.nextAttempt state3
        Assert.Equal(FallbackDecision.Dead, decision4)

    [<Fact>]
    let ``Fallback_AcceptanceUnknown_reconciles_without_side_switch`` () =
        let stateA = { Side = ModelSide.A; Failures = 0 }
        let decisionA = Fallback.handleAcceptanceUnknown stateA
        Assert.Equal(FallbackDecision.Reconcile stateA, decisionA)

        let stateB = { Side = ModelSide.B; Failures = 1 }
        let decisionB = Fallback.handleAcceptanceUnknown stateB
        Assert.Equal(FallbackDecision.Reconcile stateB, decisionB)
