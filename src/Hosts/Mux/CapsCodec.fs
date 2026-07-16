module Wanxiangshu.Hosts.Mux.CapsCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.CapsPrelude
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Runtime.CapsSynth

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.FileSys

let messageId (msg: obj) : string = Dyn.str msg "id"

let private buildTextPart (text: string) : obj =
    createObj [ "type", box "text"; "text", box text; "state", box "done" ]

let private buildMuxMessage (id: string) (role: string) (parts: obj array) : obj =
    createObj [ "id", box id; "role", box role; "parts", box parts ]

let private buildUserMessage (userId: string) (preludeText: string option) (epochId: string) (version: string) : obj =
    let body = userCapsText preludeText

    let wrappedText =
        $"<wanxiangshu-caps epoch='{epochId}' version='{version}'>\n{body}\n</wanxiangshu-caps>"

    buildMuxMessage userId "user" [| buildTextPart wrappedText |]

let private buildMuxToolPart (epochId: string) (fp: string) (index: int) (cap: CapsFile) : obj =
    let formattedOutput = formatReadOutput cap.filePath cap.content 1 None
    let callId = capsToolCallId "caps-fr-" epochId fp index

    let wrappedOutput =
        $"<wanxiangshu-caps-tools>\n{formattedOutput}\n</wanxiangshu-caps-tools>"

    createObj
        [ "type", box "dynamic-tool"
          "toolCallId", box callId
          "toolName", box "file_read"
          "state", box "output-available"
          "input", box (createObj [ "path", box cap.filePath ])
          "output",
          box (
              createObj
                  [ "success", box true
                    "file_size", box cap.content.Length
                    "modifiedTime", box "1970-01-01T00:00:00.000Z"
                    "lines_read", box (cap.content.Split('\n').Length)
                    "content", box wrappedOutput ]
          ) ]

let private buildCapsAssistantMessage
    (id: string)
    (parentId: string)
    (capsFiles: CapsFile list)
    (epochId: string)
    (fp: string)
    : obj =
    let parts =
        capsFiles
        |> List.mapi (fun i cap -> buildMuxToolPart epochId fp i cap)
        |> Array.ofList

    buildMuxMessage id "assistant" parts

let private buildAckMessage (ackId: string) : obj =
    let wrappedAck =
        $"<wanxiangshu-caps-ack>\n{acknowledgeText}\n</wanxiangshu-caps-ack>"

    buildMuxMessage ackId "assistant" [| createObj [ "type", box "reasoning"; "text", box wrappedAck ] |]

let buildCapsMessages
    (epochId: string)
    (messages: obj array)
    (capsFiles: CapsFile list)
    (preludeText: string option)
    : obj array =
    match findFirstNonSynthMessage messageId messages with
    | None -> messages
    | Some _ ->
        let existingStripped = stripLeadingCapsSynth messageId messages

        if existingStripped.Length = 0 then
            messages
        else
            let sortedCaps = capsFiles |> List.sortBy (fun cf -> cf.label, cf.filePath)
            let fp = stableFingerprint sha256HexTruncated sortedCaps
            let userId = $"{capsUserPrefix}{epochId}"
            let assistantId = $"{capsAssistantPrefix}{epochId}"
            let ackId = $"{capsAcknowledgePrefix}{epochId}"
            let userMsg = buildUserMessage userId preludeText epochId fp
            let ackMsg = buildAckMessage ackId

            let assistantMsgs =
                if sortedCaps.IsEmpty then
                    [| ackMsg |]
                else
                    [| ackMsg; buildCapsAssistantMessage assistantId userId sortedCaps epochId fp |]

            Array.concat [| [| userMsg |]; assistantMsgs; existingStripped |]
