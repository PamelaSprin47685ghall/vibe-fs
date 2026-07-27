namespace Wanxiangshu.Next.Tests

// Durable review behavior is covered by Session/ReviewerHostTests.fs and
// Review/GuardDurableTests.fs. The former local counter model was removed:
// it could not prove provider-run or confirmation-prompt causality.
module ReviewGuardTests =
    let removedLegacyCounterModel = ()
