namespace Wanxiangshu.Infrastructure.Resources

open System
open Wanxiangshu.Domain
open Wanxiangshu.Session

/// Bilingual provider assets: `resources/provider/<semantic>/<en.md|zh-CN.md>` (PROMPT-017 §4.7.8).
[<RequireQualifiedAccess>]
module ProviderResources =

    let relativePath (lang: ProviderLanguage) (semanticPath: string) : string =
        let trimmed =
            if String.IsNullOrWhiteSpace semanticPath then
                ""
            else
                semanticPath.Trim().TrimStart('/').TrimStart('\\')

        sprintf "provider/%s/%s" trimmed (ProviderLanguage.resourceFileName lang)

    let exists (lang: ProviderLanguage) (semanticPath: string) : bool =
        PackageResources.exists (relativePath lang semanticPath)

    let readText (lang: ProviderLanguage) (semanticPath: string) : string =
        PackageResources.readText(relativePath lang semanticPath).Trim()

    let tryReadText (lang: ProviderLanguage) (semanticPath: string) : string option =
        if exists lang semanticPath then
            Some(readText lang semanticPath)
        else
            None

    /// ARCH-016 Gate C hook: both locale leaves must exist for a semantic path.
    let requireLanguagePair (semanticPath: string) : unit =
        for lang in [ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ] do
            if not (exists lang semanticPath) then
                raise (
                    InvalidOperationException(
                        sprintf
                            "provider resource missing for %s: %s (HOST-026 / ARCH-016 Gate C)"
                            (ProviderLanguage.label lang)
                            semanticPath
                    )
                )

    /// Layout smoke: provider tree root present.
    let languageRootsPresent () : bool = PackageResources.exists "provider"
