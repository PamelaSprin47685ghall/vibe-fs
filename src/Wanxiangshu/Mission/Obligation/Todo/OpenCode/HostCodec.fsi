namespace Wanxiangshu.Mission.Obligation.Todo.OpenCode

open System
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Participant.Provider

/// The only raw Host boundary for the GrandRewrite Magic Todo account.
/// Provider input is `{ planComplete: bool, workingOn: string, obligations: [{ name, horizon, work }] }`;
/// the built-in Host executor still receives its legacy `{ todos: [{ content,status,priority }] }`
/// sink shape. New provider semantics never round-trip through that sink.
module MagicTodoHostCodec =

    type ProviderInputRejection =
        inherit Exception
        new: message: string -> ProviderInputRejection

    val isProviderInputRejection: error: obj -> bool

    val tryDecodeInput: args: obj -> Result<TodoWriteInput, string>

    val decodeInputOrReject: args: obj -> TodoWriteInput

    val canonicalInput: args: obj -> string

    val canonicalInputDigest: sha256: (string -> string) -> args: obj -> string

    /// HOST-019: expose the V1 compatibility view without changing the provider
    /// wire that the Host still needs to materialize. `todos` is deliberately
    /// non-enumerable: Effect Schema can decode it, while JSON persistence keeps
    /// the original enumerable `obligations` bytes.
    val replaceCompatibilityArgs: output: obj -> rows: MagicTodoSurface.CompatibilityTodoRow list -> unit

    val replaceEnrichedResult: output: obj -> text: string -> unit

    val applyDefinition: lang: ProviderLanguage -> output: obj -> unit
