module VibeFs.Kernel.SubagentIntents

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

type CoderTarget = { file: string; guide: string }

type CoderIntent =
    { objective: string
      background: string
      targets: CoderTarget list }

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

let private parseCoderTargets (targets: obj) : Result<CoderTarget list, string> =
    if Dyn.isNullish targets || not (Dyn.isArray targets) then
        Result.Error "Invalid LLM input for coder: targets must be a non-empty array"
    else
        let arr = targets :?> obj array
        if arr.Length = 0 then Result.Error "Invalid LLM input for coder: targets must be a non-empty array"
        else
            let mutable err = None
            let results = ResizeArray<CoderTarget>()
            for t in arr do
                if err.IsNone then
                    if not (Dyn.typeIs t "object") then
                        err <- Some "Invalid LLM input for coder: each target must be an object with file and guide"
                    else
                        let file = Dyn.str t "file"
                        let guide = Dyn.str t "guide"
                        if file = "" then err <- Some "Invalid LLM input for coder: each target requires file"
                        elif guide = "" then err <- Some "Invalid LLM input for coder: each target requires guide"
                        else results.Add({ file = file; guide = guide })
            match err with
            | Some m -> Result.Error m
            | None -> Result.Ok (results |> Seq.toList)

let parseCoderIntent (item: obj) : Result<CoderIntent, string> =
    if not (Dyn.typeIs item "object") then Result.Error "Invalid LLM input for coder: each intent must be an object"
    else
        requireNonEmpty "objective" (Dyn.str item "objective") "coder"
        |> Result.bind (fun objective ->
            requireNonEmpty "background" (Dyn.str item "background") "coder"
            |> Result.bind (fun background ->
                parseCoderTargets (Dyn.get item "targets")
                |> Result.map (fun targets ->
                    { objective = objective; background = background; targets = targets })))

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
        else
            let mutable err = None
            let parsed = ResizeArray<CoderIntent>()
            for item in arr do
                if err.IsNone then
                    match parseCoderIntent item with
                    | Ok i -> parsed.Add i
                    | Error m -> err <- Some m
            match err with
            | Some m -> Result.Error m
            | None -> Result.Ok (parsed |> Seq.toList)

let parseInvestigatorIntents (intents: obj) : Result<InvestigatorIntent list, string> =
    if not (Dyn.isArray intents) then Result.Error "Invalid LLM input for investigator: intents must be an array"
    else
        let arr = intents :?> obj array
        if arr.Length = 0 then Result.Error "Invalid LLM input for investigator: intents must be a non-empty array"
        else
            let mutable err = None
            let parsed = ResizeArray<InvestigatorIntent>()
            for item in arr do
                if err.IsNone then
                    match parseInvestigatorIntent item with
                    | Ok i -> parsed.Add i
                    | Error m -> err <- Some m
            match err with
            | Some m -> Result.Error m
            | None -> Result.Ok (parsed |> Seq.toList)

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
            (createObj [ "file", muxStrReq "File path to modify."; "guide", muxStrReq "Implementation constraints for this file." ])
            [| "file"; "guide" |]
    let intentItem =
        muxObjectSchema
            (createObj
                [ "objective", muxStrReq "Concrete code-change goal for this subagent."
                  "background", muxStrReq "Why this change is needed; prior findings and user context."
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