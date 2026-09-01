namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Repository.Programming.Js

/// Coexistence seam between builtin filesystem fallbacks and generated js-*.
/// GrandRewrite keeps read/edit/write/glob/grep as normal primitive fallback:
/// their Host descriptions are left untouched. Preference for intent-level
/// programs is taught inside the generated js-* contract and its Ultra Example.
module BuiltinToolDescriptionHook =
    val BuiltinFilesystemTools: Set<string>
    val annotate: builtinName: string -> description: string -> jsRoleToolName: string -> string
    val validateRecommendation: jsRoleToolName: string -> visibleToolNames: Set<string> -> Result<unit, string>

/// PROMPT-019: load already-localized js-program prose. Domain assembles;
/// this module binds language.
module JsDescriptionAssets =
    val load: lang: ProviderLanguage -> JsCanonicalDescription.Prose
    val argProgram: lang: ProviderLanguage -> string
    val missingProgram: lang: ProviderLanguage -> string

/// JS-073/JS-074: a generated js-* tool spec — the dynamic counterpart of the
/// static baseSpecs. Built from a generated surface (JS-002); execution goes
/// through JsToolWorkflow (sandbox → staging → preflight → durable facts →
/// commit) and the result renders through JsToolsResult (JS-016).
module JsToolSpec =
    val admissionFor: roleName: string -> ToolAdmission

    val create:
        factory: HostToolFactory ->
        surface: JsSurface ->
        workspaceRoot: string ->
        persistence: IJsTransactionPersistence option ->
        fileAccessObservation: (HostToolContext -> string list -> string list -> Task<unit>) option ->
            ToolSpec
