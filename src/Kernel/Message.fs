module VibeFs.Kernel.Message

open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.HostTools

let capsSynthUserPrefix = "caps-synth-user-"
let capsSynthAssistantPrefix = "caps-synth-assistant-"
let magicTodoProjectionPrefix = "magic-todo-projection-"
let magicTodoPrefixPrefix = "magic-todo-prefix-"

let messageInfo (msg: obj) : obj = Dyn.get msg "info"
let messageParts (msg: obj) : obj = Dyn.get msg "parts"
let infoId (info: obj) : string = Dyn.str info "id"
let infoAgent (info: obj) : string = Dyn.str info "agent"
let infoSessionID (info: obj) : string = Dyn.str info "sessionID"
let infoRole (info: obj) : string = Dyn.str info "role"
let infoError (info: obj) : obj = Dyn.get info "error"
let infoToolName (info: obj) : string = Dyn.str info "toolName"
let infoContent (info: obj) : obj = Dyn.get info "content"
let infoDetails (info: obj) : obj = Dyn.get info "details"

let infoIsError (info: obj) : bool =
    let isError = Dyn.get info "isError"
    not (Dyn.isNullish isError) && (isError :?> bool)

let infoFinish (info: obj) : string = Dyn.str info "finish"
let infoTimeCompleted (info: obj) : obj = let time = Dyn.get info "time" in if Dyn.isNullish time then null else Dyn.get time "completed"
let entryType (entry: obj) : string = Dyn.str entry "type"
let entryCustomType (entry: obj) : string = Dyn.str entry "customType"
let entryMessage (entry: obj) : obj = Dyn.get entry "message"
let entryData (entry: obj) : obj = Dyn.get entry "data"
let partText (part: obj) : obj = Dyn.get part "text"
let partType (part: obj) : string = Dyn.str part "type"
let partError (part: obj) : obj = Dyn.get part "error"
let partState (part: obj) : obj = Dyn.get part "state"
let partOutput (part: obj) : obj = Dyn.get part "output"

let private allPrefixes = [ capsSynthUserPrefix; capsSynthAssistantPrefix; magicTodoProjectionPrefix; magicTodoPrefixPrefix ]
let isSyntheticId (id: string) : bool = id <> "" && allPrefixes |> List.exists id.StartsWith
/// Drop synthetic (caps / magic-todo projection) messages by Source DU rather
/// than string-prefix sniffing (P8). Messages here use the { info; parts } tree
/// that decodeMessage handles.
let stripSyntheticMessages (messages: obj array) : obj array =
    if Dyn.isNullish messages then [||]
    else
        messages
        |> Array.filter (fun msg ->
            match decodeMessage msg with
            | Some m -> m.source = Native
            | None -> true)

type AssistantTextOptions = { startIndex: int; joiner: string }
let defaultOptions = { startIndex = 0; joiner = "\n\n" }

/// Pull the `content` array off an entry iff it is a well-formed assistant
/// message; returns None for any non-assistant / malformed / empty entry. One
/// flat guard instead of the previous pyramid of nested `isNullish` checks (P9).
/// NOTE: session entries use the shape `{ type; message = { role; content } }`,
/// distinct from the { info; parts } message tree handled by Messaging, so this
/// boundary reader stays shape-specific rather than going through decodeMessage.
let private tryAssistantContent (entry: obj) : obj array option =
    if isNullish entry || entryType entry <> "message" then None
    else
        let message = entryMessage entry
        if isNullish message || infoRole message <> "assistant" then None
        else
            let content = infoContent message
            if isNullish content || not (isArray content) then None
            else Some (content :?> obj array)

/// Pull the non-empty text string off a content part, if it is a text part.
let private tryPartText (part: obj) : string option =
    if isNullish part || partType part <> "text" then None
    else
        let text = partText part
        if isNullish text || string text = "" then None else Some (string text)

let readAssistantText (entries: obj array) (options: AssistantTextOptions option) : string option =
    let opts = defaultArg options defaultOptions
    if opts.startIndex >= entries.Length then None
    else
        let chunks =
            entries.[opts.startIndex..]
            |> Array.choose tryAssistantContent
            |> Array.collect id
            |> Array.choose tryPartText
        if chunks.Length > 0 then Some(String.concat opts.joiner chunks) else None

