namespace Wanxiangshu.Participant.Provider

open System
open Wanxiangshu.Resources

/// Bilingual provider assets: `resources/provider/<semantic>/<en.md|zh-CN.md>` (PROMPT-017 §4.7.8).
[<RequireQualifiedAccess>]
module ProviderResources =

    let relativePath (language: ProviderLanguage) (semanticPath: string) : string =
        let trimmed =
            if String.IsNullOrWhiteSpace semanticPath then
                ""
            else
                semanticPath.Trim().TrimStart('/').TrimStart('\\')

        sprintf "provider/%s/%s" trimmed (ProviderLanguage.resourceFileName language)

    let exists (language: ProviderLanguage) (semanticPath: string) : bool =
        ProviderResourceBytes.exists (relativePath language semanticPath)

    let readText (language: ProviderLanguage) (semanticPath: string) : string =
        ProviderResourceBytes.readText(relativePath language semanticPath).Trim()

    let private requireLanguageResource semanticPath language =
        if not (exists language semanticPath) then
            raise (
                InvalidOperationException(
                    sprintf
                        "provider resource missing for %s: %s (HOST-026 / ARCH-016 Gate C)"
                        (ProviderLanguage.label language)
                        semanticPath
                )
            )

    /// ARCH-016 Gate C hook: both locale leaves must exist for a semantic path.
    let requireLanguagePair (semanticPath: string) : unit =
        for language in [ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ] do
            requireLanguageResource semanticPath language

    /// Layout smoke: provider tree root present.
    let languageRootsPresent () : bool = ProviderResourceBytes.exists "provider"
