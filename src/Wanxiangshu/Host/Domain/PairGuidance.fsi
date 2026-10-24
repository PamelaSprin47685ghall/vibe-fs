// HNI P-16 (domain seam): the pair-guidance transcript; tier B, never
// mirrored to the TypeScript port surface.
//
// The occurrence ledger is product state, not a host fact: it lives in
// `Wanxiangshu.Interaction.PairGuidance` (`Interaction/PairGuidance/Occurrences.fs(i)`)
// together with the product rule that owns the commit order.
namespace Wanxiangshu.Host.Domain

open Wanxiangshu.Foundation.Identity

/// HOST-013: the outgoing transcript as the pair-guidance owner reads it.
/// A host adapter decodes its own wire into this record; it decides nothing.
type PairGuidanceTranscript =
    {
        /// Gaps around the newest real message when the host can render
        /// guidance on it; `Ok None` when that message carries no guidance;
        /// `Error` when the carrier has no transcript address.
        Carrier: Result<(TranscriptGap * TranscriptGap) option, unit>
        /// Call ids of the synthetic guidance messages found on the transcript.
        SyntheticCallIds: string list
        /// Some message on the transcript is authored by an internal role.
        InternalAuthor: bool
    }
