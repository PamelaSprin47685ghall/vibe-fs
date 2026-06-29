module Wanxiangshu.Mux.CapsCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Shell.CapsSynthCommon
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FileSys

let messageId (msg: obj) : string = str msg "id"

let private buildTextPart (text: string) : obj =
    createObj [ "type", box "text"; "text", box text; "state", box "done" ]

let private buildMuxMessage (id: string) (role: string) (parts: obj array) : obj =
    createObj [ "id", box id; "role", box role; "parts", box parts ]

let private buildUserMessage (userId: string) (preludeText: string option) : obj =
    buildMuxMessage userId "user" [| buildTextPart (userCapsText preludeText) |]

let private buildCapsAssistantMessage (id: string) (parentId: string) (capsFiles: CapsFile list) (fp: string) : obj =
    let parts =
        capsFiles
        |> List.mapi (fun index cap ->
            createObj
                [ "type", box "dynamic-tool"
                  "toolCallId", box $"caps-fr-{fp}-{index}"
                  "toolName", box "file_read"
                  "state", box "output-available"
                  "input", box (createObj [ "path", box cap.filePath ])
                  "output",
                  box
                      (createObj
                          [ "success", box true
                            "file_size", box cap.content.Length
                            "modifiedTime", box "1970-01-01T00:00:00.000Z"
                            "lines_read", box (cap.content.Split('\n').Length)
                            "content", box (formatReadOutput cap.filePath cap.content 1) ]) ])
        |> Array.ofList
    buildMuxMessage id "assistant" parts

let buildCapsMessages
    (messages: obj array)
    (capsFiles: CapsFile list)
    (preludeText: string option)
    : obj array =
    match findFirstNonSynthMessage messageId messages with
    | None -> messages
    | Some _ ->
        let existingStripped = stripLeadingCapsSynth messageId messages
        if existingStripped.Length = 0 then messages
        else
            let fp = stableFingerprint sha256HexTruncated capsFiles
            let userId = $"{capsUserPrefix}{fp}"
            let assistantId = $"{capsAssistantPrefix}{fp}"
            let userMsg = buildUserMessage userId preludeText
            let assistantMsgs =
                if capsFiles.IsEmpty then [||]
                else [| buildCapsAssistantMessage assistantId userId capsFiles fp |]
            Array.concat [| [| userMsg |]; assistantMsgs; existingStripped |]