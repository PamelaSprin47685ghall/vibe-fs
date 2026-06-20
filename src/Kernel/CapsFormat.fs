module VibeFs.Kernel.CapsFormat

open Fable.Core
open Fable.Core.JsInterop

/// Escape a string for safe use inside an XML attribute value.
let escapeXmlAttr (value: string) : string =
    value.Replace("&", "&amp;").Replace("\"", "&quot;")
         .Replace("'", "&apos;").Replace("<", "&lt;").Replace(">", "&gt;")

/// A discovered capability file: its absolute path, display label, and content.
type CapsFile = { filePath: string; label: string; content: string }

/// Wrap already-discovered capability files in `<caps-context>` blocks.  Pure:
/// file discovery lives in the shell; this only formats.
let buildCapitalsContext (files: CapsFile list) : string =
    files
    |> List.map (fun f -> $"<caps-context file=\"{escapeXmlAttr f.label}\">\n{f.content}\n</caps-context>")
    |> String.concat "\n\n"

let private capsUserPrefix = "caps-synth-user-"
let private capsAssistantPrefix = "caps-synth-assistant-"

/// Stable fingerprint over caps files — kernel decides WHAT to hash, the
/// injected `hashFn` decides HOW (e.g. Shell.Crypto.sha256HexTruncated).
let stableFingerprint (hashFn: string -> string) (capsFiles: CapsFile list) : string =
    capsFiles
    |> List.collect (fun cap -> [ cap.filePath; "\u0000"; cap.content; "\u0000" ])
    |> String.concat ""
    |> hashFn

let formatReadOutput (filePath: string) (content: string) : string =
    let lines = content.Split('\n')
    let numbered = lines |> Array.mapi (fun i line -> $"{i + 1}: {line}") |> String.concat "\n"
    String.concat "\n" [
        $"<path>{filePath}</path>"
        "<type>file</type>"
        "<content>"
        numbered
        ""
        $"(End of file - total {lines.Length} lines)"
        "</content>"
    ]

let private messageInfoField (field: obj -> string) (msg: obj) : string =
    let info = VibeFs.Kernel.Message.messageInfo msg
    if Dyn.isNullish info then "" else field info

let private messageId (msg: obj) : string =
    messageInfoField VibeFs.Kernel.Message.infoId msg

let private messageAgent (msg: obj) : string =
    messageInfoField VibeFs.Kernel.Message.infoAgent msg

let private messageSessionID (msg: obj) : string =
    messageInfoField VibeFs.Kernel.Message.infoSessionID msg

let hasExistingCapsMessages (messages: obj array) : bool =
    match messages |> List.ofArray with
    | m0 :: m1 :: _ ->
        let id0 = messageId m0
        id0 <> "" && id0.StartsWith capsUserPrefix &&
        (let id1 = messageId m1 in id1 <> "" && id1.StartsWith capsAssistantPrefix)
    | _ -> false

let private buildToolParts (capsFiles: CapsFile list) (fp: string) (sessionID: string option) (assistantId: string) : obj array =
    capsFiles
    |> List.mapi (fun index cap ->
        box (createObj [
            "type", box "tool"
            "tool", box "read"
            "callID", box $"caps-call-{fp}-{index}"
            "id", box $"caps-tool-{fp}-{index}"
            "sessionID", match sessionID with Some s -> box s | None -> box null
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
            "sessionID", match sessionID with Some s -> box s | None -> box null
            "role", box "user"
            "time", box (createObj [ "created", box 0 ])
            "agent", box "orchestrator"
            "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
        ])
        "parts", box [| box {| ``type`` = "text"; text = text |} |]
    ])

let private buildAssistantMessage (assistantId: string) (userId: string) (sessionID: string option) (projectRoot: string) (toolParts: obj array) : obj =
    box (createObj [
        "info", box (createObj [
            "id", box assistantId
            "sessionID", match sessionID with Some s -> box s | None -> box null
            "role", box "assistant"
            "time", box (createObj [ "created", box 0; "completed", box 1 ])
            "parentID", box userId
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
        ])
        "parts", box toolParts
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
            if hasExistingCapsMessages messages && messages.Length >= 2 then messages.[2..]
            else messages
        let hasPrelude = match preludeText with Some text when text.Trim() <> "" -> true | _ -> false
        if existingStripped.Length = 0 then messages
        elif capsFiles.IsEmpty && not hasPrelude then existingStripped
        else
            let sessionID = messageSessionID existingStripped.[0]
            let sessionOpt = if sessionID = "" then None else Some sessionID
            let fp = stableFingerprint hashFn capsFiles
            let userId = $"{capsUserPrefix}{fp}"
            let assistantId = $"{capsAssistantPrefix}{fp}"
            let toolParts = if capsFiles.IsEmpty then [||] else buildToolParts capsFiles fp sessionOpt assistantId
            let userMsg = buildUserMessage userId sessionOpt preludeText
            let assistantMsg = buildAssistantMessage assistantId userId sessionOpt projectRoot toolParts
            Array.concat [| [| userMsg |]; [| assistantMsg |]; existingStripped |]
