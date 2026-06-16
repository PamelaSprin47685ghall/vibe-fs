module VibeFs.Kernel.SessionText

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.MessageDecoder

type AssistantTextOptions = { startIndex: int; joiner: string }

let defaultOptions = { startIndex = 0; joiner = "\n\n" }

/// Extract concatenated assistant text from a session entry list.  Pure over
/// the (dynamically-shaped) entry objects: only reads, never mutates.
let readAssistantText (entries: obj array) (options: AssistantTextOptions option) : string option =
    let opts = defaultArg options defaultOptions
    let chunks = ResizeArray<string>()
    for i in opts.startIndex .. entries.Length - 1 do
        let entry = entries.[i]
        if Dyn.isNullish entry || entryType entry <> "message" then ()
        else
            let message = entryMessage entry
            if Dyn.isNullish message || infoRole message <> "assistant" then ()
            else
                let content = infoContent message
                if not (Dyn.isNullish content) && Dyn.isArray content then
                    for part in content :?> obj array do
                        if not (Dyn.isNullish part) && partType part = "text" then
                            let text = partText part
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
            elif entryType entry = "custom" && entryCustomType entry = "user_todo_edit" then
                let data = entryData entry
                let phases = if Dyn.isNullish data then null else Dyn.get data "phases"
                if not (Dyn.isNullish phases) && Dyn.isArray phases then Dyn.clone phases
                else scan (i - 1)
            elif entryType entry <> "message" then scan (i - 1)
            else
                let message = entryMessage entry
                if Dyn.isNullish message
                   || infoRole message <> "toolResult"
                   || infoToolName message <> "todowrite" then scan (i - 1)
                else
                    if infoIsError message then scan (i - 1)
                    else
                        let details = infoDetails message
                        let phases = if Dyn.isNullish details then null else Dyn.get details "phases"
                        if not (Dyn.isNullish phases) && Dyn.isArray phases then Dyn.clone phases
                        else scan (i - 1)
    scan (entries.Length - 1)
