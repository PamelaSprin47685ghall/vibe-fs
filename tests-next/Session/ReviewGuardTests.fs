namespace Wanxiangshu.Next.Tests

open Xunit
open Wanxiangshu.Next.Session


module private LegacyReviewGuard =
    type State =
        { LastGitTreeHash: string option
          ConsecutivePerfects: int }

    let empty: State =
        { LastGitTreeHash = None
          ConsecutivePerfects = 0 }

    let invalidate (state: State) : State =
        { LastGitTreeHash = None
          ConsecutivePerfects = 0 }

    let recordVerdict (verdict: ReviewVerdict) (gitTreeHash: string) (state: State) : State =
        match state.LastGitTreeHash with
        | Some lastHash when lastHash = gitTreeHash ->
            match verdict with
            | ReviewVerdict.Perfect ->
                { state with ConsecutivePerfects = state.ConsecutivePerfects + 1 }
            | ReviewVerdict.Revise ->
                { LastGitTreeHash = Some gitTreeHash
                  ConsecutivePerfects = 0 }
        | _ ->
            match verdict with
            | ReviewVerdict.Perfect ->
                { LastGitTreeHash = Some gitTreeHash
                  ConsecutivePerfects = 1 }
            | ReviewVerdict.Revise ->
                { LastGitTreeHash = Some gitTreeHash
                  ConsecutivePerfects = 0 }

    let tryFinish (state: State) : ReviewFinishResult =
        if state.ConsecutivePerfects >= 2 then
            ReviewFinishResult.Confirmed
        else
            ReviewFinishResult.NeedsReview


module ReviewGuardTests =

    [<Fact>]
    let ``Double_perfect_same_hash_confirmation`` () =
        let s0 = LegacyReviewGuard.empty
        Assert.Equal(ReviewFinishResult.NeedsReview, LegacyReviewGuard.tryFinish s0)

        let s1 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s0
        Assert.Equal(ReviewFinishResult.NeedsReview, LegacyReviewGuard.tryFinish s1)
        Assert.Equal(1, s1.ConsecutivePerfects)
        Assert.Equal(Some "hash123", s1.LastGitTreeHash)

        let s2 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s1
        Assert.Equal(ReviewFinishResult.Confirmed, LegacyReviewGuard.tryFinish s2)
        Assert.Equal(2, s2.ConsecutivePerfects)
        Assert.Equal(Some "hash123", s2.LastGitTreeHash)

    [<Fact>]
    let ``Revise_verdict_invalidates_consecutive_perfects`` () =
        let s0 = LegacyReviewGuard.empty
        let s1 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s0
        let s2 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s1
        Assert.Equal(ReviewFinishResult.Confirmed, LegacyReviewGuard.tryFinish s2)

        let s3 = LegacyReviewGuard.recordVerdict ReviewVerdict.Revise "hash123" s2
        Assert.Equal(ReviewFinishResult.NeedsReview, LegacyReviewGuard.tryFinish s3)
        Assert.Equal(0, s3.ConsecutivePerfects)
        Assert.Equal(Some "hash123", s3.LastGitTreeHash)

    [<Fact>]
    let ``Hash_change_invalidates_consecutive_perfects`` () =
        let s0 = LegacyReviewGuard.empty
        let s1 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s0
        let s2 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s1
        Assert.Equal(ReviewFinishResult.Confirmed, LegacyReviewGuard.tryFinish s2)

        // New verdict with a different hash invalidates previous confirmed count
        let s3 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash456" s2
        Assert.Equal(ReviewFinishResult.NeedsReview, LegacyReviewGuard.tryFinish s3)
        Assert.Equal(1, s3.ConsecutivePerfects)
        Assert.Equal(Some "hash456", s3.LastGitTreeHash)

    [<Fact>]
    let ``Explicit_invalidation_resets_state`` () =
        let s0 = LegacyReviewGuard.empty
        let s1 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s0
        let s2 = LegacyReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s1
        Assert.Equal(ReviewFinishResult.Confirmed, LegacyReviewGuard.tryFinish s2)

        let s3 = LegacyReviewGuard.invalidate s2
        Assert.Equal(ReviewFinishResult.NeedsReview, LegacyReviewGuard.tryFinish s3)
        Assert.Equal(0, s3.ConsecutivePerfects)
        Assert.Equal(None, s3.LastGitTreeHash)
