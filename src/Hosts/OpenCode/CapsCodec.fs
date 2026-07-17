module Wanxiangshu.Hosts.Opencode.CapsCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Kernel.CapsPrelude
open Wanxiangshu.Runtime.CapsSynth
open Wanxiangshu.Runtime.Dyn

/// The obj-boundary layer for caps message synthesis: reads info fields off
/// host message objects and constructs the synthetic user/assistant prefix
/// objects. Pure formatting (buildCapitalsContext/etc.) stays in
/// Kernel.CapsFormat; this module is the only site that touches host objects.

let private messageInfoField (field: obj -> string) (msg: obj) : string =
    let info = get msg "info"
    if isNullish info then "" else field info

let messageId (msg: obj) : string =
    messageInfoField (fun info -> str info "id") msg

let messageSessionID (msg: obj) : string =
    messageInfoField (fun info -> str info "sessionID") msg

let hasExistingCapsMessages (messages: obj array) : bool =
    CapsSynth.hasExistingCapsMessages messageId messages

let private stripExistingCapsMessages (messages: obj array) : obj array =
    stripLeadingCapsSynth messageId messages

let private sessionBox (sessionID: string option) : obj =
    match sessionID with
    | Some s -> box s
    | None -> box null

let private buildToolParts
    (capsFiles: CapsFile list)
    (epochId: string)
    (fp: string)
    (sessionID: string option)
    (assistantId: string)
    : obj array =
    capsFiles
    |> List.mapi (fun index cap ->
        let lines = cap.content.Split('\n')
        let numbered = Wanxiangshu.Runtime.NativeReadTranscript.formatNumberedLines 1 lines

        let wrappedOutput =
            $"{cap.filePath}\n{numbered}\n(End of file - total {lines.Length} lines)"

        let callId = capsToolCallId "caps-call-" epochId fp index

        box (
            createObj
                [ "type", box "tool"
                  "tool", box "read"
                  "callID", box callId
                  "id", box $"caps-tool-{epochId}-{fp}-{index}"
                  "sessionID", sessionBox sessionID
                  "messageID", box assistantId
                  "state",
                  box (
                      createObj
                          [ "status", box "completed"
                            "input", box (createObj [ "filePath", box cap.filePath ])
                            "output", box wrappedOutput
                            "title", box $"Read {cap.filePath}"
                            "metadata", box (createObj [])
                            "time", box (createObj [ "start", box 0; "end", box 1 ]) ]
                  ) ]
        ))
    |> Array.ofList

let private buildUserMessage
    (userId: string)
    (sessionID: string option)
    (preludeText: string option)
    (epochId: string)
    (version: string)
    : obj =
    let body = userCapsText preludeText

    let wrappedText =
        $"<wanxiangshu-caps epoch='{epochId}' version='{version}'>\n{body}\n</wanxiangshu-caps>"

    box (
        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box userId
                        "sessionID", sessionBox sessionID
                        "role", box "user"
                        "time", box (createObj [ "created", box 0 ])
                        "agent", box "orchestrator"
                        "model", box (createObj [ "providerID", box ""; "modelID", box "" ]) ]
              )
              "parts",
              box
                  [| box
                         {| ``type`` = "text"
                            text = wrappedText |} |] ]
    )

let private assistantInfo
    (assistantId: string)
    (parentID: string)
    (sessionID: string option)
    (projectRoot: string)
    : obj =
    createObj
        [ "id", box assistantId
          "sessionID", sessionBox sessionID
          "role", box "assistant"
          "time", box (createObj [ "created", box 0; "completed", box 1 ])
          "parentID", box parentID
          "modelID", box ""
          "providerID", box ""
          "mode", box "code"
          "path", box (createObj [ "cwd", box projectRoot; "root", box projectRoot ])
          "cost", box 0
          "tokens",
          box (
              createObj
                  [ "input", box 0
                    "output", box 0
                    "reasoning", box 0
                    "cache", box (createObj [ "read", box 0; "write", box 0 ]) ]
          ) ]

let private buildAssistantMessage
    (assistantId: string)
    (parentID: string)
    (sessionID: string option)
    (projectRoot: string)
    (parts: obj array)
    : obj =
    box (
        createObj
            [ "info", box (assistantInfo assistantId parentID sessionID projectRoot)
              "parts", box parts ]
    )

let private buildAckMessage (ackId: string) (parentID: string) (sessionID: string option) (projectRoot: string) : obj =
    let wrappedAck =
        $"<wanxiangshu-caps-ack>\n{acknowledgeText}\n</wanxiangshu-caps-ack>"

    buildAssistantMessage
        ackId
        parentID
        sessionID
        projectRoot
        [| box (createObj [ "type", box "text"; "text", box wrappedAck ]) |]

/// Build the synthetic caps prefix: a single user message whose text wraps
/// thinkText + llmText in <think></think>, then an assistant reasoning ack
/// ("好的，我将遵守规则。"), followed by optional caps-file tool reads. The
/// caller decides suppression by passing an empty `capsFiles` (no file reads)
/// and/or `None` prelude; this keeps a single decision point in
/// `MessageTransformHook`. The only guard here is structural: nothing to anchor
/// onto when there is no real message.
let buildCapsMessages
    (hashFn: string -> string)
    (sessionID: string)
    (messages: obj array)
    (projectRoot: string)
    (capsFiles: CapsFile list)
    (preludeText: string option)
    : obj array =
    match findFirstNonSynthMessage messageId messages with
    | None -> messages
    | Some _ ->
        let existingStripped = stripExistingCapsMessages messages

        if existingStripped.Length = 0 then
            messages
        else
            let realSessionID =
                if sessionID <> "" then
                    sessionID
                else
                    messageSessionID existingStripped.[0]

            let sessionOpt = if realSessionID = "" then None else Some realSessionID
            let sortedCaps = capsFiles |> List.sortBy (fun cf -> cf.label, cf.filePath)
            let fp = stableFingerprint hashFn sortedCaps
            let epochId = realSessionID
            let userId = $"{capsUserPrefix}{epochId}"
            let assistantId = $"{capsAssistantPrefix}{epochId}"
            let ackId = $"{capsAcknowledgePrefix}{epochId}"

            let toolParts =
                if sortedCaps.IsEmpty then
                    [||]
                else
                    buildToolParts sortedCaps epochId fp sessionOpt assistantId

            let userMsg = buildUserMessage userId sessionOpt preludeText epochId fp
            let ackMsg = buildAckMessage ackId userId sessionOpt projectRoot

            let assistantMessages =
                if sortedCaps.IsEmpty then
                    [| ackMsg |]
                else
                    let explainPart =
                        box (
                            createObj
                                [ "type", box "text"
                                  "text", box "I am reading the project workspace files to understand context." ]
                        )

                    let combinedParts = Array.append [| explainPart |] toolParts

                    [| ackMsg
                       buildAssistantMessage assistantId userId sessionOpt projectRoot combinedParts |]

            Array.concat [| [| userMsg |]; assistantMessages; existingStripped |]
