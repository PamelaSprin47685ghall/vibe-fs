// HNI P-16 (domain seam): the pair-guidance transcript; tier B, never
// mirrored to the TypeScript port surface.
//
// The occurrence ledger is product state, not a host fact: it lives in
// `Wanxiangshu.Interaction.PairGuidance` (`Interaction/PairGuidance/Occurrences.fs(i)`)
// together with the product rule that owns the commit order.
namespace Wanxiangshu.Host.Domain

open Wanxiangshu.Foundation.Identity

type PairGuidanceTranscript =
    { Carrier: Result<(TranscriptGap * TranscriptGap) option, unit>
      SyntheticCallIds: string list
      InternalAuthor: bool }
