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

/// Unverified construction inputs. Assembled freely at decode boundaries;
/// only `BloggerRequestMaterial.createMain` may turn them into
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

    member this.PreviousIngestedThroughSequence =
        this.StoredPreviousIngestedThroughSequence

    member this.NextIngestedThroughSequence = this.StoredNextIngestedThroughSequence

    member this.PreviousCoverableTurnCutoffExclusive =
        this.StoredPreviousCoverableTurnCutoffExclusive

    member this.NextCoverableTurnCutoffExclusive =
        this.StoredNextCoverableTurnCutoffExclusive

    member this.NextCoveredPrefixDigest = this.StoredNextCoveredPrefixDigest
    member this.FrameEpochId = this.StoredFrameEpochId
    member this.DeltaDigest = this.StoredDeltaDigest
    member this.ObservedPrefixEpochId = this.StoredObservedPrefixEpochId

[<RequireQualifiedAccess>]
type BloggerRequestContext =
    | Main of BloggerMainRequestContext

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

    /// context-compression-024: a terminal belongs to the current request only
    /// when durable evidence proves it. `Unproven` is not a weaker `Current`:
    /// no durable open request, no exact landing for the physical message, or
    /// an open request not yet bound to its dispatch prove nothing either way.
    let decide
        (currentRequestId: BloggerRequestId)
        (durableOpenRequestId: BloggerRequestId option)
        (durableOpenPromptKey: PromptKey option)
        (parent: BloggerTerminalParentEvidence option)
        : BloggerTerminalRequestOwnership =
        match durableOpenRequestId, durableOpenPromptKey, parent with
        | Some openRequestId, _, _ when openRequestId <> currentRequestId -> BloggerTerminalRequestOwnership.Superseded
        | None, _, _
        | Some _, _, None -> BloggerTerminalRequestOwnership.Unproven
        | Some _, _, Some evidence when evidence.IsRequestScopedRepair -> BloggerTerminalRequestOwnership.Current
        | Some _, Some openPromptKey, Some evidence when openPromptKey = evidence.PromptKey ->
            BloggerTerminalRequestOwnership.Current
        | Some _, None, Some _ -> BloggerTerminalRequestOwnership.Unproven
        | Some _, Some _, Some _ -> BloggerTerminalRequestOwnership.Superseded

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

    let toml (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> Some main.Toml

    let isMain (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main _ -> true

    let requestId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.RequestId

    let observedPrefixEpoch (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.ObservedPrefixEpochId

    let mainSessionId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.MainSessionId

    let bloggerSessionId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.BloggerSessionId

    let frameEpochId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.FrameEpochId

/// The single owner of `BloggerRequestContext` construction (W6). External
/// code reads fields through the context members; it cannot assemble a
/// `Main` record with arbitrary Previous/Next/Cutoff/Digest/Epoch combos.
/// Both the live derivation (`BloggerMainContext.mainContextFromChunk`) and the durable recovery
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
    /// Finalization arm (declared first so the outer check stays a single
    /// `if` pyramid level under fantomas + the nesting lint).
    let private finalizeMain (input: BloggerMainRequestInput) =
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
                  StoredPreviousCoverableTurnCutoffExclusive = input.PreviousCoverableTurnCutoffExclusive
                  StoredNextCoverableTurnCutoffExclusive = input.NextCoverableTurnCutoffExclusive
                  StoredNextCoveredPrefixDigest = input.NextCoveredPrefixDigest
                  StoredFrameEpochId = input.FrameEpochId
                  StoredDeltaDigest = input.DeltaDigest
                  StoredObservedPrefixEpochId = input.ObservedPrefixEpochId }

    let createMain (input: BloggerMainRequestInput) : Result<BloggerMainRequestContext, BloggerRequestRejection> =

        if input.NextIngestedThroughSequence <= input.PreviousIngestedThroughSequence then
            Error(
                BloggerRequestRejection.CoverageDidNotAdvance(
                    input.PreviousIngestedThroughSequence,
                    input.NextIngestedThroughSequence
                )
            )

        elif BlobDigest.value input.DeltaDigest <> HostDigest.sha256Hex input.Toml then
            Error(
                BloggerRequestRejection.DeltaDigestMismatch(
                    HostDigest.sha256Hex input.Toml,
                    BlobDigest.value input.DeltaDigest
                )
            )

        else
            finalizeMain input
