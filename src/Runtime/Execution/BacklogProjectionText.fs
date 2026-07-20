module Wanxiangshu.Runtime.BacklogProjectionText

open Wanxiangshu.Kernel.Backlog.BacklogTypes
open Wanxiangshu.Runtime.PromptFrontMatter

type CompletionItem =
    { user_message: string[]
      aha_moments: string
      changes_and_reasons: string
      gotchas: string
      lessons_and_conventions: string
      plan: string }

let private completedWorkItem (userPrompts: string list) (entry: BacklogEntry) : CompletionItem =
    let userMsgField =
        if userPrompts.IsEmpty then
            [||]
        else
            userPrompts |> List.map (fun text -> text.Trim()) |> List.toArray

    { user_message = userMsgField
      aha_moments = trunc entry.ahaMoments
      changes_and_reasons = trunc entry.changesAndReasons
      gotchas = trunc entry.gotchas
      lessons_and_conventions = trunc entry.lessonsAndConventions
      plan = trunc entry.plan }

let private projectionRootValue (backlog: BacklogEntry list) (userPrompts: string list) : CompletionItem[] =
    backlog |> List.map (completedWorkItem userPrompts) |> List.toArray

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
