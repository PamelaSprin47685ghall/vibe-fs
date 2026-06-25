module VibeFs.Shell.MessageTransformCommon

open VibeFs.Shell.HostMessagePartCodec

let extractTextsFromEncodedMessages (messages: obj array) : string seq =
    messages
    |> Seq.collect (fun msg ->
        getMessageParts msg |> extractTextLinesFromParts)