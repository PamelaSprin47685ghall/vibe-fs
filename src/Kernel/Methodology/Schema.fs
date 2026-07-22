module Wanxiangshu.Kernel.Methodology.Schema

open System
open System.Text
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Kernel.Methodology.NoteSections

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

let renderMeditatorDocument
    (entry: MethodologyEntry)
    (intentText: string)
    (backgroundText: string)
    (noteText: string)
    : PromptDocumentView =
    let turnBackground =
        let trimmed = backgroundText.Trim()

        if System.String.IsNullOrWhiteSpace trimmed then
            None
        else
            Some trimmed

    let meta: MethodologyMeta =
        { id = entry.methodologyId
          definition = entry.shortDefinition
          trigger = entry.triggerWhen
          role = entry.meditatorRole
          noteSections = splitNoteSections entry.noteDescription noteText }

    let sectionOutcomes =
        entry.outputSections
        |> List.mapi (fun i s ->
            { label = sprintf "section_%d" (i + 1)
              text = s })

    let outcomes =
        sectionOutcomes
        @ [ { label = "report"
              text = "Use every methodology output section and end with concrete next actions." } ]

    { objective = intentText.Trim()
      background = turnBackground
      agentRole = AgentRole.MethodologyReasoning
      targets = [ PromptTarget.MethodologyTarget meta ]
      boundaries = []
      rules =
        [ PromptRule.Constraint "LANGUAGE: dense modern Chinese unless inputs are explicitly English-only."
          PromptRule.Constraint "NO_TOOLS: do not call tools or invent workspace facts." ]
      outcomes = outcomes }
