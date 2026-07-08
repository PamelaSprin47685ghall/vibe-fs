module Wanxiangshu.Omp.OmpToolSchema

open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Omp.Schema
open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Registry

module Params = Wanxiangshu.Kernel.ToolCatalog.Params

let private addRequired (schema: obj) (key: string) : unit =
    let existing = Dyn.get schema "required"

    if Dyn.isArray existing then
        existing?("push") (box key) |> ignore
    else
        schema?("required") <- box [| box key |]

let methodologyParameters (tb: obj) : obj =
    objectOf
        [| ("methodology",
            enumOf (Wanxiangshu.Methodology.Registry.enumValuesArray.Value) "Select which methodology to apply." tb)
           ("intent", str intentFieldDescription tb)
           ("background", str backgroundFieldDescription tb)
           ("note", str unifiedNoteDescription.Value tb) |]
        tb

let private coderIntentItem (tb: obj) : obj =
    let targetShape =
        objectOf
            [| ("file", str coderTargetFileDesc tb)
               ("guide", str coderTargetGuideDesc tb)
               ("draft", opt coderTargetDraftDesc tb str) |]
            tb

    objectOf
        [| ("objective", str coderObjectiveDesc tb)
           ("background", str coderBackgroundDesc tb)
           ("do_not_touch", opt coderDoNotTouchDesc tb (fun _ tb -> strArray coderDoNotTouchItemDesc tb))
           ("targets", arrayOf targetShape coderTargetsDesc tb) |]
        tb

let coderParameters (tb: obj) : obj =
    let schema =
        objectOf
            [| ("intents", arrayOf (coderIntentItem tb) Params.coderIntents tb)
               ("tdd", enumOf [| "red"; "green" |] Params.coderTdd tb) |]
            tb

    if isModificationTool "coder" then
        addRequired schema "warn_tdd"
        let props = Dyn.get schema "properties"

        if isNullish (Dyn.get props "warn_tdd") then
            props?("warn_tdd") <-
                box (
                    createObj
                        [| "type", box "string"
                           "enum", box [| box canonicalValue |]
                           "description", box warnTddDescription |]
                )

    schema

let private investigatorIntentItem (tb: obj) : obj =
    objectOf
        [| ("objective", str investigatorObjectiveDesc tb)
           ("background", str investigatorBackgroundDesc tb)
           ("questions", strArray investigatorQuestionsDesc tb)
           ("entries", opt investigatorEntriesDesc tb (fun _ tb -> strArray investigatorEntryItemDesc tb)) |]
        tb

let investigatorParameters (tb: obj) : obj =
    objectOf [| ("intents", arrayOf (investigatorIntentItem tb) Params.investigatorIntents tb) |] tb

let meditatorParameters (tb: obj) : obj =
    objectOf
        [| ("intent", str Params.meditatorIntent tb)
           ("files", strArray Params.meditatorFiles tb) |]
        tb

let browserParameters (tb: obj) : obj =
    objectOf [| ("intent", str Params.browserIntent tb) |] tb

let executorParameters (tb: obj) : obj =
    let schema =
        objectOf
            [| ("language",
                optWithDefault Params.executorLanguage tb "shell" (fun desc tb ->
                    enumOf [| "shell"; "python"; "javascript" |] desc tb))
               ("program", str Params.executorProgram tb)
               ("dependencies", opt Params.executorDeps tb (fun desc tb -> strArray desc tb))
               ("timeout_type", enumOf [| "short"; "long" |] Params.executorTimeout tb)
               ("mode", enumOf [| "ro"; "rw" |] Params.executorMode tb)
               ("what_to_summarize", str Params.executorWhatToSummarize tb) |]
            tb

    if isModificationTool "executor" then
        addRequired schema "warn_tdd"
        let props = Dyn.get schema "properties"

        if isNullish (Dyn.get props "warn_tdd") then
            props?("warn_tdd") <-
                box (
                    createObj
                        [| "type", box "string"
                           "enum", box [| box canonicalValue |]
                           "description", box warnTddDescription |]
                )

    if isWarnRequiredTool "executor" then
        addRequired schema "warn"
        let props = Dyn.get schema "properties"

        if isNullish (Dyn.get props "warn") then
            props?("warn") <-
                box (
                    createObj
                        [| "type", box "string"
                           "enum", box [| box warnCanonicalValue |]
                           "description", box warnDescription |]
                )

    addRequired schema "what_to_summarize"
    schema

let returnReviewerParameters (tb: obj) : obj =
    objectOf
        [| ("verdict", enumOf [| "PERFECT"; "REVISE" |] "PERFECT to accept, REVISE to request revision" tb)
           ("feedback", opt "Detailed, actionable feedback when requesting revision; omit when passing." tb str) |]
        tb

let todowriteParameters (tb: obj) : obj =
    let todoItem =
        objectOf
            [| ("content", str todoContentDesc tb)
               ("status", str todoStatusDesc tb)
               ("priority", str todoPriorityDesc tb) |]
            tb

    objectOf
        [| ("todos", arrayOf todoItem todosDesc tb)
           ("ahaMoments", str ahaMomentsDesc tb)
           ("changesAndReasons", str changesAndReasonsDesc tb)
           ("gotchas", str gotchasDesc tb)
           ("lessonsAndConventions", str lessonsAndConventionsDesc tb)
           ("plan", str planDesc tb)
           ("select_methodology",
            arrayOf
                (enumOf Wanxiangshu.Methodology.Registry.enumValuesArray.Value "Methodology name" tb)
                selectMethodologyFieldDescription
                tb) |]
        tb
