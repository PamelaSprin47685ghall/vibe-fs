module Wanxiangshu.Mux.CapsCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.CapsPrelude
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Shell.CapsSynthCommon

module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FileSys

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

let private buildMuxToolPart (epochId: string) (index: int) (cap: CapsFile) : obj =
    createObj
        [ "type", box "dynamic-tool"
          "toolCallId", box $"caps-fr-{epochId}-{index}"
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
                    "content", box (formatReadOutput cap.filePath cap.content 1 None) ]
          ) ]

let private buildCapsAssistantMessage
    (id: string)
    (parentId: string)
    (capsFiles: CapsFile list)
    (epochId: string)
    : obj =
    let parts = capsFiles |> List.mapi (buildMuxToolPart epochId) |> Array.ofList
    buildMuxMessage id "assistant" parts

let private buildAckMessage (ackId: string) : obj =
    buildMuxMessage ackId "assistant" [| createObj [ "type", box "reasoning"; "text", box acknowledgeText ] |]

let buildCapsMessages
    (sessionID: string)
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
            let fp = stableFingerprint sha256HexTruncated capsFiles
            let epochId = sessionID
            let userId = $"{capsUserPrefix}{epochId}"
            let assistantId = $"{capsAssistantPrefix}{epochId}"
            let ackId = $"{capsAcknowledgePrefix}{epochId}"
            let userMsg = buildUserMessage userId preludeText epochId fp
            let ackMsg = buildAckMessage ackId

            let assistantMsgs =
                if capsFiles.IsEmpty then
                    [| ackMsg |]
                else
                    [| ackMsg; buildCapsAssistantMessage assistantId userId capsFiles epochId |]

            Array.concat [| [| userMsg |]; assistantMsgs; existingStripped |]
