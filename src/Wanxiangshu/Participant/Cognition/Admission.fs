namespace Wanxiangshu.Participant.Cognition

open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling

/// Pure admission for the `assume` tool input.
///
/// Everything here decides before any external effect: the canvas, the declaration,
/// the phase and the epoch all stay untouched when this module returns a refusal.
/// It is the one place that knows the tool's wire shape, so the adapter cannot drift
/// into a second, looser reading of the same contract.
[<RequireQualifiedAccess>]
module AssumeAdmission =

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'object' && $0 !== null && !Array.isArray($0)")>]
    let private isPlainObject (value: obj) : bool = jsNative

    /// The accepted argument set. Anything else — including the retired `query`,
    /// `planComplete`, `workingOn`, `obligations` and `horizon` — is refused here so
    /// a half-old protocol can never reach the executor.
    let private knownArguments = set [ "update"; "todos" ]

    type Rejection =
        | MissingUpdate
        | UpdateNotString
        | MissingTodos
        | TodosNotArray
        | TodoRowNotObject of index: int
        | TodoContentMissing of index: int
        | TodoContentNotString of index: int
        | TodoContentBlank of index: int
        | TodoStatusMissing of index: int
        | TodoStatusUnknown of index: int * value: string
        | TodoPriorityUnknown of index: int * value: string
        | UnknownArgument of name: string

    [<RequireQualifiedAccess>]
    module Rejection =
        let message (rejection: Rejection) : string =
            match rejection with
            | MissingUpdate -> "assume requires update: a standard jq program over the current canvas"
            | UpdateNotString -> "assume.update must be a string"
            | MissingTodos -> "assume requires todos: the complete todo list for this session"
            | TodosNotArray -> "assume.todos must be an array"
            | TodoRowNotObject index -> sprintf "assume.todos[%d] must be an object" index
            | TodoContentMissing index -> sprintf "assume.todos[%d].content is required" index
            | TodoContentNotString index -> sprintf "assume.todos[%d].content must be a string" index
            | TodoContentBlank index -> sprintf "assume.todos[%d].content must not be empty" index
            | TodoStatusMissing index -> sprintf "assume.todos[%d].status is required" index
            | TodoStatusUnknown(index, value) ->
                sprintf
                    "assume.todos[%d].status must be one of pending, in_progress, completed, cancelled; got '%s'"
                    index
                    value
            | TodoPriorityUnknown(index, value) ->
                sprintf "assume.todos[%d].priority must be one of high, medium, low; got '%s'" index value
            | UnknownArgument name ->
                sprintf
                    "assume accepts only update and todos; '%s' is not part of this contract (the retired query parameter no longer exists)"
                    name

    /// jq must produce exactly one value. Zero, several, or a runtime error all mean
    /// "no new canvas", never "take the first output".
    [<RequireQualifiedAccess>]
    type UpdateOutcome =
        | OneCanvas of canvasJson: string
        | NoOutput
        | MultipleOutputs of count: int
        | ProgramError of message: string

    [<RequireQualifiedAccess>]
    module UpdateOutcome =
        let toRejection (outcome: UpdateOutcome) : Rejection =
            match outcome with
            | UpdateOutcome.NoOutput -> Rejection.UnknownArgument "update produced no output"
            | UpdateOutcome.MultipleOutputs count ->
                Rejection.UnknownArgument(sprintf "update produced %d outputs" count)
            | UpdateOutcome.ProgramError message -> Rejection.UnknownArgument(sprintf "update failed: %s" message)
            | UpdateOutcome.OneCanvas _ -> Rejection.UnknownArgument "unreachable"

    let private enumOf (value: obj) : string option =
        if isString value then Some(unbox<string> value) else None

    /// One row's status, in the wire vocabulary the sink speaks. A value the contract
    /// does not name is refused rather than defaulted — silently mapping "started" to
    /// "pending" would let the model believe a state it never declared is in effect.
    let private decodeStatus (index: int) (value: obj) : Result<TodoStatus, Rejection> =
        match enumOf value with
        | None -> Error(Rejection.TodoStatusUnknown(index, string value))
        | Some raw ->
            TodoStatus.tryParse raw
            |> Option.map Ok
            |> Option.defaultWith (fun () -> Error(Rejection.TodoStatusUnknown(index, raw)))

    /// Priority defaults to medium when omitted, which the contract states. An
    /// unrecognised value defaults too: the field is optional metadata, so refusing
    /// the whole declaration for it would cost the model its canvas update.
    let private decodePriority (value: obj) : TodoPriority =
        enumOf value
        |> Option.bind TodoPriority.tryParse
        |> Option.defaultValue TodoPriority.Medium

    /// A declared `content` value, or why it is not usable text.
    let private nonBlankText (index: int) (text: string) : Result<string, Rejection> =
        if System.String.IsNullOrWhiteSpace text then
            Error(Rejection.TodoContentBlank index)
        else
            Ok text

    let private declaredText (index: int) (value: obj) : Result<string, Rejection> =
        if isNull value then
            Error(Rejection.TodoContentMissing index)
        elif not (isString value) then
            Error(Rejection.TodoContentNotString index)
        else
            nonBlankText index (unbox<string> value)

    let private decodeContent (index: int) (row: obj) : Result<string, Rejection> = declaredText index (row?("content"))

    let private decodeStatusField (index: int) (row: obj) : Result<TodoStatus, Rejection> =
        match row?("status") with
        | null -> Error(Rejection.TodoStatusMissing index)
        | status -> decodeStatus index status

    let private decodeRow (index: int) (row: obj) : Result<string * TodoStatus * TodoPriority, Rejection> =
        if isNull row || not (isPlainObject row) then
            Error(Rejection.TodoRowNotObject index)
        else
            result {
                let! text = decodeContent index row
                let! status = decodeStatusField index row

                return text, status, decodePriority row?("priority")
            }

    /// Walk the rows left to right, stopping at the first refusal. The accumulator
    /// holds the rows decoded so far, so an earlier refusal can never be masked by a
    /// later success and a failure never discards earlier work.
    let rec private walkRows
        (rows: obj array)
        (index: int)
        (decoded: (string * TodoStatus * TodoPriority) list)
        : Result<(string * TodoStatus * TodoPriority) list, Rejection> =
        if index >= rows.Length then
            Ok(List.rev decoded)
        else
            decodeRow index rows.[index]
            |> Result.map (fun row -> walkRows rows (index + 1) (row :: decoded))
            |> Result.bind id

    let private decodeRows (rows: obj array) = walkRows rows 0 []

    /// The `update` program, or why there isn't one. Kept as its own step so the
    /// argument object's shape is decided before anything looks at the declaration.
    let private decodeUpdate (args: obj) : Result<string, Rejection> =
        match args?("update") with
        | null -> Error Rejection.MissingUpdate
        | update when not (isString update) -> Error Rejection.UpdateNotString
        | update -> Ok(unbox<string> update)

    /// The declared rows, or why there aren't any.
    let private decodeTodos (args: obj) : Result<(string * TodoStatus * TodoPriority) list, Rejection> =
        match args?("todos") with
        | null -> Error Rejection.MissingTodos
        | todos when not (isArray todos) -> Error Rejection.TodosNotArray
        | todos -> decodeRows (unbox<obj array> todos)

    /// Decode the whole tool argument object.
    ///
    /// Unknown keys are refused rather than ignored: silently dropping `query` would
    /// let a model that still carries the old habit believe its read succeeded.
    /// The first argument name the contract does not declare, if any.
    let private firstUnknownArgument (args: obj) : string option =
        // DSL-MUTABLE: algorithm-scratch — tracks first unknown key during argument object validation.
        let mutable unknown = None

        let keys =
            if isNull args || not (isPlainObject args) then
                [||]
            else
                unbox<string array> (emitJsExpr args "Object.keys($0)")

        let isUnknown (key: string) =
            not (Set.contains key knownArguments) && unknown.IsNone

        let record key =
            if isUnknown key then
                unknown <- Some key

        for key in keys do
            record key

        unknown

    /// Decode the whole tool argument object.
    ///
    /// Unknown keys are refused rather than ignored: silently dropping `query` would
    /// let a model that still carries the old habit believe its read succeeded.
    let tryDecode (args: obj) : Result<string * (string * TodoStatus * TodoPriority) list, Rejection> =
        match firstUnknownArgument args with
        | Some name -> Error(Rejection.UnknownArgument name)
        | None -> Result.map2 (fun update todos -> update, todos) (decodeUpdate args) (decodeTodos args)
