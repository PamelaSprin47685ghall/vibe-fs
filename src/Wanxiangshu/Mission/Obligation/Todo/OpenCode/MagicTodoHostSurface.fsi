namespace Wanxiangshu.Mission.Obligation.Todo.OpenCode

open Wanxiangshu.Mission.Obligation.Todo

/// JS-native owner for the Magic Todo Host boundary.
/// Provider input and compatibility rows cross as plain objects; Host codec
/// validation and one-way sink projection remain production-owned.
[<RequireQualifiedAccess>]
module MagicTodoHostSurface =

    val decodeInput: args: obj -> obj

    val decodeInputOrReject: args: obj -> obj

    val isProviderInputRejection: error: obj -> bool

    val projectCompatibilityRows: workingOn: string -> obligations: obj array -> obj array

    val canonicalInput: args: obj -> string

    val canonicalInputDigest: sha256: (string -> string) -> args: obj -> string

    val replaceCompatibilityArgs: output: obj -> rows: obj array -> unit

    val replaceEnrichedResult: output: obj -> text: string -> unit

    val applyDefinition: output: obj -> unit
