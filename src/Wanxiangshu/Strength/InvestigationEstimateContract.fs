namespace Wanxiangshu.Strength

open Fable.Core
open Fable.Core.JsInterop

module InvestigationEstimateContract =

    /// Plain `let`, not `[<Literal>]`: Fable inlines a literal and emits no export,
    /// so tests and JS callers can import the constant from dist.
    let EstimatedReadonlyRoundsField = "estimated_readonly_rounds"

    [<Literal>]
    let private LegacyRoundsField = "delegate_readonly_rounds"

    [<Literal>]
    let private NoteField = "self_note"

    /// Plain `let`, not `[<Literal>]`: Fable inlines a literal and emits no export,
    /// so tests and JS callers can import the constant from dist.
    let ProtocolRevision = 2

    [<RequireQualifiedAccess>]
    type InvestigationToolPolicy =
        | EstimateAfterCall
        | NoEstimate
        | Unreviewed

    let classifyTool (toolName: string) : InvestigationToolPolicy =
        match toolName with
        | "read"
        | "glob"
        | "grep"
        | "js-manager"
        | "js-engineer"
        | "js-devops"
        | "edit"
        | "write"
        | "mv"
        | "rm"
        | "fetch"
        | "run" -> InvestigationToolPolicy.EstimateAfterCall
        | "fork"
        | "resume"
        | "commission"
        | "join"
        | "horizon"
        | "review"
        | "suicide"
        | "fission"
        | "open-terminal"
        | "send-terminal"
        | "read-terminal"
        | "signal-terminal"
        | "skill"
        | "sphinx"
        | "assume"
        | "enough"
        | "abandon"
        | "defer"
        | "subscribe"
        | "publish"
        | "celebrate"
        | "regret"
        | "chronicle"
        | "js-bookkeeper"
        | "bash-honeypot"
        | "invalid"
        | "js-orchestrator"
        | "js-blogger" -> InvestigationToolPolicy.NoEstimate
        | _ -> InvestigationToolPolicy.Unreviewed

    [<Struct>]
    type EstimatedReadonlyRounds = private EstimatedReadonlyRounds of int

    module EstimatedReadonlyRounds =
        let value (EstimatedReadonlyRounds v) = v

        let toExecutionBudget (rounds: EstimatedReadonlyRounds) : ReadonlyRoundBudget =
            match ReadonlyRoundBudget.tryCreate (value rounds) with
            | Ok b -> b
            | Error msg -> failwith ("unexpected invalid budget from valid estimate: " + msg)

    [<RequireQualifiedAccess>]
    type EstimateArgumentError =
        | MissingEstimate
        | WrongNumberType
        | InvalidRange
        | NotePresentWhenZero
        | MissingOrBlankNoteWhenPositive
        | NoteNotString
        | MixedProtocolFields
        | InvalidArgumentObject

    [<Emit("typeof $0 === 'object' && $0 !== null && !Array.isArray($0)")>]
    let private isPlainObject (value: obj) : bool = jsNative

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasOwn (target: obj) (key: string) : bool = jsNative

    [<Emit("typeof $0 === 'number'")>]
    let private isJsNumber (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isJsString (value: obj) : bool = jsNative

    [<Emit("Number.isFinite($0)")>]
    let private isJsFinite (value: obj) : bool = jsNative

    [<Emit("Number.isInteger($0)")>]
    let private isJsInteger (value: obj) : bool = jsNative

    let private validateNumber (raw: obj) : Result<int, EstimateArgumentError> =
        if not (isJsNumber raw) then
            Error EstimateArgumentError.WrongNumberType
        elif not (isJsFinite raw) || not (isJsInteger raw) then
            Error EstimateArgumentError.InvalidRange
        else
            let num = unbox<float> raw
            // 检查非负以及是否在 [0, 2147483647]
            if num < 0.0 || num > 2147483647.0 then
                Error EstimateArgumentError.InvalidRange
            else
                // -0 在 JS 转换为 float 后与 0.0 相等，按 0 处理
                let intVal = if num = 0.0 then 0 else int num
                Ok intVal

    let private validateNoteForZero (args: obj) : Result<string option, EstimateArgumentError> =
        if hasOwn args NoteField then
            Error EstimateArgumentError.NotePresentWhenZero
        else
            Ok None

    let private validateNoteForPositive (args: obj) : Result<string option, EstimateArgumentError> =
        if not (hasOwn args NoteField) then
            Error EstimateArgumentError.MissingOrBlankNoteWhenPositive
        else
            let rawNote = args?(NoteField)
            if not (isJsString rawNote) then
                Error EstimateArgumentError.NoteNotString
            else
                let noteStr = string rawNote
                if noteStr.Trim().Length = 0 then
                    Error EstimateArgumentError.MissingOrBlankNoteWhenPositive
                else
                    Ok(Some noteStr)

    let parseParticipatingArguments (arguments: obj) : Result<EstimatedReadonlyRounds * string option, EstimateArgumentError> =
        if not (isPlainObject arguments) then
            Error EstimateArgumentError.InvalidArgumentObject
        elif hasOwn arguments LegacyRoundsField then
            Error EstimateArgumentError.MixedProtocolFields
        elif not (hasOwn arguments EstimatedReadonlyRoundsField) then
            Error EstimateArgumentError.MissingEstimate
        else
            let rawRounds = arguments?(EstimatedReadonlyRoundsField)
            match validateNumber rawRounds with
            | Error err -> Error err
            | Ok 0 ->
                match validateNoteForZero arguments with
                | Error err -> Error err
                | Ok noteOpt -> Ok(EstimatedReadonlyRounds 0, noteOpt)
            | Ok positiveRounds ->
                match validateNoteForPositive arguments with
                | Error err -> Error err
                | Ok noteOpt -> Ok(EstimatedReadonlyRounds positiveRounds, noteOpt)
