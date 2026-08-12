namespace Wanxiangshu.Sphinx

open Fable.Core.JsInterop

module Codec =

    let decodeObservation = ObservationCodec.decode

    let requestObject = WireEncode.requestObject

    let answerObject = WireEncode.answerObject

    let private envelope handle fields =
        let handleField =
            match handle with
            | Some value -> [ "handle" ==> value ]
            | None -> []

        createObj (handleField @ fields)

    let inquiryResultObject (handle: string option) (inquiryResult: InquiryResult) =
        match inquiryResult with
        | InquiryResult.Yield request -> envelope handle [ "status" ==> "yield"; "request" ==> requestObject request ]
        | InquiryResult.Answered answer -> envelope handle [ "status" ==> "answered"; "answer" ==> answerObject answer ]
        | InquiryResult.Error error -> envelope handle [ "status" ==> "error"; "error" ==> error ]
