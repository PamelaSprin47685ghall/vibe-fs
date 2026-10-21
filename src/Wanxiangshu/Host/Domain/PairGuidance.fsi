// HNI P-16 (domain seam): the pair-guidance transcript and ledger; tier B,
// never mirrored to the TypeScript port surface.
namespace Wanxiangshu.Host.Domain

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HOST-013: one pair-guidance occurrence, what it said and where it sits
/// on the transcript.
type PairGuidanceOccurrence =
    { Ordinal: int64
      CallId: string
      MarkerText: string
      CallGap: TranscriptGap
      ResultGap: TranscriptGap }

/// HOST-013: the outgoing transcript as the pair-guidance owner reads it.
/// A host adapter decodes its own wire into this record; it decides nothing.
type PairGuidanceTranscript =
    {
        /// Gaps around the newest real message when the host can render
        /// guidance on it; `Ok None` when that message carries no guidance;
        /// `Error` when the carrier has no transcript address.
        Carrier: Result<(TranscriptGap * TranscriptGap) option, string>
        /// Call ids of the synthetic guidance messages found on the transcript.
        SyntheticCallIds: string list
        /// Some message on the transcript is authored by an internal role.
        InternalAuthor: bool
    }

/// HOST-013: the occurrence ledger of one transcript.
type PairGuidanceLedger =
    { History: PairGuidanceOccurrence list
      Visible: PairGuidanceOccurrence list
      Append: PairGuidanceOccurrence -> Task<Result<unit, string>> }
