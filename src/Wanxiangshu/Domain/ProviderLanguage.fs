namespace Wanxiangshu.Domain

open System

/// PROMPT-017 / HOST-026: provider-facing world language.
[<RequireQualifiedAccess>]
type ProviderLanguage =
    | English
    | SimplifiedChinese

[<RequireQualifiedAccess>]
module ProviderLanguage =

    /// Locale leaf filename under a semantic resource directory (§4.7.8).
    let resourceFileName =
        function
        | ProviderLanguage.English -> "en.md"
        | ProviderLanguage.SimplifiedChinese -> "zh-CN.md"

    let resourceDirectory =
        function
        | ProviderLanguage.English -> "en"
        | ProviderLanguage.SimplifiedChinese -> "zh-CN"

    let label =
        function
        | ProviderLanguage.English -> "en"
        | ProviderLanguage.SimplifiedChinese -> "zh-CN"

    /// Accept resource dirs and common aliases (`en`, `zh-CN`, `zh`, `english`, …).
    let tryParse (raw: string) : ProviderLanguage option =
        if String.IsNullOrWhiteSpace raw then
            None
        else
            match raw.Trim().ToLowerInvariant() with
            | "en"
            | "eng"
            | "english" -> Some ProviderLanguage.English
            | "zh-cn"
            | "zh"
            | "zh_cn"
            | "chs"
            | "simplifiedchinese"
            | "simplified-chinese"
            | "cn" -> Some ProviderLanguage.SimplifiedChinese
            | _ -> None

    let parse (raw: string) : ProviderLanguage =
        match tryParse raw with
        | Some lang -> lang
        | None -> raise (ArgumentException(sprintf "unrecognized ProviderLanguage: %s (PROMPT-017)" raw))

    /// HOST-026: child / attached / InternalLeaf language = owner | commissioner.
    let inheritFrom (owner: ProviderLanguage) : ProviderLanguage = owner
