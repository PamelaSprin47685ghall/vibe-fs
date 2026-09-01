namespace Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
type ProviderLanguage =
    | English
    | SimplifiedChinese

[<RequireQualifiedAccess>]
module ProviderLanguage =
    val externalName: language: ProviderLanguage -> string
    val label: language: ProviderLanguage -> string
    val resourceDirectory: language: ProviderLanguage -> string
    val resourceFileName: language: ProviderLanguage -> string
    val tryParse: raw: string -> ProviderLanguage option
    val parse: raw: string -> ProviderLanguage
    val fromPreferenceObservation: observation: string option -> Result<ProviderLanguage, string>
    val inheritFrom: owner: ProviderLanguage -> ProviderLanguage
