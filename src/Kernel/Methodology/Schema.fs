module Wanxiangshu.Kernel.Methodology.Schema

open System
open System.Text

type MethodologyEntry =
    { methodologyId: string
      shortDefinition: string
      triggerWhen: string
      noteDescription: string
      meditatorRole: string
      outputSections: string list }

let notebookRecommendedWords = 512

let intentFieldDescription =
    "Mandatory statement of the fundamental intent this methodology must serve on this turn. Aim for about "
    + string notebookRecommendedWords
    + " words or more when helpful; there is no minimum word count. Explain what root problem or decision you are using this methodology to crack—not a task checklist, but the underlying why (e.g. why first-principles rebuild instead of patching, why abduction instead of blame). Tie intent to user goals, failure symptoms, and what success would unblock. Do not paste generic methodology lectures."

let backgroundFieldDescription =
    "Mandatory notebook context for this methodology note. Aim for about "
    + string notebookRecommendedWords
    + " words or more when helpful; there is no minimum word count. Include: current task objective and acceptance criteria; relevant repository paths and symbols; prior attempts and outcomes; constraints from AGENTS.md, README, PRD, or user messages; open questions; risks; and how this methodology should frame the next work step. Do not paste tool catalogs or generic methodology essays—anchor every paragraph to this workspace and this turn."

let unifiedToolName = "meditator"

let unifiedToolDescription =
    "Record a durable, structured methodology notebook entry for this workspace and turn. "
    + "Select a methodology from the enum, then fill intent, background, and note. "
    + "The note field description lists what each methodology expects you to cover."

let buildUnifiedNoteDescription (entries: MethodologyEntry list) : string =
    let sb = StringBuilder()

    sb.AppendLine(
        "Fill in the content for the selected methodology. Depending on which methodology you choose, your note should cover:"
    )
    |> ignore

    sb.AppendLine() |> ignore

    for e in entries do
        sb.AppendLine(e.methodologyId + ": " + e.noteDescription) |> ignore

    sb.ToString()

let renderMeditatorIntent (entry: MethodologyEntry) (intentText: string) (backgroundText: string) (noteText: string) =
    let sections =
        entry.outputSections
        |> List.mapi (fun i s -> $"{i + 1}. {s}")
        |> String.concat "\n"

    $"""You are applying the "{entry.methodologyId}" methodology.

Definition: {entry.shortDefinition}
Use when: {entry.triggerWhen}

Role: {entry.meditatorRole}

Intent: {intentText}

Background: {backgroundText}

Note: {noteText}

Produce the tool output in dense modern Chinese unless the inputs are explicitly English-only. Structure your answer with these sections:
{sections}

Do not call tools. Do not propose code edits unless the inputs ask for implementation plans. End with concrete next actions the parent can execute without you.

You are in a quiet room with the question.
No tools (except the read tool to view files), no distractions — just you and the problem.
Read carefully. Turn it over in your mind.
When you are ready, answer with clarity and depth."""
