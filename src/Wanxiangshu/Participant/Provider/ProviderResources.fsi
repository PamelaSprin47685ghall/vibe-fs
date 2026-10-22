namespace Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module ProviderResources =
    val relativePath: language: ProviderLanguage -> semanticPath: string -> string
    val exists: language: ProviderLanguage -> semanticPath: string -> bool
    val readText: language: ProviderLanguage -> semanticPath: string -> string
    val languageRootsPresent: unit -> bool
