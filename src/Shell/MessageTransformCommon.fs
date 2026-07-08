module Wanxiangshu.Shell.MessageTransformCommon

open Wanxiangshu.Shell.HostMessagePartCodec

let extractTextsFromEncodedMessages (messages: obj array) : string seq =
    messages
    |> Seq.collect (fun msg -> getMessageParts msg |> extractTextLinesFromParts)
