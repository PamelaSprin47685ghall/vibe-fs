module VibeFs.Kernel.SubagentIntents

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

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

let private requireNonEmpty (field: string) (value: string) (tool: string) : Result<string, string> =
    if System.String.IsNullOrWhiteSpace value then Result.Error $"Invalid LLM input for {tool}: {field} is required"
    else Result.Ok value

let private optionalStrArray (o: obj) (key: string) : string array =
    let v = Dyn.get o key
    if Dyn.isNullish v || not (Dyn.isArray v) then [||]
    else v :?> obj array |> Array.map string

let private foldArrayResult<'T> (decode: obj -> Result<'T, string>) (arr: obj array) : Result<'T list, string> =
    arr
    |> Array.fold
        (fun acc item ->
            acc
            |> Result.bind (fun xs -> decode item |> Result.map (fun x -> x :: xs)))
        (Ok [])
    |> Result.map List.rev

let private decodeCoderTarget (t: obj) : Result<CoderTarget, string> =
    if not (Dyn.typeIs t "object") then
        Result.Error "Invalid LLM input for coder: each target must be an object with file and guide"
    else
        let file = Dyn.str t "file"
        let guide = Dyn.str t "guide"
        let draft =
            match Dyn.opt t "draft" with
            | Some value ->
                let text = string value
                if System.String.IsNullOrWhiteSpace text then None else Some text
            | None -> None
        if file = "" then Result.Error "Invalid LLM input for coder: each target requires file"
        elif guide = "" then Result.Error "Invalid LLM input for coder: each target requires guide"
        else Result.Ok { file = file; guide = guide; draft = draft }

let private parseCoderTargets (targets: obj) : Result<CoderTarget list, string> =
    if Dyn.isNullish targets || not (Dyn.isArray targets) then
        Result.Error "Invalid LLM input for coder: targets must be a non-empty array"
    else
        let arr = targets :?> obj array
        if arr.Length = 0 then Result.Error "Invalid LLM input for coder: targets must be a non-empty array"
        else foldArrayResult decodeCoderTarget arr

let parseCoderIntent (item: obj) : Result<CoderIntent, string> =
    if not (Dyn.typeIs item "object") then Result.Error "Invalid LLM input for coder: each intent must be an object"
    else
        requireNonEmpty "objective" (Dyn.str item "objective") "coder"
        |> Result.bind (fun objective ->
            requireNonEmpty "background" (Dyn.str item "background") "coder"
            |> Result.bind (fun background ->
                parseCoderTargets (Dyn.get item "targets")
                |> Result.map (fun targets ->
                    { objective = objective
                      background = background
                      targets = targets
                      doNotTouch = optionalStrArray item "do_not_touch" })))

let parseInvestigatorIntent (item: obj) : Result<InvestigatorIntent, string> =
    if not (Dyn.typeIs item "object") then Result.Error "Invalid LLM input for investigator: each intent must be an object"
    else
        requireNonEmpty "objective" (Dyn.str item "objective") "investigator"
        |> Result.bind (fun objective ->
            requireNonEmpty "background" (Dyn.str item "background") "investigator"
            |> Result.bind (fun background ->
                let questions = optionalStrArray item "questions"
                if questions.Length = 0 then
                    Result.Error "Invalid LLM input for investigator: questions must be a non-empty string array"
                else
                    Result.Ok
                        { objective = objective
                          background = background
                          questions = questions
                          entries = optionalStrArray item "entries" }))

let parseCoderIntents (intents: obj) : Result<CoderIntent list, string> =
    if not (Dyn.isArray intents) then Result.Error "Invalid LLM input for coder: intents must be an array"
    else
        let arr = intents :?> obj array
        if arr.Length = 0 then Result.Error "Invalid LLM input for coder: intents must be a non-empty array"
        else foldArrayResult parseCoderIntent arr

let parseInvestigatorIntents (intents: obj) : Result<InvestigatorIntent list, string> =
    if not (Dyn.isArray intents) then Result.Error "Invalid LLM input for investigator: intents must be an array"
    else
        let arr = intents :?> obj array
        if arr.Length = 0 then Result.Error "Invalid LLM input for investigator: intents must be a non-empty array"
        else foldArrayResult parseInvestigatorIntent arr

let joinCoderUiLabel (intents: obj) : Result<string, string> =
    parseCoderIntents intents |> Result.map (fun list -> list |> List.map (fun i -> i.objective) |> String.concat "; ")

let joinInvestigatorUiLabel (intents: obj) : Result<string, string> =
    parseInvestigatorIntents intents |> Result.map (fun list -> list |> List.map (fun i -> i.objective) |> String.concat "; ")

let coderTargetFiles (intent: CoderIntent) : string list = intent.targets |> List.map (fun t -> t.file)

let private muxStrReq (desc: string) : obj =
    createObj [ "type", box "string"; "minLength", box 1; "description", box desc ]

let private muxStrArrayReq (desc: string) : obj =
    createObj
        [ "type", box "array"
          "minItems", box 1
          "items", createObj [ "type", box "string"; "minLength", box 1 ]
          "description", box desc ]

let private muxStrArrayOpt (desc: string) : obj =
    createObj
        [ "type", box "array"
          "items", createObj [ "type", box "string"; "minLength", box 1 ]
          "description", box desc ]

let private muxObjectSchema (properties: obj) (required: string array) : obj =
    createObj
        [ "type", box "object"
          "properties", properties
          "required", box required
          "additionalProperties", box false ]

let muxCoderIntentsSchema (intentsDesc: string) : obj =
    let targetItem =
        muxObjectSchema
            (createObj [ "file", muxStrReq "File path to modify."
                         "guide", muxStrReq "Implementation constraints for this file."
                         "draft", createObj [ "type", box "string"; "description", box "Optional minimal draft for the coder to reference. Prefer leaving this empty; use only when strict quality needs a concrete sketch. No patch or special format required." ] ])
            [| "file"; "guide" |]
    let intentItem =
        muxObjectSchema
            (createObj
                [ "objective", muxStrReq "Concrete code-change goal for this subagent."
                  "background", muxStrReq "Why this change is needed; prior findings and user context."
                  "do_not_touch", muxStrArrayOpt "Optional list of files, directories, symbols, or constraints this subagent must not modify."
                  "targets",
                  createObj
                      [ "type", box "array"
                        "minItems", box 1
                        "items", targetItem
                        "description", box "Non-empty per-file implementation guides." ] ])
            [| "objective"; "background"; "targets" |]
    createObj
        [ "type", box "array"
          "minItems", box 1
          "items", intentItem
          "description", box intentsDesc ]

let muxInvestigatorIntentsSchema (intentsDesc: string) : obj =
    let intentItem =
        muxObjectSchema
            (createObj
                [ "objective", muxStrReq "What to investigate in the codebase."
                  "background", muxStrReq "Why this investigation is needed; blockers and prior context."
                  "questions", muxStrArrayReq "Non-empty list of questions the report must answer explicitly."
                  "entries", muxStrArrayOpt "Optional entry paths, symbols, or files to start from." ])
            [| "objective"; "background"; "questions" |]
    createObj
        [ "type", box "array"
          "minItems", box 1
          "items", intentItem
          "description", box intentsDesc ]
