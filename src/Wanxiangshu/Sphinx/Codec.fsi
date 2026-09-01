namespace Wanxiangshu.Sphinx

module Codec =
    val decodeObservation: raw: obj -> Result<Observation, string>
    val requestObject: request: Request -> obj
    val answerObject: answer: CanonicalAnswer -> obj
