module VibeFs.Kernel.BacklogProjectionCore

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.PromptFrontMatter

let todoWriteToolNameFor (host: Host) : string = todoWriteToolName host
let todoWriteToolNameDefault = todoWriteToolNameFor opencode
let reviewToolName = "submit_review"

type BacklogEntry =
    { report: string }

let isTodoResultFor (host: Host) (part: Part<'raw>) : bool =
    match part with
    | ToolPart(toolName, _, Some state, _) when toolName = todoWriteToolNameFor host && state.status = "completed" -> true
    | _ -> false

let isTodoResult (part: Part<'raw>) : bool =
    isTodoResultFor opencode part

let isTodoErrorFor (host: Host) (part: Part<'raw>) : bool =
    match part with
    | ToolPart(toolName, _, Some state, _) when toolName = todoWriteToolNameFor host && state.status = "error" -> true
    | _ -> false

let isTodoError (part: Part<'raw>) : bool =
    isTodoErrorFor opencode part

let lastTodoErrorTextFor (host: Host) (flat: FlatPart<'raw> list) : string option =
    flat
    |> List.tryFindBack (fun fp -> isTodoErrorFor host fp.part)
    |> Option.map (fun fp ->
        match fp.part with
        | ToolPart(_, _, Some state, _) -> state.error
        | _ -> "")

let isReviewTool (part: Part<'raw>) : bool =
    match part with
    | ToolPart(toolName, _, _, _) when toolName = reviewToolName -> true
    | _ -> false

let private indentBlock (text: string) : string =
    text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
    |> Array.map (fun line -> "      " + line)
    |> String.concat "\n"

let private renderUserMessages (userPrompts: string list) : string =
    if userPrompts.IsEmpty then
        "  user_message: []"
    else
        "  user_message:\n"
        + (userPrompts
           |> List.map (fun text -> "    - |\n" + indentBlock (text.Trim()))
           |> String.concat "\n")

let private renderCompletedWorkEntry (userPrompts: string list) (report: BacklogEntry) : string =
    "-\n"
    + renderUserMessages userPrompts
    + "\n  completed_work: |\n"
    + indentBlock (report.report.Trim())

let private projectionFrontMatter (backlog: BacklogEntry list) (userPrompts: string list) : string list =
    backlog |> List.map (renderCompletedWorkEntry userPrompts)

let private projectionBody (errorNotice: string option) : string =
    let baseText = "Completed work from folded turns. File changes are already on disk."

    match errorNotice with
    | Some err when err.Trim() <> "" -> baseText + "\n\nLast todo write error: " + err.Trim()
    | _ -> baseText

let buildBacklogTextWithError (backlog: BacklogEntry list) (userPrompts: string list) (errorNotice: string option) : string =
    frontMatterPrompt (projectionFrontMatter backlog userPrompts) (projectionBody errorNotice)

let buildBacklogText (backlog: BacklogEntry list) (userPrompts: string list) : string =
    buildBacklogTextWithError backlog userPrompts None

let lastTodoErrorText (flat: FlatPart<'raw> list) : string option =
    lastTodoErrorTextFor opencode flat