namespace Wanxiangshu.Context.Companion.Blogger

open System
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host

/// ENFORCER-045/050/051: typed material for one Blogger provider request.
///
/// Staged and consumed as a whole. Coverage advance on cycle commit reads this
/// context — never re-derives from the latest XTrace (fail closed if missing).
///
/// C5: RequestId + ObservedPrefixEpochId are frozen at materialization. Commit
/// must use the frozen epoch, not the live PrefixEpoch at tool-return time.
///
/// Why a request context was refused at construction or recovery decode.

/// Typed so recovery can distinguish corrupt, version-incompatible and
/// unreadable durable input instead of collapsing them to None (W6).
[<RequireQualifiedAccess>]
type BloggerRequestRejection =

    | CoverageDidNotAdvance of previous: int64 * next: int64
    /// The stored delta digest disagrees with SHA256 of the carried TOML.
    | DeltaDigestMismatch of expected: string * actual: string
    /// An epoch bound to the request is not a legal generation.
    | EpochNotCoherent of field: string * value: int64
    /// Squash must cover at least one frame.
    | EmptySquashCoverage
    /// The declared covered frame count disagrees with the carried digests.
    | SquashCoverageMismatch of declared: int * carried: int

/// Unverified construction inputs. Assembled freely at decode boundaries;
/// only `BloggerRequestMaterial.createMain/createSquash` may turn them into
/// a `BloggerRequestContext`.
type BloggerMainRequestInput =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
      Items: BloggerDeltaItem list
      Toml: string
      PreviousIngestedThroughSequence: int64
      NextIngestedThroughSequence: int64
      PreviousCoverableTurnCutoffExclusive: int
      NextCoverableTurnCutoffExclusive: int
      NextCoveredPrefixDigest: string
      FrameEpochId: FrameEpochId
      DeltaDigest: BlobDigest
      ObservedPrefixEpochId: PrefixEpochId }

type BloggerSquashRequestInput =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
      FrameEpochId: FrameEpochId
      CoveredFrameCount: int
      FrameDigests: BlobDigest list
      ObservedPrefixEpochId: PrefixEpochId }

type BloggerMainRequestContext =
    private
        { StoredRequestId: BloggerRequestId
          StoredMainSessionId: SessionId
          StoredBloggerSessionId: SessionId
          StoredItems: BloggerDeltaItem list
          StoredToml: string
          StoredPreviousIngestedThroughSequence: int64
          StoredNextIngestedThroughSequence: int64
          StoredPreviousCoverableTurnCutoffExclusive: int
          StoredNextCoverableTurnCutoffExclusive: int
          StoredNextCoveredPrefixDigest: string
          StoredFrameEpochId: FrameEpochId
          StoredDeltaDigest: BlobDigest
          StoredObservedPrefixEpochId: PrefixEpochId }

    member this.RequestId = this.StoredRequestId
    member this.MainSessionId = this.StoredMainSessionId
    member this.BloggerSessionId = this.StoredBloggerSessionId
    member this.Items = this.StoredItems
    member this.Toml = this.StoredToml
    member this.PreviousIngestedThroughSequence = this.StoredPreviousIngestedThroughSequence
    member this.NextIngestedThroughSequence = this.StoredNextIngestedThroughSequence
    member this.PreviousCoverableTurnCutoffExclusive = this.StoredPreviousCoverableTurnCutoffExclusive
    member this.NextCoverableTurnCutoffExclusive = this.StoredNextCoverableTurnCutoffExclusive
    member this.NextCoveredPrefixDigest = this.StoredNextCoveredPrefixDigest
    member this.FrameEpochId = this.StoredFrameEpochId
    member this.DeltaDigest = this.StoredDeltaDigest
    member this.ObservedPrefixEpochId = this.StoredObservedPrefixEpochId

type BloggerSquashRequestContext =
    private
        { StoredRequestId: BloggerRequestId
          StoredMainSessionId: SessionId
          StoredBloggerSessionId: SessionId
          StoredFrameEpochId: FrameEpochId
          StoredCoveredFrameCount: int
          StoredFrameDigests: BlobDigest list
          StoredObservedPrefixEpochId: PrefixEpochId }

    member this.RequestId = this.StoredRequestId
    member this.MainSessionId = this.StoredMainSessionId
    member this.BloggerSessionId = this.StoredBloggerSessionId
    member this.FrameEpochId = this.StoredFrameEpochId
    member this.CoveredFrameCount = this.StoredCoveredFrameCount
    member this.FrameDigests = this.StoredFrameDigests
    member this.ObservedPrefixEpochId = this.StoredObservedPrefixEpochId

