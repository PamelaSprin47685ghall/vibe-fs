module Wanxiangshu.Kernel.SubagentIntents


type CoderTarget =
    { file: string
      guide: string
      draft: string option }

type CoderIntent =
    { objective: string
      background: string
      targets: CoderTarget list
      doNotTouch: string array }

type InvestigatorIntent =
    { objective: string
      background: string
      questions: string array
      entries: string array }

let coderTargetFileDesc = "File path to modify."
let coderTargetGuideDesc = "Implementation constraints for this file."
let coderTargetDraftDesc = "Optional minimal draft for the coder to reference. Prefer leaving this empty; use only when strict quality needs a concrete sketch. No patch or special format required."
let coderTargetsDesc = "Non-empty per-file implementation guides."
let coderObjectiveDesc = "Concrete code-change goal for this item."
let coderBackgroundDesc = "Why this change is needed; prior findings and user context."
let coderDoNotTouchItemDesc = "Do-not-touch path, symbol, or constraint."
let coderDoNotTouchDesc = "Optional list of files, directories, symbols, or constraints this item must not modify."
let investigatorQuestionItemDesc = "Question the report must answer."
let investigatorQuestionsDesc = "Non-empty list of questions the report must answer explicitly."
let investigatorEntryItemDesc = "Optional entry path, symbol, or file."
let investigatorEntriesDesc = "Optional entry paths, symbols, or files to start from."
let investigatorObjectiveDesc = "What to investigate in the codebase."
let investigatorBackgroundDesc = "Why this investigation is needed; blockers and prior context."

let coderTargetFiles (intent: CoderIntent) : string list = intent.targets |> List.map (fun t -> t.file)

