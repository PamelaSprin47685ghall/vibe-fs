namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation

/// HNI-09: the product decisions of one generated js-* tool. The OpenCode
/// binding (`JsToolSpec`) keeps the host-wire plumbing and asks this module
/// for the decisions.
[<RequireQualifiedAccess>]
module JsToolPolicy =

    /// The directory one session's js-* surface executes in: the path its
    /// owner registered for that session, else the given fallback.
    val rootForSession:
        sessionDirectories: System.Collections.Generic.Dictionary<string, string> ->
        fallback: string ->
        sessionId: string ->
            string

    /// Whether the office role may call the generated tool of that role name.
    val admitsRole: roleName: string -> role: Role -> bool

    /// The localized prose of the `program` argument.
    val argProgramProse: unit -> string

    /// The localized prose of the required `name` argument.
    val argNameProse: unit -> string

    /// The localized prose of the refusal of a call that carries no state.
    val missingProgramProse: unit -> string

    /// The localized prose of the flat view declaration's extra field.
    val viewDeclarationExtraProse: unit -> string

    /// The flat view declaration of a call: the protected prefix and carry
    /// array. A call that declares neither carries none.
    val viewDeclarationOf: raw: obj -> obj

    /// Why the declaration is not a valid view declaration, if it is not.
    val parsedDeclarationFailure: declaration: obj -> JsFailure option

    /// Why this call's view declaration is refused, if it is refused.
    val viewFailureOf: raw: obj -> declaration: obj -> JsFailure option

    /// Commit the declaration and its owning (message ID, tool call ID) pair.
    val commitView: sessionId: string -> callIdentity: (string * string) option -> declaration: obj -> JsFailure option

    /// Commit the declaration only after the program settled.
    val viewCommitFailure:
        sessionId: string ->
        callIdentity: (string * string) option ->
        declaration: obj ->
        outcome: JsToolWorkflow.JsToolOutcome ->
            JsFailure option

    /// The rendered result of a settled call, with the view declaration
    /// committed when the program settled.
    val renderCommitted:
        sessionId: string ->
        callIdentity: (string * string) option ->
        declaration: obj ->
        outcome: JsToolWorkflow.JsToolOutcome ->
            string
