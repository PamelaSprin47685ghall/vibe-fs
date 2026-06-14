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
    if files.IsEmpty then ""
    else
        files
        |> List.map (fun f -> $"<caps-context file=\"{escapeXmlAttr f.label}\">\n{f.content}\n</caps-context>")
        |> String.concat "\n\n"

let private capsUserPrefix = "caps-synth-user-"
let private capsAssistantPrefix = "caps-synth-assistant-"

[<Import("createHash", "node:crypto")>]
let private createHash (algorithm: string) : obj = jsNative

let private hashUpdate (hash: obj) (data: string) : unit =
    hash?update(data) |> ignore

let private hashDigestHexSlice (hash: obj) (startIndex: int) (endIndex: int) : string =
    hash?digest("hex")?slice(startIndex, endIndex)

let stableFingerprint (capsFiles: CapsFile list) : string =
    let hash = createHash "sha256"
    for cap in capsFiles do
        hashUpdate hash cap.filePath
        hashUpdate hash "\u0000"
        hashUpdate hash cap.content
        hashUpdate hash "\u0000"
    hashDigestHexSlice hash 0 16

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

let private messageId (msg: obj) : string =
    let info = Dyn.get msg "info"
    if Dyn.isNullish info then "" else Dyn.str info "id"

let private messageAgent (msg: obj) : string =
    let info = Dyn.get msg "info"
    if Dyn.isNullish info then "" else Dyn.str info "agent"

let private messageSessionID (msg: obj) : string =
    let info = Dyn.get msg "info"
    if Dyn.isNullish info then "" else Dyn.str info "sessionID"

let hasExistingCapsMessages (messages: obj array) : bool =
    messages.Length >= 2 &&
    let id0 = messageId messages.[0] in id0 <> "" && id0.StartsWith capsUserPrefix &&
    let id1 = messageId messages.[1] in id1 <> "" && id1.StartsWith capsAssistantPrefix

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

let private buildUserMessage (userId: string) (sessionID: string option) : obj =
    box (createObj [
        "info", box (createObj [
            "id", box userId
            "sessionID", match sessionID with Some s -> box s | None -> box null
            "role", box "user"
            "time", box (createObj [ "created", box 0 ])
            "agent", box "orchestrator"
            "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
        ])
        "parts", box [| box {| ``type`` = "text"; text = "你好" |} |]
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

let buildCapsMessages
    (messages: obj array)
    (projectRoot: string)
    (excludedAgents: string list)
    (capsFiles: CapsFile list)
    : obj array =
    if messages.Length = 0 then messages
    else
        let existingStripped = if hasExistingCapsMessages messages then messages.[2..] else messages
        if existingStripped.Length = 0 then messages
        elif excludedAgents |> List.contains (messageAgent existingStripped.[0]) then existingStripped
        elif capsFiles.IsEmpty then existingStripped
        else
            let sessionID = messageSessionID existingStripped.[0]
            let sessionOpt = if sessionID = "" then None else Some sessionID
            let fp = stableFingerprint capsFiles
            let userId = $"{capsUserPrefix}{fp}"
            let assistantId = $"{capsAssistantPrefix}{fp}"
            let toolParts = buildToolParts capsFiles fp sessionOpt assistantId
            let userMsg = buildUserMessage userId sessionOpt
            let assistantMsg = buildAssistantMessage assistantId userId sessionOpt projectRoot toolParts
            Array.concat [| [| userMsg |]; [| assistantMsg |]; existingStripped |]
