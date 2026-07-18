module Wanxiangshu.Hosts.Omp.OmpToolSchema

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.Methodology.Api
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Hosts.Omp.Schema
open Wanxiangshu.Kernel.Methodology.Schema
open Wanxiangshu.Kernel.Methodology.Registry

module Params = Wanxiangshu.Kernel.ToolCatalog.Params

let private addRequired (schema: obj) (key: string) : unit =
    let existing = Dyn.get schema "required"

    if Dyn.isArray existing then
        existing?("push") (box key) |> ignore
    else
        schema?("required") <- box [| box key |]

let private injectWarnReuseIntoOmpParameters (schema: obj) (toolName: string) : obj =
    if isSubagentTool toolName then
        let props = Dyn.get schema "properties"

        if not (isNullish props) then
            if isNullish (Dyn.get props "warn_reuse") then
                props?("warn_reuse") <-
                    box (
                        createObj
                            [| "type", box "string"
                               "description", box warnReuseDescription
                               "required_", box true |]
                    )
            else
                let prop = Dyn.get props "warn_reuse"

                if not (isNullish prop) then
                    prop?("required_") <- true

    schema

let meditatorParameters (tb: obj) : obj =
    objectOf
        [| ("methodology",
            enumOf
                (Wanxiangshu.Kernel.Methodology.Registry.enumValuesArray.Value)
                "Select which methodology to apply."
                tb)
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
        let props = Dyn.get schema "properties"

        if isNullish (Dyn.get props "warn_tdd") then
            props?("warn_tdd") <-
                box (
                    createObj
                        [| "type", box "string"
                           "description", box warnTddDescription
                           "required_", box true |]
                )
        else
            let prop = Dyn.get props "warn_tdd"

            if not (isNullish prop) then
                prop?("required_") <- true

    injectWarnReuseIntoOmpParameters schema "coder"

let private inspectorIntentItem (tb: obj) : obj =
    objectOf
        [| ("objective", str inspectorObjectiveDesc tb)
           ("background", str inspectorBackgroundDesc tb)
           ("questions", strArray inspectorQuestionsDesc tb)
           ("entries", opt inspectorEntriesDesc tb (fun _ tb -> strArray inspectorEntryItemDesc tb)) |]
        tb

let inspectorParameters (tb: obj) : obj =
    injectWarnReuseIntoOmpParameters
        (objectOf [| ("intents", arrayOf (inspectorIntentItem tb) Params.inspectorIntents tb) |] tb)
        "inspector"

let browserParameters (tb: obj) : obj =
    injectWarnReuseIntoOmpParameters (objectOf [| ("intent", str Params.browserIntent tb) |] tb) "browser"

let continueParameters (tb: obj) : obj =
    objectOf
        [| ("iterator",
            str
                "The iterator ID representing the target subagent session (usually returned in the front matter of a previous subagent run)."
                tb)
           ("prompt", str "The new query, instructions, or follow-up question to send to the subagent session." tb) |]
        tb

let executorParameters (tb: obj) : obj =
    let schema =
        objectOf
            [| ("language",
                optWithDefault Params.executorLanguage tb "shell" (fun desc tb ->
                    enumOf [| "shell"; "python"; "javascript" |] desc tb))
               ("command", str Params.executorCommand tb)
               ("dependencies", opt Params.executorDeps tb (fun desc tb -> strArray desc tb))
               ("timeout_type", enumOf [| "short"; "long" |] Params.executorTimeout tb)
               ("mode", enumOf [| "ro"; "rw" |] Params.executorMode tb)
               ("what_to_summarize", str Params.executorWhatToSummarize tb)
               ("max_bytes", num Params.executorMaxBytes tb) |]
            tb

    if isModificationTool "executor" then
        let props = Dyn.get schema "properties"

        if isNullish (Dyn.get props "warn_tdd") then
            props?("warn_tdd") <-
                box (
                    createObj
                        [| "type", box "string"
                           "description", box warnTddDescription
                           "required_", box true |]
                )
        else
            let prop = Dyn.get props "warn_tdd"

            if not (isNullish prop) then
                prop?("required_") <- true

    if isWarnRequiredTool "executor" then
        let props = Dyn.get schema "properties"

        if isNullish (Dyn.get props "warn") then
            props?("warn") <-
                box (
                    createObj
                        [| "type", box "string"
                           "description", box warnDescription
                           "required_", box true |]
                )
        else
            let prop = Dyn.get props "warn"

            if not (isNullish prop) then
                prop?("required_") <- true

    addRequired schema "max_bytes"
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
           ("ahaMoments", opt ahaMomentsDesc tb str)
           ("changesAndReasons", opt changesAndReasonsDesc tb str)
           ("gotchas", opt gotchasDesc tb str)
           ("lessonsAndConventions", opt lessonsAndConventionsDesc tb str)
           ("plan", opt planDesc tb str)
           ("select_methodology",
            arrayOf
                (enumOf Wanxiangshu.Kernel.Methodology.Registry.enumValuesArray.Value "Methodology name" tb)
                selectMethodologyFieldDescription
                tb) |]
        tb
