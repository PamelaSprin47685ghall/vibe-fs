namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Cognition
open Wanxiangshu.Participant.Provider

/// The single cognitive write entry point.
///
/// This module is an adapter: it decodes the wire shape, resolves the owner, runs the
/// jq program and renders the committed canvas. Ordering, idempotence, persistence
/// and phase semantics belong to the cognitive owner, so they cannot drift per tool.
module AssumeTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/assume/description"

        [<Literal>]
        let ArgUpdate = "tool/assume/arg-update"

        [<Literal>]
        let ArgTodos = "tool/assume/arg-todos"

    [<Import("json", "jq-wasm")>]
    let private jqJson (inputJson: string) (query: string) : JS.Promise<obj array> = jsNative

    [<Emit("$0.then($1).catch((error) => $2(String((error && (error.stderr || error.message)) || error)))")>]
    let private observeJq
        (promise: JS.Promise<obj array>)
        (onSuccess: obj array -> unit)
        (onError: string -> unit)
        : unit =
        jsNative

    let private runJq input query : Task<Result<obj array, string>> =
        let completion = TaskCompletionSource<Result<obj array, string>>()

        // `input` is already canonical JSON text: the canvas is carried as text so the
        // committed bytes are exactly what jq produced. Stringifying it again would
        // wrap it in quotes, and then `.` would evaluate to that quoted string rather
        // than the canvas — which is how a canvas silently turns into its own JSON
        // encoding on the second call.
        observeJq (jqJson input query) (Ok >> completion.SetResult) (Error >> completion.SetResult)

        completion.Task

    /// jq output → canvas decision. Exactly one output is the only success shape.
    /// The one output's canonical text, or why it is not a JSON value at all.
    let private canvasText (value: obj) : Result<string, string> =
        if CanvasCodec.isJsonValue value then
            Ok(CanvasCodec.toJson value)
        else
            Error "assume update must produce a JSON value"

    let private canvasOf (outputs: Result<obj array, string>) : Result<string, string> =
        match outputs with
        | Error message -> Error message
        | Ok results when results.Length = 1 -> canvasText results.[0]
        | Ok results when results.Length = 0 ->
            Error "assume update must produce exactly one JSON value; got 0; canvas unchanged"
        | Ok results ->
            Error(sprintf "assume update must produce exactly one JSON value; got %d; canvas unchanged" results.Length)

    /// Host delivery state, never a TodoItem business field: it says whether the
    /// UI projection landed, not whether the work is done.
    [<RequireQualifiedAccess>]
    module TodoSync =
        let Applied = "applied"
        let Pending = "pending"

    /// The rendering the model reads back: the full committed canvas, once, through
    /// the one LlmFacing writer.
    ///
    /// `canvas_json` always carries the lossless payload. TOML cannot express a root
    /// `null`, a key holding `null`, or an object inside a mixed array, so the
    /// readable hierarchical form is not an option that preserves every canvas the
    /// model is allowed to write. Choosing the guaranteed-lossless form keeps the
    /// canvas the model reads identical to the canvas it committed.
    let private renderCanvas (todoSync: string) (canvasJson: string) : string =
        LlmFacing.instructions []
        |> LlmFacing.withData
            [ LlmFacing.Data.stringField "todo_sync" todoSync
              LlmFacing.Data.stringField "canvas_encoding" "json"
              LlmFacing.Data.stringField "canvas_json" canvasJson ]
        |> LlmFacing.render

    let private jsonString (text: string) : string =
        "\""
        + (text
              .Replace("\\", "\\\\")
              .Replace("\"", "\\\"")
              .Replace("\n", "\\n")
              .Replace("\r", "\\r")
              .Replace("\t", "\\t"))
        + "\""


    let private todosJson (todos: (string * TodoStatus * TodoPriority) list) : string =
        let rows =
            todos
            |> List.map (fun (content, status, priority) ->
                sprintf
                    """{"content":%s,"status":"%s","priority":"%s"}"""
                    (jsonString content)
                    (TodoStatus.wire status)
                    (TodoPriority.wire priority))

        "[" + String.concat "," rows + "]"

    /// Canonical JSON of the exact tool input, so a replay is recognised by what the
    /// model actually sent rather than by a digest taken over something else.
    let private canonicalInput (update: string) (todos: (string * TodoStatus * TodoPriority) list) : string =
        sprintf """{"update":%s,"todos":%s}""" (jsonString update) (todosJson todos)

    /// One refusal, one shape. Every rejection the tool produces is an instruction the
    /// model can act on; wrapping each in a separate call would let the wording drift
    /// between branches that mean the same thing.
    let private refuse (instructions: string list) : string =
        ToolHostCodec.tomlObjectWithInstructions instructions []

    /// The exact identity the Host must have supplied. host-boundary-009: both halves
    /// are required, and neither may be guessed from the other's context.
    let private callIdentity (ctx: HostToolContext) : Result<ToolCallId, string> =
        ctx.ToolCallId
        |> Option.map Ok
        |> Option.defaultWith (fun () -> Error "assume requires an exact tool call identity")

    /// The owner this call aims at, or why there is none.
    let private ownerFor (resolveOwner: HostToolContext -> CognitiveOwner.T option) (ctx: HostToolContext) =
        resolveOwner ctx
        |> Option.map Ok
        |> Option.defaultWith (fun () -> Error "assume could not resolve a cognitive owner for this call")

    /// jq's output as the next canvas, or the refusal the caller should see.
    let private nextCanvas (canvasJson: string) (update: string) : Task<Result<string, string>> =
        task {
            let! outputs = runJq canvasJson update
            return canvasOf outputs
        }

    /// The commit result as the bytes the model should read back.
    let private renderOutcome (outcome: CommitOutcome) (canvasJson: string) : string =
        match outcome with
        | CommitOutcome.Rejected reason -> refuse [ reason ]
        // A replay answers with the frozen first result, not a second rendering: the
        // bytes the model already saw are the ones it will see again.
        | CommitOutcome.Committed _
        | CommitOutcome.Replayed _ -> renderCanvas TodoSync.Applied canvasJson

    /// Run `update`, commit the resulting canvas, and render what the model reads.
    ///
    /// Everything past admission, owner resolution and call identity lives here, so
    /// each of those three guards stays a separate question rather than one nested tree.
    let private commitAndRender
        (runtime: CognitiveRuntime)
        (owner: CognitiveOwner.T)
        (toolCallId: ToolCallId)
        (update: string)
        (todos: (string * TodoStatus * TodoPriority) list)
        : Task<string> =
        task {
            // The canvas jq sees is the owner's current committed canvas.
            match! nextCanvas runtime.CurrentCanvas.CanvasJson update with
            | Error reason -> return refuse [ reason ]
            | Ok canvasJson ->
                let inputDigest =
                    // Canonical JSON of the exact tool input, so a replay is
                    // recognised by what the model actually sent.
                    Wanxiangshu.Foundation.CanonicalJson.canonicalJson (canonicalInput update todos)

                let! outcome = runtime.Commit owner toolCallId inputDigest canvasJson todos
                return renderOutcome outcome canvasJson
        }

    /// One refusal as a completed task.
    let private refuseOnce (instruction: string) = Task.FromResult(refuse [ instruction ])

    /// either missing is a refusal the model can act on.
    let private resolveOwners
        (runtime: CognitiveRuntime)
        (resolveOwner: HostToolContext -> CognitiveOwner.T option)
        (ctx: HostToolContext)
        (update: string)
        (todos: (string * TodoStatus * TodoPriority) list)
        : Task<string> =
        match ownerFor resolveOwner ctx, callIdentity ctx with
        | Error reason, _
        | _, Error reason -> refuseOnce reason
        | Ok owner, Ok toolCallId -> commitAndRender runtime owner toolCallId update todos

    /// The call the tool actually runs: decode, resolve, commit.
    ///
    /// Each guard answers one question in turn, so a refusal is attributable to the
    /// step that produced it rather than to a position in a nested tree.
    let executeWith
        (runtime: CognitiveRuntime)
        (resolveOwner: HostToolContext -> CognitiveOwner.T option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        match AssumeAdmission.tryDecode args.Raw with
        | Error rejection -> refuseOnce (AssumeAdmission.Rejection.message rejection)
        | Ok(update, todos) -> resolveOwners runtime resolveOwner ctx update todos

    /// Resolve the owner and the call identity, then commit. Both are required, and


    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> r <> Role.Blogger && r <> Role.Distiller)

    let spec
        (factory: HostToolFactory)
        (runtime: CognitiveRuntime)
        (resolveOwner: HostToolContext -> CognitiveOwner.T option)
        : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "assume"
          Description = ProviderProse.render language Path.Description Map.empty
          Arguments =
            [ "update",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgUpdate Map.empty) factory
              "todos",
              ToolHostCodec.todoArraySchemaDescribed (ProviderProse.render language Path.ArgTodos Map.empty) factory ]
          Admission = admission
          Execute = executeWith runtime resolveOwner }
