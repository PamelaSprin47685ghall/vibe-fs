module Wanxiangshu.Omp.CapsCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Shell.CapsPrelude
open Wanxiangshu.Shell.OmpCaps
module Dyn = Wanxiangshu.Shell.Dyn

let private entryId (entry: obj) : string =
    let info = Dyn.get entry "info"
    if Dyn.isNullish info then ""
    else
        let id = Dyn.str info "id"
        if id <> "" then id else Dyn.str entry "id"

let private entrySessionId (entry: obj) : string =
    let info = Dyn.get entry "info"
    if Dyn.isNullish info then "" else Dyn.str info "sessionID"

let hasExistingCapsMessages (entries: obj array) : bool =
    entries.Length > 0 && (entryId entries.[0]).StartsWith capsUserPrefix

let private stripExistingCapsMessages (entries: obj array) : obj array =
    if not (hasExistingCapsMessages entries) then entries
    else
        entries
        |> Array.skipWhile (fun entry ->
            let id = entryId entry
            id <> "" && id.StartsWith "caps-synth-")

let private ompCapsToKernel (files: OmpCapsFile list) : CapsFile list =
    files |> List.map (fun f -> { filePath = f.filePath; label = f.label; content = f.content })

let private buildTextPart (text: string) : obj =
    createObj [ "type", box "text"; "text", box text ]

let private buildUserEntry (userId: string) (sessionId: string) (preludeText: string option) : obj =
    let text =
        match preludeText with
        | Some prelude when prelude.Trim() <> "" -> prelude.Trim() + "\n\n" + thinkWrapped
        | _ -> thinkWrapped
    let info = createObj [ "id", box userId; "role", box "user" ]
    if sessionId <> "" then info?sessionID <- box sessionId
    createObj [ "info", box info; "parts", box [| buildTextPart text |] ]

let private buildReadToolPart (cap: CapsFile) (fp: string) (index: int) : obj =
    createObj [
        "type", box "tool"
        "tool", box "read"
        "callID", box $"caps-call-{fp}-{index}"
        "state",
            box(
                createObj [
                    "status", box "completed"
                    "input", box(createObj [ "filePath", box cap.filePath ])
                    "output", box(formatReadOutput cap.filePath cap.content 1)
                    "error", box ""
                ])
    ]

let private buildAssistantEntry (assistantId: string) (parentUserId: string) (sessionId: string) (projectRoot: string) (capsFiles: CapsFile list) (fp: string) : obj =
    let parts = capsFiles |> List.mapi (fun i cap -> buildReadToolPart cap fp i) |> List.toArray
    let info = createObj [ "id", box assistantId; "role", box "assistant"; "parentID", box parentUserId ]
    if sessionId <> "" then info?sessionID <- box sessionId
    if projectRoot <> "" then info?path <- box(createObj [ "cwd", box projectRoot; "root", box projectRoot ])
    createObj [ "info", box info; "parts", box parts ]

let private buildAckEntry (ackId: string) (parentUserId: string) (sessionId: string) : obj =
    let info = createObj [ "id", box ackId; "role", box "assistant"; "parentID", box parentUserId ]
    if sessionId <> "" then info?sessionID <- box sessionId
    createObj [
        "info", box info
        "parts", box [| createObj [ "type", box "reasoning"; "text", box acknowledgeText ] |]
    ]

let private findFirstRealEntry (entries: obj array) : obj option =
    entries
    |> Array.tryFind (fun entry ->
        let id = entryId entry
        id <> "" && not (id.StartsWith capsUserPrefix) && not (id.StartsWith capsAssistantPrefix) && not (id.StartsWith capsAcknowledgePrefix))

let buildCapsEntries (hashFn: string -> string) (entries: obj array) (projectRoot: string) (ompCaps: OmpCapsFile list) (preludeText: string option) : obj array =
    match findFirstRealEntry entries with
    | None -> entries
    | Some _ ->
        let stripped = stripExistingCapsMessages entries
        if stripped.Length = 0 then entries
        else
            let capsFiles = ompCapsToKernel ompCaps
            let sessionId = entrySessionId stripped.[0]
            let fp = stableFingerprint hashFn capsFiles
            let userId = $"{capsUserPrefix}{fp}"
            let assistantId = $"{capsAssistantPrefix}{fp}"
            let ackId = $"{capsAcknowledgePrefix}{fp}"
            let userEntry = buildUserEntry userId sessionId preludeText
            let ackEntry = buildAckEntry ackId userId sessionId
            let assistantEntries =
                if capsFiles.IsEmpty
                then [| ackEntry |]
                else
                    [| ackEntry
                       buildAssistantEntry assistantId userId sessionId projectRoot capsFiles fp |]
            Array.concat [| [| userEntry |]; assistantEntries; stripped |]