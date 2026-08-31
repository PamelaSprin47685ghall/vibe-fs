namespace Wanxiangshu.Participant.Provider

open System

/// PROMPT-017 / HOST-026: provider-facing world language.
[<RequireQualifiedAccess>]
type ProviderLanguage =
    | English
    | SimplifiedChinese

[<RequireQualifiedAccess>]
module ProviderLanguage =

    type private Locale =
        { ExternalName: string
          Label: string
          ResourceDirectory: string
          ResourceFileName: string }

    let private locale =
        function
        | ProviderLanguage.English ->
            { ExternalName = "English"
              Label = "en"
              ResourceDirectory = "en"
              ResourceFileName = "en.md" }
        | ProviderLanguage.SimplifiedChinese ->
            { ExternalName = "SimplifiedChinese"
              Label = "zh-CN"
              ResourceDirectory = "zh-CN"
              ResourceFileName = "zh-CN.md" }

    let externalName language = (locale language).ExternalName

    let label language = (locale language).Label

    let resourceDirectory language = (locale language).ResourceDirectory

    /// Locale leaf filename under a semantic resource directory (§4.7.8).
    let resourceFileName language = (locale language).ResourceFileName

    let private parseNormalized (normalized: string) : ProviderLanguage option =
        match normalized with
        | "en"
        | "eng"
        | "english" -> Some ProviderLanguage.English
        | "zh-cn"
        | "zh"
        | "zh_cn"
        | "chs"
        | "chinese"
        | "simplifiedchinese"
        | "simplified-chinese"
        | "cn" -> Some ProviderLanguage.SimplifiedChinese
        | _ -> None

    let tryParse (raw: string) : ProviderLanguage option =
        if String.IsNullOrWhiteSpace raw then
            None
        else
            raw.Trim().ToLowerInvariant() |> parseNormalized

    let parse (raw: string) : ProviderLanguage =
        match tryParse raw with
        | Some language -> language
        | None -> raise (ArgumentException(sprintf "unrecognized ProviderLanguage: %s (PROMPT-017)" raw))

    let private configuredPreference (raw: string) : Result<ProviderLanguage, string> =
        match tryParse raw with
        | Some language -> Ok language
        | None -> Error(sprintf "WANXIANGSHU_PROVIDER_LANGUAGE unrecognized: %s (HOST-026)" raw)

    /// The sole policy for interpreting the raw provider-language preference.
    let fromPreferenceObservation (observation: string option) : Result<ProviderLanguage, string> =
        match observation with
        | None -> Ok ProviderLanguage.English
        | Some raw when String.IsNullOrWhiteSpace raw -> Ok ProviderLanguage.English
        | Some raw -> configuredPreference raw

    /// HOST-026: child / attached / InternalLeaf language = owner | commissioner.
    let inheritFrom (owner: ProviderLanguage) : ProviderLanguage = owner
