module VibeFs.Kernel.SubagentIntents

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

let coderTargetFileDesc = "File path to modify."
let coderTargetGuideDesc = "Implementation constraints for this file."
let coderTargetDraftDesc = "Optional minimal draft for the coder to reference. Prefer leaving this empty; use only when strict quality needs a concrete sketch. No patch or special format required."
let coderTargetsDesc = "Non-empty per-file implementation guides."
let coderObjectiveDesc = "Concrete code-change goal for this subagent."
let coderBackgroundDesc = "Why this change is needed; prior findings and user context."
let coderDoNotTouchItemDesc = "Do-not-touch path, symbol, or constraint."
let coderDoNotTouchDesc = "Optional list of files, directories, symbols, or constraints this subagent must not modify."
let investigatorQuestionItemDesc = "Question the report must answer."
let investigatorQuestionsDesc = "Non-empty list of questions the report must answer explicitly."
let investigatorEntryItemDesc = "Optional entry path, symbol, or file."
let investigatorEntriesDesc = "Optional entry paths, symbols, or files to start from."
let investigatorObjectiveDesc = "What to investigate in the codebase."
let investigatorBackgroundDesc = "Why this investigation is needed; blockers and prior context."

let private requireNonEmpty (field: string) (value: string) (tool: string) : Result<string, string> =
    if System.String.IsNullOrWhiteSpace value then Result.Error $"Invalid LLM input for {tool}: {field} is required"
    else Result.Ok value

let private optionalStrArray (o: obj) (key: string) : string array =
    let v = Dyn.get o key
    if Dyn.isNullish v || not (Dyn.isArray v) then [||]
    else v :?> obj array |> Array.map string

/// Applicative `apply` for Result. Turns a multi-field decoder from a nested
/// `Result.bind` pyramid into a flat left-to-right pipeline of field bindings
/// (P18/P19): each field is decoded once into a `Result`, then combined via
/// `<*>` rather than threaded through callbacks.
let private ( <*> ) (f: Result<'a -> 'b, string>) (x: Result<'a, string>) : Result<'b, string> =
    match f, x with
    | Ok g, Ok v -> Ok (g v)
    | Error e, _ -> Error e
    | _, Error e -> Error e

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
        let fileResult =
            if file = "" then Result.Error "Invalid LLM input for coder: each target requires file"
            else Result.Ok file
        let guideResult =
            if guide = "" then Result.Error "Invalid LLM input for coder: each target requires guide"
            else Result.Ok guide
        Ok (fun f g -> { file = f; guide = g; draft = draft })
        <*> fileResult <*> guideResult

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
        let objective = requireNonEmpty "objective" (Dyn.str item "objective") "coder"
        let background = requireNonEmpty "background" (Dyn.str item "background") "coder"
        let targets = parseCoderTargets (Dyn.get item "targets")
        let doNotTouch = optionalStrArray item "do_not_touch"
        Ok (fun o b t dnt ->
            { objective = o
              background = b
              targets = t
              doNotTouch = dnt })
        <*> objective <*> background <*> targets <*> Ok doNotTouch

let parseInvestigatorIntent (item: obj) : Result<InvestigatorIntent, string> =
    if not (Dyn.typeIs item "object") then Result.Error "Invalid LLM input for investigator: each intent must be an object"
    else
        let objective = requireNonEmpty "objective" (Dyn.str item "objective") "investigator"
        let background = requireNonEmpty "background" (Dyn.str item "background") "investigator"
        let questions = optionalStrArray item "questions"
        let entries = optionalStrArray item "entries"
        if questions.Length = 0 then
            Result.Error "Invalid LLM input for investigator: questions must be a non-empty string array"
        else
            Ok (fun o b e -> { objective = o; background = b; questions = questions; entries = e })
            <*> objective <*> background <*> Ok entries

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
