namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

type BloggerCycleReceipt =
    { ProviderRun: ProviderRunIdentity
      Kind: BlogFrameKind
      RequestId: BloggerRequestId }

type OpenBloggerRequest =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
      RequestKind: string
      ContextRef: BlobRef
      ContextDigest: BlobDigest
      ObservedPrefixEpochId: PrefixEpochId
      PreviousIngestedThroughSequence: int64
      NextIngestedThroughSequence: int64
      FrameEpochId: FrameEpochId
      SelectedFrameDigests: BlobDigest list
      PromptKey: PromptKey option }

[<RequireQualifiedAccess>]
module OpenBloggerRequest =
    val providerRequestKind: request: OpenBloggerRequest -> Result<ProviderRequestKind, string>

type BloggerCycleProjectionState =
    { ByProviderRun: Map<ProviderRunIdentity, BloggerCycleReceipt>
      OpenByRequestId: Map<BloggerRequestId, OpenBloggerRequest>
      OpenByBlogger: Map<SessionId, BloggerRequestId>
      ProviderRunByRequestId: Map<BloggerRequestId, ProviderRunIdentity> }

module BloggerCycleProjection =
    val empty: BloggerCycleProjectionState
    val tryOpenByBlogger: bloggerId: SessionId -> state: BloggerCycleProjectionState -> OpenBloggerRequest option
    val tryReceipt: run: ProviderRunIdentity -> state: BloggerCycleProjectionState -> BloggerCycleReceipt option
    val materialize: openReq: OpenBloggerRequest -> state: BloggerCycleProjectionState -> Result<BloggerCycleProjectionState, string>
    val abandon: requestId: BloggerRequestId -> bloggerSessionId: SessionId -> state: BloggerCycleProjectionState -> BloggerCycleProjectionState
    val recordReceipt: receipt: BloggerCycleReceipt -> state: BloggerCycleProjectionState -> Result<BloggerCycleProjectionState, string>
