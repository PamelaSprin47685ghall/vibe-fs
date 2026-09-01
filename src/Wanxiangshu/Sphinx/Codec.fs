namespace Wanxiangshu.Sphinx

module Codec =

    let decodeObservation (raw: obj) = ObservationCodec.decode raw

    let requestObject (request: Request) = WireEncode.requestObject request

    let answerObject (answer: CanonicalAnswer) = WireEncode.answerObject answer
