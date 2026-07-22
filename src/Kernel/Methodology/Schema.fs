module Wanxiangshu.Kernel.Methodology.Schema

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
    + "The note field MUST be structured as key: value sections matching the methodology's noteDescription tokens "
    + "(one section per key, e.g. problem_statement: ...). Freeform prose without keys is rejected for structured projection."

let buildUnifiedNoteDescription (entries: MethodologyEntry list) : string =
    let header =
        "Fill structured note sections for the selected methodology. "
        + "Write each token from the list as its own `key: text` section (not a single freeform body). "
        + "Per methodology:"

    let lines =
        entries
        |> List.map (fun e -> e.methodologyId + ": " + e.noteDescription)

    String.concat "\n" (header :: "" :: lines)

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
        [ PromptRule.Constraint "Write in dense modern Chinese unless inputs are explicitly English-only."
          PromptRule.Constraint "Do not call tools or invent workspace facts." ]
      outcomes = outcomes }
