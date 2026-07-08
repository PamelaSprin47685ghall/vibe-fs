module Wanxiangshu.Opencode.CapsCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Opencode.CapsPrelude
open Wanxiangshu.Shell.CapsSynthCommon
open Wanxiangshu.Shell.Dyn

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
    CapsSynthCommon.hasExistingCapsMessages messageId messages

let private stripExistingCapsMessages (messages: obj array) : obj array =
    stripLeadingCapsSynth messageId messages

let private sessionBox (sessionID: string option) : obj =
    match sessionID with
    | Some s -> box s
    | None -> box null

let private buildToolParts
    (capsFiles: CapsFile list)
    (fp: string)
    (sessionID: string option)
    (assistantId: string)
    : obj array =
    capsFiles
    |> List.mapi (fun index cap ->
        box (
            createObj
                [ "type", box "tool"
                  "tool", box "read"
                  "callID", box $"caps-call-{fp}-{index}"
                  "id", box $"caps-tool-{fp}-{index}"
                  "sessionID", sessionBox sessionID
                  "messageID", box assistantId
                  "state",
                  box (
                      createObj
                          [ "status", box "completed"
                            "input", box (createObj [ "filePath", box cap.filePath ])
                            "output", box (formatReadOutput cap.filePath cap.content 1 None)
                            "title", box $"Read {cap.filePath}"
                            "metadata", box (createObj [])
                            "time", box (createObj [ "start", box 0; "end", box 1 ]) ]
                  ) ]
        ))
    |> Array.ofList

let private buildUserMessage (userId: string) (sessionID: string option) (preludeText: string option) : obj =
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
                            text = userCapsText preludeText |} |] ]
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
    buildAssistantMessage
        ackId
        parentID
        sessionID
        projectRoot
        [| box (createObj [ "type", box "text"; "text", box acknowledgeText ]) |]

/// Build the synthetic caps prefix: a single user message whose text wraps
/// thinkText + llmText in <think></think>, then an assistant reasoning ack
/// ("好的，我将遵守规则。"), followed by optional caps-file tool reads. The
/// caller decides suppression by passing an empty `capsFiles` (no file reads)
/// and/or `None` prelude; this keeps a single decision point in
/// `MessageTransform`. The only guard here is structural: nothing to anchor
/// onto when there is no real message.
let buildCapsMessages
    (hashFn: string -> string)
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
            let sessionID = messageSessionID existingStripped.[0]
            let sessionOpt = if sessionID = "" then None else Some sessionID
            let fp = stableFingerprint hashFn capsFiles
            let userId = $"{capsUserPrefix}{fp}"
            let assistantId = $"{capsAssistantPrefix}{fp}"
            let ackId = $"{capsAcknowledgePrefix}{fp}"

            let toolParts =
                if capsFiles.IsEmpty then
                    [||]
                else
                    buildToolParts capsFiles fp sessionOpt assistantId

            let userMsg = buildUserMessage userId sessionOpt preludeText
            let ackMsg = buildAckMessage ackId userId sessionOpt projectRoot

            let assistantMessages =
                if capsFiles.IsEmpty then
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
