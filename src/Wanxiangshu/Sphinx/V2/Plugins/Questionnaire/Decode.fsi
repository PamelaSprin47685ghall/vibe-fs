namespace Wanxiangshu.Sphinx.V2.Plugins

type DecodeError = { Code: string; Message: string }

module Decode =
    /// Strict: an unknown judgment, a missing field or a non-array shape is refused.
    val decodePairwise: string -> obj -> Result<PairwiseResponse, DecodeError>
    val decodeRanking: obj -> Result<RankingResponse, DecodeError>
    val decodeMaxDiff: obj -> Result<MaxDiffObservation, DecodeError>
