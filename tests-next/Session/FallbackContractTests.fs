namespace Wanxiangshu.Next.Tests.Session

open Xunit
open Wanxiangshu.Next.Session

module FallbackContractTests =

    [<Fact>]
    let ``Fallback starts on A and retries before switching`` () =
        match Fallback.nextAttempt Fallback.initial with
        | FallbackDecision.NextAttempt state ->
            Assert.Equal(ModelSide.A, state.Side)
            Assert.Equal(1, state.Failures)
        | decision -> Assert.Fail(sprintf "Expected A retry, got %A" decision)

    [<Fact>]
    let ``Fallback switches permanently after second A failure`` () =
        match Fallback.nextAttempt { Side = ModelSide.A; Failures = 1 } with
        | FallbackDecision.NextAttempt state ->
            Assert.Equal(ModelSide.B, state.Side)
            Assert.Equal(2, state.Failures)
        | decision -> Assert.Fail(sprintf "Expected B fallback, got %A" decision)

    [<Fact>]
    let ``Fallback dies after two B failures`` () =
        Assert.Equal(FallbackDecision.Dead, Fallback.nextAttempt { Side = ModelSide.B; Failures = 3 })
