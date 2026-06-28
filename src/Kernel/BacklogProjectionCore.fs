module Wanxiangshu.Kernel.BacklogProjectionCore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.PromptFrontMatter

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

let private completedWorkItem (userPrompts: string list) (entry: BacklogEntry) : obj =
    let userMsgField =
        if userPrompts.IsEmpty then box [||]
        else box (userPrompts |> List.map (fun text -> box (text.Trim())) |> List.toArray)
    createObj [ "user_message", userMsgField; "completed_work", box (entry.report.Trim()) ]

let private projectionRootValue (backlog: BacklogEntry list) (userPrompts: string list) : obj =
    box (backlog |> List.map (completedWorkItem userPrompts) |> List.toArray)

let private projectionBody (errorNotice: string option) : string =
    let baseText = "Completed work from folded turns. File changes are already on disk."

    match errorNotice with
    | Some err when err.Trim() <> "" -> baseText + "\n\nLast todo write error: " + err.Trim()
    | _ -> baseText

let buildBacklogTextWithError (backlog: BacklogEntry list) (userPrompts: string list) (errorNotice: string option) : string =
    frontMatterPromptRoot (projectionRootValue backlog userPrompts) (projectionBody errorNotice)

let buildBacklogText (backlog: BacklogEntry list) (userPrompts: string list) : string =
    buildBacklogTextWithError backlog userPrompts None

let lastTodoErrorText (flat: FlatPart<'raw> list) : string option =
    lastTodoErrorTextFor opencode flat

/// Build the compaction-anchor prompt text from backlog entries and a function
/// that extracts text candidates (message text + tool output) from the
/// message stream. Pure: shared by the compaction hook (MessageTransform) and
/// the post-compaction nudge path (NudgeEffect).
let buildCompactionAnchorPrompt
    (backlogEntries: BacklogEntry list)
    (extractAnchorTexts: unit -> string list)
    : string =
    let backlogBlock =
        let entries =
            backlogEntries
            |> List.map (fun be -> createObj [ "user_message", box [||]; "completed_work", box (be.report.Trim()) ])
            |> List.toArray
        [ frontMatterRoot (box entries) ]
    let anchorBlocks = extractAnchorTexts () |> List.collect extractFrontMatterFenceStrings
    renderCompactionAnchorPrompt (backlogBlock @ anchorBlocks)