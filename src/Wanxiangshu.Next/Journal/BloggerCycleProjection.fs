namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity

/// C5: unified Entry|Squash receipt keyed by ProviderRun (item 12).
[<RequireQualifiedAccess>]
type BloggerCycleKind =
    | Entry
    | Squash

type BloggerCycleReceipt =
    { ProviderRun: ProviderRunIdentity
      Kind: BloggerCycleKind
      RequestId: BloggerRequestId }

/// Open request materialization (pre-send). O(1) by RequestId and by BloggerSession.
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

type BloggerCycleProjectionState =
    { /// ProviderRun → receipt (Entry or Squash). Duplicate kind or mixed kinds rejected.
      ByProviderRun: Map<ProviderRunIdentity, BloggerCycleReceipt>
      /// RequestId → open materialization (cleared on commit/abandon).
      OpenByRequestId: Map<BloggerRequestId, OpenBloggerRequest>
      /// BloggerSession → open RequestId (at most one open request per Blogger).
      OpenByBlogger: Map<SessionId, BloggerRequestId>
      /// RequestId → ProviderRun after first commit (reject rebinding).
      ProviderRunByRequestId: Map<BloggerRequestId, ProviderRunIdentity> }

module BloggerCycleProjection =

    let empty: BloggerCycleProjectionState =
        { ByProviderRun = Map.empty
          OpenByRequestId = Map.empty
          OpenByBlogger = Map.empty
          ProviderRunByRequestId = Map.empty }

    let tryOpenByBlogger (bloggerId: SessionId) (state: BloggerCycleProjectionState) =
        match Map.tryFind bloggerId state.OpenByBlogger with
        | None -> None
        | Some requestId -> Map.tryFind requestId state.OpenByRequestId

    let tryReceipt (run: ProviderRunIdentity) (state: BloggerCycleProjectionState) =
        Map.tryFind run state.ByProviderRun

    let materialize
        (openReq: OpenBloggerRequest)
        (state: BloggerCycleProjectionState)
        : Result<BloggerCycleProjectionState, string> =
        match Map.tryFind openReq.RequestId state.OpenByRequestId with
        | Some _ ->
            Error(
                sprintf
                    "BloggerRequestMaterialized already open for request %s"
                    (BloggerRequestId.value openReq.RequestId)
            )
        | None ->
            match Map.tryFind openReq.BloggerSessionId state.OpenByBlogger with
            | Some existing when existing <> openReq.RequestId ->
                Error(
                    sprintf
                        "Blogger session %s already has open request %s"
                        (SessionId.value openReq.BloggerSessionId)
                        (BloggerRequestId.value existing)
                )
            | _ ->
                Ok
                    { state with
                        OpenByRequestId = Map.add openReq.RequestId openReq state.OpenByRequestId
                        OpenByBlogger = Map.add openReq.BloggerSessionId openReq.RequestId state.OpenByBlogger }

    let abandon
        (requestId: BloggerRequestId)
        (bloggerSessionId: SessionId)
        (state: BloggerCycleProjectionState)
        : BloggerCycleProjectionState =
        let withoutRequest = Map.remove requestId state.OpenByRequestId

        let withoutBlogger =
            match Map.tryFind bloggerSessionId state.OpenByBlogger with
            | Some rid when rid = requestId -> Map.remove bloggerSessionId state.OpenByBlogger
            | _ -> state.OpenByBlogger

        { state with
            OpenByRequestId = withoutRequest
            OpenByBlogger = withoutBlogger }

    /// Record Entry or Squash receipt. Rejects:
    /// - same ProviderRun twice (any kind)
    /// - same ProviderRun Entry then Squash or reverse
    /// - same RequestId bound to a different ProviderRun
    let recordReceipt
        (receipt: BloggerCycleReceipt)
        (state: BloggerCycleProjectionState)
        : Result<BloggerCycleProjectionState, string> =
        match Map.tryFind receipt.ProviderRun state.ByProviderRun with
        | Some existing ->
            Error(
                sprintf
                    "ProviderRun %s already has %A receipt (request %s); cannot record %A (request %s)"
                    (ProviderRunIdentity.value receipt.ProviderRun)
                    existing.Kind
                    (BloggerRequestId.value existing.RequestId)
                    receipt.Kind
                    (BloggerRequestId.value receipt.RequestId)
            )
        | None ->
            match Map.tryFind receipt.RequestId state.ProviderRunByRequestId with
            | Some bound when bound <> receipt.ProviderRun ->
                Error(
                    sprintf
                        "RequestId %s already bound to ProviderRun %s; cannot rebind to %s"
                        (BloggerRequestId.value receipt.RequestId)
                        (ProviderRunIdentity.value bound)
                        (ProviderRunIdentity.value receipt.ProviderRun)
                )
            | _ ->
                let withoutRequest = Map.remove receipt.RequestId state.OpenByRequestId

                let withoutBlogger =
                    state.OpenByBlogger
                    |> Map.filter (fun _ rid -> rid <> receipt.RequestId)

                Ok
                    { state with
                        ByProviderRun = Map.add receipt.ProviderRun receipt state.ByProviderRun
                        ProviderRunByRequestId =
                            Map.add receipt.RequestId receipt.ProviderRun state.ProviderRunByRequestId
                        OpenByRequestId = withoutRequest
                        OpenByBlogger = withoutBlogger }
