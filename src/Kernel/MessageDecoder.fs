module VibeFs.Kernel.MessageDecoder

open Fable.Core.JsInterop
open Fable.Core
open VibeFs.Kernel

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

let infoTimeCompleted (info: obj) : obj =
    let time = Dyn.get info "time"
    if Dyn.isNullish time then null else Dyn.get time "completed"

let entryType (entry: obj) : string = Dyn.str entry "type"

let entryCustomType (entry: obj) : string = Dyn.str entry "customType"

let entryMessage (entry: obj) : obj = Dyn.get entry "message"

let entryData (entry: obj) : obj = Dyn.get entry "data"

let partText (part: obj) : obj = Dyn.get part "text"

let partType (part: obj) : string = Dyn.str part "type"

let partError (part: obj) : obj = Dyn.get part "error"

let partState (part: obj) : obj = Dyn.get part "state"

let partOutput (part: obj) : obj = Dyn.get part "output"

let private allPrefixes =
    [ capsSynthUserPrefix; capsSynthAssistantPrefix; magicTodoProjectionPrefix; magicTodoPrefixPrefix ]

let isSyntheticId (id: string) : bool = id <> "" && allPrefixes |> List.exists id.StartsWith

let isSyntheticMessage (msg: obj) : bool =
    let info = messageInfo msg
    if Dyn.isNullish info then false else isSyntheticId (infoId info)

let stripSyntheticMessages (messages: obj array) : obj array =
    if Dyn.isNullish messages then [||]
    else messages |> Array.filter (fun msg -> not (isSyntheticMessage msg))

type AssistantTextOptions = { startIndex: int; joiner: string }

let defaultOptions = { startIndex = 0; joiner = "\n\n" }

let readAssistantText (entries: obj array) (options: AssistantTextOptions option) : string option =
    let opts = defaultArg options defaultOptions
    let chunks = ResizeArray<string>()
    for index in opts.startIndex .. entries.Length - 1 do
        let entry = entries.[index]
        if not (Dyn.isNullish entry) && entryType entry = "message" then
            let message = entryMessage entry
            if not (Dyn.isNullish message) && infoRole message = "assistant" then
                let content = infoContent message
                if not (Dyn.isNullish content) && Dyn.isArray content then
                    for part in content :?> obj array do
                        if not (Dyn.isNullish part) && partType part = "text" then
                            let text = partText part
                            if not (Dyn.isNullish text) && string text <> "" then chunks.Add(string text)
    if chunks.Count > 0 then Some(String.concat opts.joiner chunks) else None

let getLatestTodoPhasesFromEntries (entries: obj array) : obj =
    let rec scan index =
        if index < 0 then box [||]
        else
            let entry = entries.[index]
            if Dyn.isNullish entry then scan (index - 1)
            elif entryType entry = "custom" && entryCustomType entry = "user_todo_edit" then
                let data = entryData entry
                let phases = if Dyn.isNullish data then null else Dyn.get data "phases"
                if not (Dyn.isNullish phases) && Dyn.isArray phases then Dyn.clone phases else scan (index - 1)
            elif entryType entry <> "message" then scan (index - 1)
            else
                let message = entryMessage entry
                if Dyn.isNullish message || infoRole message <> "toolResult" || infoToolName message <> "todowrite" then
                    scan (index - 1)
                elif infoIsError message then
                    scan (index - 1)
                else
                    let details = infoDetails message
                    let phases = if Dyn.isNullish details then null else Dyn.get details "phases"
                    if not (Dyn.isNullish phases) && Dyn.isArray phases then Dyn.clone phases else scan (index - 1)
    scan (entries.Length - 1)


let firstPresent (keys: string list) (source: obj) : string option =
    keys |> List.tryPick (fun key ->
        let value = Dyn.get source key
        if Dyn.isNullish value then None else Some (string value))