[<RequireQualifiedAccess>]
type BloggerRequestContext =
    | Main of BloggerMainRequestContext
    | Squash of BloggerSquashRequestContext

[<RequireQualifiedAccess>]
type BloggerTerminalRequestOwnership =
    | Current
    | Superseded
    | Unproven

type BloggerTerminalParentEvidence =
    { PromptKey: PromptKey
      IsRequestScopedRepair: bool }

[<RequireQualifiedAccess>]
module BloggerRequestOwnership =

    let decide
        (currentRequestId: BloggerRequestId)
        (durableOpenRequestId: BloggerRequestId option)
        (durableOpenPromptKey: PromptKey option)
        (parent: BloggerTerminalParentEvidence option)
        : BloggerTerminalRequestOwnership =
        match durableOpenRequestId, parent with
        | Some openRequestId, _ when openRequestId <> currentRequestId -> BloggerTerminalRequestOwnership.Superseded
        | None, _
        | Some _, None -> BloggerTerminalRequestOwnership.Unproven
        | Some _, Some evidence when durableOpenPromptKey = Some evidence.PromptKey ->
            BloggerTerminalRequestOwnership.Current
        | Some _, Some evidence when evidence.IsRequestScopedRepair -> BloggerTerminalRequestOwnership.Current
        | Some _, Some _ -> BloggerTerminalRequestOwnership.Superseded

