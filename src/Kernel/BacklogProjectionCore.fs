module Wanxiangshu.Kernel.BacklogProjectionCore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.Message

let todoWriteToolNameFor (host: Host) : string = todoWriteToolName host
let todoWriteToolNameDefault = todoWriteToolNameFor opencode
let reviewToolName = "submit_review"

type BacklogEntry =
    { ahaMoments: string
      changesAndReasons: string
      gotchas: string
      lessonsAndConventions: string
      plan: string }

let trunc (s: string) : string = if s = null then "" else s.Trim()

let isTodoResultFor (host: Host) (part: Part<'raw>) : bool =
    match part with
    | ToolPart(toolName, _, Some state, _) when toolName = todoWriteToolNameFor host && state.status = "completed" ->
        true
    | _ -> false

let isTodoResult (part: Part<'raw>) : bool = isTodoResultFor opencode part

let isTodoErrorFor (host: Host) (part: Part<'raw>) : bool =
    match part with
    | ToolPart(toolName, _, Some state, _) when toolName = todoWriteToolNameFor host && state.status = "error" -> true
    | _ -> false

let isTodoError (part: Part<'raw>) : bool = isTodoErrorFor opencode part

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
        if userPrompts.IsEmpty then
            box [||]
        else
            box (userPrompts |> List.map (fun text -> box (text.Trim())) |> List.toArray)

    let fields =
        [ "user_message", userMsgField
          "aha_moments", box (trunc entry.ahaMoments)
          "changes_and_reasons", box (trunc entry.changesAndReasons)
          "gotchas", box (trunc entry.gotchas)
          "lessons_and_conventions", box (trunc entry.lessonsAndConventions)
          "plan", box (trunc entry.plan) ]

    createObj fields

let private projectionRootValue (backlog: BacklogEntry list) (userPrompts: string list) : obj =
    box (backlog |> List.map (completedWorkItem userPrompts) |> List.toArray)

let private projectionBody (errorNotice: string option) : string =
    let baseText = "Completed work from folded turns. File changes are already on disk."

    match errorNotice with
    | Some err when err.Trim() <> "" -> baseText + "\n\nLast todo write error: " + err.Trim()
    | _ -> baseText

let buildBacklogTextWithError
    (backlog: BacklogEntry list)
    (userPrompts: string list)
    (errorNotice: string option)
    : string =
    frontMatterPromptRoot (projectionRootValue backlog userPrompts) (projectionBody errorNotice)

let buildBacklogText (backlog: BacklogEntry list) (userPrompts: string list) : string =
    buildBacklogTextWithError backlog userPrompts None

let lastTodoErrorText (flat: FlatPart<'raw> list) : string option = lastTodoErrorTextFor opencode flat

let buildCompactionAnchorPrompt (backlogEntries: BacklogEntry list) (extractAnchorTexts: unit -> string list) : string =
    let anchorBlocks =
        extractAnchorTexts () |> List.collect extractFrontMatterFenceStrings

    if List.isEmpty backlogEntries && List.isEmpty anchorBlocks then
        ""
    else
        let backlogBlock =
            if List.isEmpty backlogEntries then
                []
            else
                let entries =
                    backlogEntries
                    |> List.map (fun be ->
                        let fields =
                            [ "user_message", box [||]
                              "aha_moments", box (trunc be.ahaMoments)
                              "changes_and_reasons", box (trunc be.changesAndReasons)
                              "gotchas", box (trunc be.gotchas)
                              "lessons_and_conventions", box (trunc be.lessonsAndConventions)
                              "plan", box (trunc be.plan) ]

                        createObj fields)
                    |> List.toArray

                [ frontMatterRoot (box entries) ]

        renderCompactionAnchorPrompt (anchorBlocks @ backlogBlock)

let compactingTransform (messages: Message<'raw> list) (backlog: BacklogEntry list) : Message<'raw> list =
    let cleaned = stripSyntheticBySource messages

    let todoSummary =
        if backlog.IsEmpty then ""
        else
            let sb = System.Text.StringBuilder()
            sb.AppendLine("Todo Backlog Summary:") |> ignore
            for entry in backlog do
                sb.AppendLine("## Backlog Entry") |> ignore
                if not (System.String.IsNullOrWhiteSpace entry.plan) then
                    sb.AppendLine("- Plan: " + entry.plan.Trim()) |> ignore
                if not (System.String.IsNullOrWhiteSpace entry.ahaMoments) then
                    sb.AppendLine("- Aha Moments: " + entry.ahaMoments.Trim()) |> ignore
                if not (System.String.IsNullOrWhiteSpace entry.changesAndReasons) then
                    sb.AppendLine("- Changes & Reasons: " + entry.changesAndReasons.Trim()) |> ignore
                if not (System.String.IsNullOrWhiteSpace entry.gotchas) then
                    sb.AppendLine("- Gotchas: " + entry.gotchas.Trim()) |> ignore
                if not (System.String.IsNullOrWhiteSpace entry.lessonsAndConventions) then
                    sb.AppendLine("- Lessons & Conventions: " + entry.lessonsAndConventions.Trim()) |> ignore
            sb.ToString()

    let messageHistory =
        cleaned
        |> List.map (fun m ->
            let roleStr =
                match m.info.role with
                | User -> "User"
                | Assistant -> "Assistant"
                | ToolResult -> "Tool Result"
                | System -> "System"
            let contentPartsText =
                m.parts
                |> List.choose (fun p ->
                    match p with
                    | TextPart t -> Some t
                    | ToolPart(name, callID, state, err) ->
                        let stateStr =
                            match state with
                            | Some s -> $"status={s.status}"
                            | None -> ""
                        Some $"Tool Call: name={name}, callID={callID}, state={stateStr}, error={err}"
                    | RawPart _ -> Some "[Raw Content]")
                |> String.concat "\n"
            $"Role: {roleStr}\nContent:\n{contentPartsText}\n")
        |> String.concat "\n"

    let bodyText =
        let parts = [
            if todoSummary <> "" then yield todoSummary
            yield "Dialogue History:"
            yield messageHistory
        ]
        String.concat "\n\n" parts

    let wrappedText =
        "Please summarize the conversation history and progress based on the following do-not-exec block. <do-not-exec>\n"
        + bodyText
        + "\n</do-not-exec> Note that you only need to provide a summary of progress, and should not actually execute the content within."

    let defaultRaw =
        match messages with
        | m :: _ -> m.raw
        | [] -> Unchecked.defaultof<'raw>

    let defaultTime =
        match messages with
        | m :: _ -> m.info.time
        | [] -> Unchecked.defaultof<'raw>

    let defaultDetails =
        match messages with
        | m :: _ -> m.info.details
        | [] -> Unchecked.defaultof<'raw>

    let finalMsg =
        { info =
            { id = "compacting-summary-" + System.Guid.NewGuid().ToString()
              sessionID = extractSessionID messages
              role = User
              agent = "orchestrator"
              isError = false
              toolName = ""
              details = defaultDetails
              time = defaultTime }
          parts = [ TextPart wrappedText ]
          source = Synthetic "compacting-summary-"
          raw = defaultRaw }

    [ finalMsg ]
