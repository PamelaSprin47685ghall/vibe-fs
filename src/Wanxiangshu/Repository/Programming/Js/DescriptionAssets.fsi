namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Participant.Provider

/// PROMPT-019: load already-localized js-program prose. Domain assembles;
/// this module binds language. Host-neutral (CUT-40): it reads provider prose
/// resources only; the OpenCode tool binding (`JsToolSpec`) consumes it.
module JsDescriptionAssets =
    val load: lang: ProviderLanguage -> JsCanonicalDescription.Prose
    val argProgram: lang: ProviderLanguage -> string
    val missingProgram: lang: ProviderLanguage -> string
    val viewDeclarationExtra: lang: ProviderLanguage -> string
