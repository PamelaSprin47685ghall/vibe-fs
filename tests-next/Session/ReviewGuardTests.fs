namespace Wanxiangshu.Next.Tests

open Xunit
open Wanxiangshu.Next.Session

module ReviewGuardTests =

    [<Fact>]
    let ``Double_perfect_same_hash_confirmation`` () =
        let s0 = ReviewGuard.empty
        Assert.Equal(ReviewFinishResult.NeedsReview, ReviewGuard.tryFinish s0)

        let s1 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s0
        Assert.Equal(ReviewFinishResult.NeedsReview, ReviewGuard.tryFinish s1)
        Assert.Equal(1, s1.ConsecutivePerfects)
        Assert.Equal(Some "hash123", s1.LastGitTreeHash)

        let s2 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s1
        Assert.Equal(ReviewFinishResult.Confirmed, ReviewGuard.tryFinish s2)
        Assert.Equal(2, s2.ConsecutivePerfects)
        Assert.Equal(Some "hash123", s2.LastGitTreeHash)

    [<Fact>]
    let ``Revise_verdict_invalidates_consecutive_perfects`` () =
        let s0 = ReviewGuard.empty
        let s1 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s0
        let s2 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s1
        Assert.Equal(ReviewFinishResult.Confirmed, ReviewGuard.tryFinish s2)

        let s3 = ReviewGuard.recordVerdict ReviewVerdict.Revise "hash123" s2
        Assert.Equal(ReviewFinishResult.NeedsReview, ReviewGuard.tryFinish s3)
        Assert.Equal(0, s3.ConsecutivePerfects)
        Assert.Equal(Some "hash123", s3.LastGitTreeHash)

    [<Fact>]
    let ``Hash_change_invalidates_consecutive_perfects`` () =
        let s0 = ReviewGuard.empty
        let s1 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s0
        let s2 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s1
        Assert.Equal(ReviewFinishResult.Confirmed, ReviewGuard.tryFinish s2)

        // New verdict with a different hash invalidates previous confirmed count
        let s3 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash456" s2
        Assert.Equal(ReviewFinishResult.NeedsReview, ReviewGuard.tryFinish s3)
        Assert.Equal(1, s3.ConsecutivePerfects)
        Assert.Equal(Some "hash456", s3.LastGitTreeHash)

    [<Fact>]
    let ``Explicit_invalidation_resets_state`` () =
        let s0 = ReviewGuard.empty
        let s1 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s0
        let s2 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "hash123" s1
        Assert.Equal(ReviewFinishResult.Confirmed, ReviewGuard.tryFinish s2)

        let s3 = ReviewGuard.invalidate s2
        Assert.Equal(ReviewFinishResult.NeedsReview, ReviewGuard.tryFinish s3)
        Assert.Equal(0, s3.ConsecutivePerfects)
        Assert.Equal(None, s3.LastGitTreeHash)
