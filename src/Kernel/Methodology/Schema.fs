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

let renderMeditatorDocument (entry: MethodologyEntry) (intentText: string) (backgroundText: string) (noteText: string) : PromptDocumentView =
    let sections =
        entry.outputSections
        |> List.mapi (fun i s -> $"{i + 1}. {s}")
        |> String.concat "\n"

    let bg =
        [ $"Methodology: {entry.methodologyId}"
          $"Definition: {entry.shortDefinition}"
          $"Use when: {entry.triggerWhen}"
          $"Background: {backgroundText.Trim()}" ]
        |> String.concat "\n"

    { objective = intentText.Trim()
      background = Some bg
      agentRole = AgentRole.MethodologyReasoning
      targets = [ PromptTarget.EvidenceTarget("methodology_note", noteText.Trim()) ]
      boundaries = []
      rules =
        [ PromptRule.Policy $"Apply the role of {entry.meditatorRole}."
          PromptRule.Policy $"Structure your report using these sections:\n{sections}"
          PromptRule.Constraint "Produce dense modern Chinese unless the inputs are explicitly English-only."
          PromptRule.Constraint "Do NOT call tools or invent workspace facts." ]
      outcomes =
        [ { label = "report"
            text = "Use every methodology output section and end with concrete next actions." } ] }
