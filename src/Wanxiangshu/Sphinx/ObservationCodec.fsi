namespace Wanxiangshu.Sphinx

module ObservationCodec =
    val decodeSemanticAssessment: raw: obj -> Result<Observation, string>
    val decodeCandidates: raw: obj -> Result<Observation, string>
    val decodeInvestigation: raw: obj -> Result<Observation, string>
    val decodeSynthesis: raw: obj -> Result<Observation, string>
    val decode: raw: obj -> Result<Observation, string>
