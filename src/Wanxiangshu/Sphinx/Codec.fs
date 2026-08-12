namespace Wanxiangshu.Sphinx

module Codec =

    let decodeObservation = ObservationCodec.decode

    let requestObject = WireEncode.requestObject

    let answerObject = WireEncode.answerObject
