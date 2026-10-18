namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Repository.Programming.Js


/// PROMPT-019: load already-localized js-program prose. Domain assembles;
/// this module binds language.
module JsDescriptionAssets =
    val load: lang: ProviderLanguage -> JsCanonicalDescription.Prose
    val argProgram: lang: ProviderLanguage -> string
    val missingProgram: lang: ProviderLanguage -> string
    val viewDeclarationExtra: lang: ProviderLanguage -> string

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
        lifecyclePersistence: IJsLifecyclePersistence option ->
        fileAccessObservation: (HostToolContext -> JsExplicitFileRead list -> string list -> Task<unit>) option ->
            ToolSpec

    val createWithAdmission:
        factory: HostToolFactory ->
        surface: JsSurface ->
        admission: ToolAdmission ->
        workspaceRoot: string ->
        persistence: IJsTransactionPersistence option ->
        lifecyclePersistence: IJsLifecyclePersistence option ->
        fileAccessObservation: (HostToolContext -> JsExplicitFileRead list -> string list -> Task<unit>) option ->
            ToolSpec
