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

    let private isChineseLocale (raw: string) : bool =
        if String.IsNullOrWhiteSpace raw then
            false
        else
            let lower = raw.ToLowerInvariant()

            lower.StartsWith "zh"
            || lower.Contains "zh-cn"
            || lower.Contains "zh_cn"
            || lower.Contains "zh-hans"
            || lower.Contains "chs"
            || lower.Contains "chinese"

    /// Resolves global preference through the normative preference ladder:
    /// 1. Explicit environment variable (WANXIANGSHU_PROVIDER_LANGUAGE)
    /// 2. Host config preference (opencode.json language)
    /// 3. IDE / System locale (VSCODE_NLS_CONFIG, POSIX LC_ALL/LANG, Node Intl)
    /// 4. Default fallback: ProviderLanguage.English
    let fromObservationLadder
        (explicit: string option)
        (hostConfig: string option)
        (vscodeNls: string option)
        (posixLocale: string option)
        (intlLocale: string option)
        : Result<ProviderLanguage, string> =
        match explicit with
        | Some raw when not (String.IsNullOrWhiteSpace raw) -> configuredPreference raw
        | _ ->
            match hostConfig with
            | Some raw when not (String.IsNullOrWhiteSpace raw) -> configuredPreference raw
            | _ ->
                let systemDetected =
                    [ vscodeNls; posixLocale; intlLocale ]
                    |> List.tryPick (fun candidate ->
                        match candidate with
                        | Some raw when isChineseLocale raw -> Some ProviderLanguage.SimplifiedChinese
                        | _ -> None)

                match systemDetected with
                | Some lang -> Ok lang
                | None -> Ok ProviderLanguage.English

    /// HOST-026: child / attached / InternalLeaf language = owner | commissioner.
    let inheritFrom (owner: ProviderLanguage) : ProviderLanguage = owner