[<RequireQualifiedAccess>]
module BloggerRequestContext =

    /// Canonical Main request identity: sessions + kind + content digest +
    /// coverage window. Computed by the owner at materialization and frozen
    /// into the durable open record; recovery trusts the durable value and
    /// never recomputes it from blob content.
    let mainRequestId
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (deltaDigest: BlobDigest)
        (previousSeq: int64)
        (nextSeq: int64)
        : BloggerRequestId =
        BloggerRequestId.create (
            HostDigest.sha256Hex (
                String.concat
                    "|"
                    [ SessionId.value mainSessionId
                      SessionId.value bloggerSessionId
                      "main"
                      BlobDigest.value deltaDigest
                      string previousSeq
                      string nextSeq ]
            )
        )

    /// Canonical Squash request identity: sessions + kind + epoch + covered
    /// count + covered digests. Same frozen-identity rule as `mainRequestId`.
    let squashRequestId
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (frameEpoch: FrameEpochId)
        (coveredFrameCount: int)
        (frameDigests: BlobDigest list)
        : BloggerRequestId =
        BloggerRequestId.create (
            HostDigest.sha256Hex (
                String.concat
                    "|"
                    [ SessionId.value mainSessionId
                      SessionId.value bloggerSessionId
                      "squash"
                      string (FrameEpochId.value frameEpoch)
                      string coveredFrameCount
                      (frameDigests |> List.map BlobDigest.value |> String.concat ",") ]
            )
        )

    let toml (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> Some main.Toml
        | BloggerRequestContext.Squash _ -> None

    let isMain (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main _ -> true
        | BloggerRequestContext.Squash _ -> false

    let requestId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.RequestId
        | BloggerRequestContext.Squash squash -> squash.RequestId

    let observedPrefixEpoch (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.ObservedPrefixEpochId
        | BloggerRequestContext.Squash squash -> squash.ObservedPrefixEpochId

    let mainSessionId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.MainSessionId
        | BloggerRequestContext.Squash squash -> squash.MainSessionId

    let bloggerSessionId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.BloggerSessionId
        | BloggerRequestContext.Squash squash -> squash.BloggerSessionId

    let frameEpochId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.FrameEpochId
        | BloggerRequestContext.Squash squash -> squash.FrameEpochId

/// The single owner of `BloggerRequestContext` construction (W6). External
/// code reads fields through the context members; it cannot assemble a
/// `Main`/`Squash` record with arbitrary
/// Previous/Next/Cutoff/Digest/Epoch combos. Both the live derivation
/// (`BloggerMainContext.mainContextFromChunk`,
/// `CompanionHostBlogger.tryBuildSquashContext`) and the durable recovery
/// decode (`EnforcerFrameRecovery`) construct through this module.
[<RequireQualifiedAccess>]
module BloggerRequestMaterial =

    let private checkEpoch (field: string) (epoch: int64) =
        if epoch < 0L then
            Error(BloggerRequestRejection.EpochNotCoherent(field, epoch))
        else
            Ok()

    /// Validate one Main candidate. Enforces the §4.2 local proof
    /// invariants: strict coverage advance and an exact
    /// `DeltaDigest = SHA256(Toml)` binding, plus coherent epochs. The
    /// request id is the owner's frozen materialization identity (durable
    /// open record); it is carried, never recomputed from blob content.
    /// Failure is a typed rejection, never a partial record.
    let createMain (input: BloggerMainRequestInput) : Result<BloggerMainRequestContext, BloggerRequestRejection> =

        if input.NextIngestedThroughSequence <= input.PreviousIngestedThroughSequence then
            Error(
                BloggerRequestRejection.CoverageDidNotAdvance(
                    input.PreviousIngestedThroughSequence,
                    input.NextIngestedThroughSequence
                )
            )

        else
            let expectedDigest = HostDigest.sha256Hex input.Toml
            let actualDigest = BlobDigest.value input.DeltaDigest

            if actualDigest <> expectedDigest then
                Error(BloggerRequestRejection.DeltaDigestMismatch(expectedDigest, actualDigest))
            else
                match
                    checkEpoch "FrameEpochId" (FrameEpochId.value input.FrameEpochId),
                    checkEpoch "ObservedPrefixEpochId" (PrefixEpochId.value input.ObservedPrefixEpochId)
                with
                | Error rejection, _
                | _, Error rejection -> Error rejection
                | Ok(), Ok() ->
                    Ok
                        { StoredRequestId = input.RequestId
                          StoredMainSessionId = input.MainSessionId
                          StoredBloggerSessionId = input.BloggerSessionId
                          StoredItems = input.Items
                          StoredToml = input.Toml
                          StoredPreviousIngestedThroughSequence = input.PreviousIngestedThroughSequence
                          StoredNextIngestedThroughSequence = input.NextIngestedThroughSequence
                          StoredPreviousCoverableTurnCutoffExclusive =
                            input.PreviousCoverableTurnCutoffExclusive
                          StoredNextCoverableTurnCutoffExclusive = input.NextCoverableTurnCutoffExclusive
                          StoredNextCoveredPrefixDigest = input.NextCoveredPrefixDigest
                          StoredFrameEpochId = input.FrameEpochId
                          StoredDeltaDigest = input.DeltaDigest
                          StoredObservedPrefixEpochId = input.ObservedPrefixEpochId }

    /// Validate one Squash candidate. Squash carries no ingest window or
    /// delta digest; its checks are a non-empty covered count agreeing with
    /// the carried digests, plus coherent epochs. The request id is the
    /// owner's frozen identity, carried like Main.
    let createSquash
        (input: BloggerSquashRequestInput)
        : Result<BloggerSquashRequestContext, BloggerRequestRejection> =
        if input.CoveredFrameCount < 1 then
            Error BloggerRequestRejection.EmptySquashCoverage
        elif List.length input.FrameDigests <> input.CoveredFrameCount then
            Error(
                BloggerRequestRejection.SquashCoverageMismatch(input.CoveredFrameCount, List.length input.FrameDigests)
            )
        else
            match
                checkEpoch "FrameEpochId" (FrameEpochId.value input.FrameEpochId),
                checkEpoch "ObservedPrefixEpochId" (PrefixEpochId.value input.ObservedPrefixEpochId)
            with
            | Error rejection, _
            | _, Error rejection -> Error rejection
            | Ok(), Ok() ->
                Ok
                    { StoredRequestId = input.RequestId
                      StoredMainSessionId = input.MainSessionId
                      StoredBloggerSessionId = input.BloggerSessionId
                      StoredFrameEpochId = input.FrameEpochId
                      StoredCoveredFrameCount = input.CoveredFrameCount
                      StoredFrameDigests = input.FrameDigests
                      StoredObservedPrefixEpochId = input.ObservedPrefixEpochId }
