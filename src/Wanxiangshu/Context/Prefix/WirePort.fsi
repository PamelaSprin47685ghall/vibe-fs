namespace Wanxiangshu.Context.Prefix

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Participant.Provider.Projection

/// Domain-owned state view for one session needed by Wire.
type WireSessionState =
    {
        XTrace: XTraceProjectionState option
        Blog: BlogProjectionState option
        PrefixEpoch: ActivePrefixEpoch option
        /// context-compression-028: the committed phases this session still keeps raw.
        /// Empty means no phase has committed, so there is no window to fold.
        PhaseCommits: PhaseWindow.PhaseCommitWindow
    }

/// Single-snapshot view required by Wire for one call.
/// Derived from exactly one Journal Snapshot read per call.
type WireSnapshotView =
    {
        State: WireSessionState option
        IsCompanion: bool
        ActiveAuthorityProfile: PromptAuthority.AuthorityExecutionProfile option
        ProviderFailureState: ProviderFailureProjection option
        /// context-compression-028: the admission origin recorded for one physical
        /// request. A phase-boundary attempt plan must freeze the same provenance the
        /// request itself carries — guessing it would re-identify the prompt.
        AcceptedOrigin: PhysicalUserMessageId -> PromptAuthority.PromptOrigin option
    }

type WireBlobRecord =
    { BlobRef: BlobRef
      BlobDigest: BlobDigest }

type WirePrefixRebasePayload =
    {| SessionId: SessionId
       PreviousEpochId: PrefixEpochId
       NextEpochId: PrefixEpochId
       FrozenRecordPrefixRef: BlobRef
       FrozenRecordPrefixDigest: BlobDigest
       CutoffExclusive: int
       CoveredPrefixDigest: string
       SealRoot: string
       SyntheticMessageId: string
       ProbeId: string
       SolvingProviderRun: ProviderRunIdentity |}

type WireJournalPort =
    { ReadView: SessionId -> WireSnapshotView
      ReadBlob: BlobRef -> Task<Result<string, string>>
      WriteBlob: string -> Task<Result<WireBlobRecord, string>>
      CurrentProjection: XTraceProjectionState -> Task<Result<ProviderProjection.ProviderSemanticProjection, string>>
      RecordConfirmedSuccess: SessionId -> ProviderRunIdentity -> Task<Result<unit, string>>
      CommitPrefixRebase: SessionId -> ProviderRunIdentity -> WirePrefixRebasePayload -> Task<Result<unit, string>> }
