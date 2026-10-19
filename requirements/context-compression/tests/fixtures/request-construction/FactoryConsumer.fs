namespace ContextCompressionConsumers

open Wanxiangshu.Context.Companion.Blogger

module FactoryConsumer =
    let main (input: BloggerMainRequestInput) : Result<BloggerMainRequestContext, BloggerRequestRejection> =
        BloggerRequestMaterial.createMain input

    let readMain (value: BloggerMainRequestContext) =
        value.RequestId,
        value.MainSessionId,
        value.BloggerSessionId,
        value.Items,
        value.Toml,
        value.PreviousIngestedThroughSequence,
        value.NextIngestedThroughSequence,
        value.PreviousCoverableTurnCutoffExclusive,
        value.NextCoverableTurnCutoffExclusive,
        value.NextCoveredPrefixDigest,
        value.FrameEpochId,
        value.DeltaDigest,
        value.ObservedPrefixEpochId

    let classify context =
        match context with
        | BloggerRequestContext.Main value -> value.RequestId
