module VibeFs.Kernel.SessionText

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

type AssistantTextOptions = { startIndex: int; joiner: string }

let defaultOptions = { startIndex = 0; joiner = "\n\n" }

/// Extract concatenated assistant text from a session entry list.  Pure over
/// the (dynamically-shaped) entry objects: only reads, never mutates.
let readAssistantText (entries: obj array) (options: AssistantTextOptions option) : string option =
    let opts = defaultArg options defaultOptions
    let chunks = ResizeArray<string>()
    for i in opts.startIndex .. entries.Length - 1 do
        let entry = entries.[i]
        if Dyn.isNullish entry || Dyn.str entry "type" <> "message" then ()
        else
            let message = Dyn.get entry "message"
            if Dyn.isNullish message || Dyn.str message "role" <> "assistant" then ()
            else
                let content = Dyn.get message "content"
                if not (Dyn.isNullish content) && Dyn.isArray content then
                    for part in content :?> obj array do
                        if not (Dyn.isNullish part) && Dyn.str part "type" = "text" then
                            let text = Dyn.get part "text"
                            if not (Dyn.isNullish text) && string text <> "" then chunks.Add(string text)
    if chunks.Count > 0 then Some(String.concat opts.joiner chunks) else None

/// Find the most recent todo phases data in the entry stream — either a custom
/// `user_todo_edit` event or a successful `todowrite` tool result — and return
/// a defensive deep clone so the caller owns a private copy.
let getLatestTodoPhasesFromEntries (entries: obj array) : obj =
    let rec scan i =
        if i < 0 then box [||]
        else
            let entry = entries.[i]
            if Dyn.isNullish entry then scan (i - 1)
            elif Dyn.str entry "type" = "custom" && Dyn.str entry "customType" = "user_todo_edit" then
                let data = Dyn.get entry "data"
                let phases = if Dyn.isNullish data then null else Dyn.get data "phases"
                if not (Dyn.isNullish phases) && Dyn.isArray phases then Dyn.clone phases
                else scan (i - 1)
            elif Dyn.str entry "type" <> "message" then scan (i - 1)
            else
                let message = Dyn.get entry "message"
                if Dyn.isNullish message
                   || Dyn.str message "role" <> "toolResult"
                   || Dyn.str message "toolName" <> "todowrite" then scan (i - 1)
                else
                    let isError = Dyn.get message "isError"
                    if not (Dyn.isNullish isError) && (isError :?> bool) then scan (i - 1)
                    else
                        let details = Dyn.get message "details"
                        let phases = if Dyn.isNullish details then null else Dyn.get details "phases"
                        if not (Dyn.isNullish phases) && Dyn.isArray phases then Dyn.clone phases
                        else scan (i - 1)
    scan (entries.Length - 1)
