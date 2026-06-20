module VibeFs.Opencode.CapsCodec

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.CapsFormat
open VibeFs.Opencode.CapsPrelude

/// The obj-boundary layer for caps message synthesis: reads info fields off
/// host message objects and constructs the synthetic user/assistant prefix
/// objects. Pure formatting (escapeXmlAttr/buildCapitalsContext/etc.) stays in
/// Kernel.CapsFormat; this module is the only site that touches host objects.

let private capsUserPrefix = "caps-synth-user-"
let private capsAssistantPrefix = "caps-synth-assistant-"

let private messageInfoField (field: obj -> string) (msg: obj) : string =
    let info = get msg "info"
    if isNullish info then "" else field info

let messageId (msg: obj) : string =
    messageInfoField (fun info -> str info "id") msg

let private isPrefixed (prefix: string) (msg: obj) : bool =
    let id = messageId msg
    id <> "" && id.StartsWith prefix

let messageAgent (msg: obj) : string =
    messageInfoField (fun info -> str info "agent") msg

let messageSessionID (msg: obj) : string =
    messageInfoField (fun info -> str info "sessionID") msg

let hasExistingCapsMessages (messages: obj array) : bool =
    match messages |> List.ofArray with
    | m0 :: m1 :: _ ->
        isPrefixed capsUserPrefix m0
        && isPrefixed capsAssistantPrefix m1
    | _ -> false

let private stripExistingCapsMessages (messages: obj array) : obj array =
    if not (hasExistingCapsMessages messages) then messages
    else messages.[2..]

let private sessionBox (sessionID: string option) : obj =
    match sessionID with Some s -> box s | None -> box null

let private buildToolParts (capsFiles: CapsFile list) (fp: string) (sessionID: string option) (assistantId: string) : obj array =
    capsFiles
    |> List.mapi (fun index cap ->
        box (createObj [
            "type", box "tool"
            "tool", box "read"
            "callID", box $"caps-call-{fp}-{index}"
            "id", box $"caps-tool-{fp}-{index}"
            "sessionID", sessionBox sessionID
            "messageID", box assistantId
            "state", box (createObj [
                "status", box "completed"
                "input", box (createObj [ "filePath", box cap.filePath ])
                "output", box (formatReadOutput cap.filePath cap.content)
                "title", box $"Read {cap.filePath}"
                "metadata", box (createObj [])
                "time", box (createObj [ "start", box 0; "end", box 1 ])
            ])
        ]))
    |> Array.ofList

let private buildUserMessage (userId: string) (sessionID: string option) (preludeText: string option) : obj =
    let text =
        match preludeText with
        | Some prelude when prelude.Trim() <> "" -> "你好\n\n" + prelude.Trim()
        | _ -> "你好"
    box (createObj [
        "info", box (createObj [
            "id", box userId
            "sessionID", sessionBox sessionID
            "role", box "user"
            "time", box (createObj [ "created", box 0 ])
            "agent", box "orchestrator"
            "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
        ])
        "parts", box [| box {| ``type`` = "text"; text = text |} |]
    ])

let private assistantInfo (assistantId: string) (parentID: string) (sessionID: string option) (projectRoot: string) : obj =
    createObj [
        "id", box assistantId
        "sessionID", sessionBox sessionID
        "role", box "assistant"
        "time", box (createObj [ "created", box 0; "completed", box 1 ])
        "parentID", box parentID
        "modelID", box ""
        "providerID", box ""
        "mode", box "code"
        "path", box (createObj [ "cwd", box projectRoot; "root", box projectRoot ])
        "cost", box 0
        "tokens", box (createObj [
            "input", box 0
            "output", box 0
            "reasoning", box 0
            "cache", box (createObj [ "read", box 0; "write", box 0 ])
        ])
    ]

let private textPart (partId: string) (sessionID: string option) (messageID: string) (text: string) : obj =
    box (createObj [
        "id", box partId
        "sessionID", sessionBox sessionID
        "messageID", box messageID
        "type", box "text"
        "text", box text
    ])

let private reasoningPart (partId: string) (sessionID: string option) (messageID: string) (text: string) : obj =
    box (createObj [
        "id", box partId
        "sessionID", sessionBox sessionID
        "messageID", box messageID
        "type", box "reasoning"
        "text", box text
    ])

let private buildAssistantMessage (assistantId: string) (parentID: string) (sessionID: string option) (projectRoot: string) (parts: obj array) : obj =
    box (createObj [
        "info", box (assistantInfo assistantId parentID sessionID projectRoot)
        "parts", box parts
    ])

let private findFirstRealMessage (messages: obj array) : obj option =
    messages
    |> Array.tryFind (fun msg ->
        let id = messageId msg
        id <> "" && not (id.StartsWith capsUserPrefix) && not (id.StartsWith capsAssistantPrefix))

let buildCapsMessages
    (hashFn: string -> string)
    (messages: obj array)
    (projectRoot: string)
    (excludedAgents: string list)
    (capsFiles: CapsFile list)
    (preludeText: string option)
    : obj array =
    let shouldSkip =
        match findFirstRealMessage messages with
        | None -> true
        | Some firstReal -> excludedAgents |> List.contains (messageAgent firstReal)

    if shouldSkip then messages
    else
        let existingStripped =
            stripExistingCapsMessages messages
        let hasPrelude = match preludeText with Some text when text.Trim() <> "" -> true | _ -> false
        if existingStripped.Length = 0 then messages
        elif capsFiles.IsEmpty && not hasPrelude then existingStripped
        else
            let sessionID = messageSessionID existingStripped.[0]
            let sessionOpt = if sessionID = "" then None else Some sessionID
            let fp = stableFingerprint hashFn capsFiles
            let userId = $"{capsUserPrefix}{fp}"
            let assistantId = $"{capsAssistantPrefix}{fp}"
            let mergedParts =
                Array.append
                    [|
                        reasoningPart $"caps-reasoning-{fp}" sessionOpt assistantId thinkText
                        textPart $"caps-text-{fp}" sessionOpt assistantId llmText
                    |]
                    (if capsFiles.IsEmpty then [||] else buildToolParts capsFiles fp sessionOpt assistantId)
            let userMsg = buildUserMessage userId sessionOpt preludeText
            let mergedAssistantMsg = buildAssistantMessage assistantId userId sessionOpt projectRoot mergedParts
            Array.concat [| [| userMsg |]; [| mergedAssistantMsg |]; existingStripped |]
