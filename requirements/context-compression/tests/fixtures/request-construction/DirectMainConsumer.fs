namespace ContextCompressionConsumers

open Wanxiangshu.Context.Companion.Blogger

module DirectMainConsumer =
    let construct (input: BloggerMainRequestInput) : BloggerMainRequestContext =
        { StoredRequestId = input.RequestId
          StoredMainSessionId = input.MainSessionId
          StoredBloggerSessionId = input.BloggerSessionId
          StoredItems = input.Items
          StoredToml = input.Toml
          StoredPreviousIngestedThroughSequence = input.PreviousIngestedThroughSequence
          StoredNextIngestedThroughSequence = input.NextIngestedThroughSequence
          StoredPreviousCoverableTurnCutoffExclusive = input.PreviousCoverableTurnCutoffExclusive
          StoredNextCoverableTurnCutoffExclusive = input.NextCoverableTurnCutoffExclusive
          StoredNextCoveredPrefixDigest = input.NextCoveredPrefixDigest
          StoredFrameEpochId = input.FrameEpochId
          StoredDeltaDigest = input.DeltaDigest
          StoredObservedPrefixEpochId = input.ObservedPrefixEpochId }
