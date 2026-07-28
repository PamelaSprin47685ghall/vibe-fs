namespace Wanxiangshu.Next.Tests.Session

open Xunit
open Wanxiangshu.Next.Session

module FallbackContractTests =

    [<Fact>]
    let ``nextAttempt from initial yields Offset 1 side A`` () =
        match Fallback.nextAttempt Fallback.initial with
        | FallbackDecision.NextAttempt state ->
            Assert.equal (1uy, state.Offset)
            Assert.equal (ModelSide.A, Fallback.currentSide state)

    [<Fact>]
    let ``nextAttempt from Offset 1 yields Offset 2 side B`` () =
        match Fallback.nextAttempt { Offset = 1uy } with
        | FallbackDecision.NextAttempt state ->
            Assert.equal (2uy, state.Offset)
            Assert.equal (ModelSide.B, Fallback.currentSide state)

    [<Fact>]
    let ``nextAttempt from Offset 3 wraps to Offset 0 side A never Dead`` () =
        match Fallback.nextAttempt { Offset = 3uy } with
        | FallbackDecision.NextAttempt state ->
            Assert.equal (0uy, state.Offset)
            Assert.equal (ModelSide.A, Fallback.currentSide state)

    [<Fact>]
    let ``cycle of 12 advances from Offset 0 is A A B B repeated`` () =
        let expected =
            [ ModelSide.A
              ModelSide.A
              ModelSide.B
              ModelSide.B
              ModelSide.A
              ModelSide.A
              ModelSide.B
              ModelSide.B
              ModelSide.A
              ModelSide.A
              ModelSide.B
              ModelSide.B ]

        let mutable state = Fallback.initial

        for expectedSide in expected do
            Assert.equal (expectedSide, Fallback.currentSide state)

            match Fallback.nextAttempt state with
            | FallbackDecision.NextAttempt next -> state <- next