let getLatestTodoPhasesFromEntriesFor (isTodoToolResult: string -> bool) (entries: obj array) : obj =
    let phasesOf entry =
        if isNullish entry then None
        elif entryType entry = "custom" && entryCustomType entry = "user_todo_edit" then
            let data = entryData entry
            let phases = if isNullish data then null else Dyn.get data "phases"
            if not (isNullish phases) && isArray phases then Some phases else None
        elif entryType entry <> "message" then None
        else
            let message = entryMessage entry
            if isNullish message || infoRole message <> "toolResult" || not (isTodoToolResult (infoToolName message)) then None
            elif infoIsError message then None
            else
                let details = infoDetails message
                let phases = if isNullish details then null else Dyn.get details "phases"
                if not (isNullish phases) && isArray phases then Some phases else None
    entries
    |> Array.tryFindBack (fun entry -> phasesOf entry |> Option.isSome)
    |> Option.bind phasesOf
    |> Option.map clone
    |> Option.defaultValue (box [||])

let getLatestTodoPhasesFromEntries (entries: obj array) : obj =
    getLatestTodoPhasesFromEntriesFor (fun toolName -> toolName = "todowrite") entries

let getLatestTodoPhasesFromEntriesForHost (host: Host) (entries: obj array) : obj =
    getLatestTodoPhasesFromEntriesFor (fun toolName -> toolName = todoWriteToolName host) entries

let firstPresent (keys: string list) (source: obj) : string option =
    keys |> List.tryPick (fun key -> let value = Dyn.get source key in if Dyn.isNullish value then None else Some (string value))

type FlatPart = { msgIndex: int; partIndex: int; isUser: bool; part: obj }
let messageIsUser (msg: obj) : bool = let info = messageInfo msg in if isNullish info then false else infoRole info = "user"
let partIsTool (part: obj) : bool = partType part = "tool"
let partIsText (part: obj) : bool = partType part = "text"
let partToolName (part: obj) : string = str part "tool"
let partToolStatus (part: obj) : string = let state = partState part in if isNullish state then "" else str state "status"
let partToolOutput (part: obj) : string = let state = partState part in if isNullish state then "" else str state "output"
let partToolError (part: obj) : string = let state = partState part in if isNullish state then "" else str state "error"
let partToolInput (part: obj) : obj = let state = partState part in if isNullish state then null else get state "input"
let partCallID (part: obj) : string = str part "callID"
let partTextStr (part: obj) : string = let text = partText part in if isNullish text then "" else string text
let setPartOutput (part: obj) (newOutput: string) : obj =
    withToolOutput (decodePart part) newOutput

let flatten (messages: obj array) : FlatPart list =
    messages
    |> Array.indexed
    |> Array.collect (fun (msgIdx, msg) ->
        if isNullish msg then [||]
        else
            let isUser = messageIsUser msg
            let parts = messageParts msg
            if isNullish parts || not (isArray parts) then [||]
            else
                (parts :?> obj array)
                |> Array.indexed
                |> Array.choose (fun (partIdx, part) ->
                    if isNullish part then None
                    else Some { msgIndex = msgIdx; partIndex = partIdx; isUser = isUser; part = part }))
    |> List.ofArray

let rebuild (messages: obj array) (visible: FlatPart list) : obj array =
    let byMessage = visible |> List.groupBy (fun e -> e.msgIndex) |> Map.ofList
    messages
    |> Array.indexed
    |> Array.choose (fun (msgIdx, msg) ->
        if isNullish msg then None
        else
            let isUser = messageIsUser msg
            match Map.tryFind msgIdx byMessage with
            | None -> if isUser then Some msg else None
            | Some entries ->
                if isUser then Some msg
                else
                    let partMap = entries |> List.map (fun e -> e.partIndex, e.part) |> Map.ofList
                    let originalParts = messageParts msg
                    if isNullish originalParts || not (isArray originalParts) then Some msg
                    else
                        let partsArr = originalParts :?> obj array
                        let newParts =
                            partsArr
                            |> Array.indexed
                            |> Array.choose (fun (partIdx, _) -> Map.tryFind partIdx partMap)
                        if newParts.Length > 0 then Some (Dyn.withKey msg "parts" (box newParts)) else None)
