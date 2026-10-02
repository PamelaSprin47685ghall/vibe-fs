namespace ContextCompressionConsumers

open Wanxiangshu.Context.Companion.Blogger

module DirectSquashConsumer =
    let construct (input: BloggerSquashRequestInput) : BloggerSquashRequestContext =
        { StoredRequestId = input.RequestId
          StoredMainSessionId = input.MainSessionId
          StoredBloggerSessionId = input.BloggerSessionId
          StoredFrameEpochId = input.FrameEpochId
          StoredCoveredFrameCount = input.CoveredFrameCount
          StoredFrameDigests = input.FrameDigests
          StoredObservedPrefixEpochId = input.ObservedPrefixEpochId }
