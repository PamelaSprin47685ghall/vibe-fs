module VibeFs.Omp.OmpToolSchema

open Fable.Core.JsInterop
open VibeFs.Kernel.WorkBacklog
open VibeFs.Kernel.Methodology
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.ToolCatalog
open VibeFs.Omp.Schema
open VibeFs.Methodology.SchemaCommon

module Params = VibeFs.Kernel.ToolCatalog.Params

let private methodologyField (f: MethodologyField) (tb: obj) : string * obj =
    match f.kind, f.required, f.minArrayItems with
    | FieldKind.String, _, _ -> f.name, str f.description tb
    | FieldKind.StringArray, true, n when n > 0 ->
        f.name, arrayMin (str "" tb) n f.description tb
    | FieldKind.StringArray, true, _ ->
        f.name, strArray f.description tb
    | FieldKind.StringArray, _, _ -> f.name, opt f.description tb (fun desc t -> strArray desc t)

let methodologyParameters (schema: MethodologySchema) (tb: obj) : obj =
    objectOf (schema.fields |> List.map (fun f -> methodologyField f tb) |> Array.ofList) tb

let private coderIntentItem (tb: obj) : obj =
    let targetShape =
        objectOf
            [|
                ("file", str coderTargetFileDesc tb)
                ("guide", str coderTargetGuideDesc tb)
                ("draft", opt coderTargetDraftDesc tb str)
            |]
            tb
    objectOf
        [|
            ("objective", str coderObjectiveDesc tb)
            ("background", str coderBackgroundDesc tb)
            ("do_not_touch", opt coderDoNotTouchDesc tb (fun _ tb -> strArray coderDoNotTouchItemDesc tb))
            ("targets", arrayOf targetShape coderTargetsDesc tb)
        |]
        tb

let coderParameters (tb: obj) : obj =
    objectOf
        [|
            ("intents", arrayOf (coderIntentItem tb) Params.coderIntents tb)
            ("tdd", enumOf [| "red"; "green" |] Params.coderTdd tb)
        |]
        tb

let private investigatorIntentItem (tb: obj) : obj =
    objectOf
        [|
            ("objective", str investigatorObjectiveDesc tb)
            ("background", str investigatorBackgroundDesc tb)
            ("questions", strArray investigatorQuestionsDesc tb)
            ("entries", opt investigatorEntriesDesc tb (fun _ tb -> strArray investigatorEntryItemDesc tb))
        |]
        tb

let investigatorParameters (tb: obj) : obj =
    objectOf [| ("intents", arrayOf (investigatorIntentItem tb) Params.investigatorIntents tb) |] tb

let meditatorParameters (tb: obj) : obj =
    objectOf
        [|
            ("intent", str Params.meditatorIntent tb)
            ("files", strArray Params.meditatorFiles tb)
        |]
        tb

let browserParameters (tb: obj) : obj =
    objectOf [| ("intent", str Params.browserIntent tb) |] tb

let executorParameters (tb: obj) : obj =
    objectOf
        [|
            ("language", opt Params.executorLanguage tb (fun desc tb -> enumOf [| "shell"; "python"; "javascript" |] desc tb))
            ("program", str Params.executorProgram tb)
            ("dependencies", opt Params.executorDeps tb (fun desc tb -> strArray desc tb))
            ("timeout_type", enumOf [| "short"; "long"; "last-resort" |] Params.executorTimeout tb)
            ("mode", enumOf [| "ro"; "rw" |] Params.executorMode tb)
            ("what_to_summarize", opt "Optional summary focus for long executor output." tb str)
        |]
        tb

let returnReviewerParameters (tb: obj) : obj =
    objectOf
        [|
            ("verdict", enumOf [| "PASS"; "REJECT" |] "PASS to accept, REJECT to reject" tb)
            ("feedback", opt "Detailed, actionable feedback when rejecting; omit when passing." tb str)
        |]
        tb

let todowriteParameters (tb: obj) : obj =
    let todoItem =
        objectOf
            [|
                ("content", str todoContentDesc tb)
                ("status", str todoStatusDesc tb)
                ("priority", str todoPriorityDesc tb)
            |]
            tb
    objectOf
        [|
            ("todos", arrayOf todoItem todosDesc tb)
            ("completedWorkReport", str reportDesc tb)
            ("select_methodology", arrayOf (enumOf (methodologyEnumValues |> List.toArray) "Methodology name" tb) selectMethodologyFieldDescription tb)
        |]
        tb