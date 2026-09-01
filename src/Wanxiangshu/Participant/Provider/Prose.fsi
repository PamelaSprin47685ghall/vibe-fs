namespace Wanxiangshu.Participant.Provider

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ProviderProse =
    val languageOf: sessionId: SessionId -> ProviderLanguage
    val substitute: template: string -> substitutions: Map<string, string> -> string
    val render: language: ProviderLanguage -> semanticPath: string -> substitutions: Map<string, string> -> string

    val instructionLines:
        language: ProviderLanguage -> semanticPath: string -> substitutions: Map<string, string> -> string list

    val document: language: ProviderLanguage -> semanticPath: string -> substitutions: Map<string, string> -> string
    val documentFor: sessionId: SessionId -> semanticPath: string -> substitutions: Map<string, string> -> string
