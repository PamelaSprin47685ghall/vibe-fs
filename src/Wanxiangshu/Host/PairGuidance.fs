namespace Wanxiangshu.Host

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type PairGuidanceOccurrence =
    { Ordinal: int64
      CallId: string
      MarkerText: string
      CallGap: TranscriptGap
      ResultGap: TranscriptGap }

type PairGuidanceTranscript =
    { Carrier: Result<(TranscriptGap * TranscriptGap) option, string>
      SyntheticCallIds: string list
      InternalAuthor: bool }

type PairGuidanceLedger =
    { History: PairGuidanceOccurrence list
      Visible: PairGuidanceOccurrence list
      Append: PairGuidanceOccurrence -> Task<Result<unit, string>> }
