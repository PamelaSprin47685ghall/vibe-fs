namespace Wanxiangshu.Infrastructure.Resources

open System
open Wanxiangshu.Domain

/// Bilingual provider assets under `resources/provider/{en,zh-CN}/…` (PROMPT-017).
/// Phase 2 = layout + load hooks; Phase 17 migrates prose into this tree.
[<RequireQualifiedAccess>]
module ProviderResources =

    let relativePath (lang: ProviderLanguage) (semanticPath: string) : string =
        let trimmed =
            if String.IsNullOrWhiteSpace semanticPath then
                ""
            else
                semanticPath.Trim().TrimStart('/').TrimStart('\\')

        sprintf "provider/%s/%s" (ProviderLanguage.resourceDirectory lang) trimmed

    let exists (lang: ProviderLanguage) (semanticPath: string) : bool =
        PackageResources.exists (relativePath lang semanticPath)

    let readText (lang: ProviderLanguage) (semanticPath: string) : string =
        PackageResources.readText(relativePath lang semanticPath).Trim()

    let tryReadText (lang: ProviderLanguage) (semanticPath: string) : string option =
        if exists lang semanticPath then
            Some(readText lang semanticPath)
        else
            None

    /// ARCH-016 Gate C hook: both EN and zh-CN representations must exist.
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

    /// Layout smoke: language roots present (Phase 2 placeholder dirs).
    let languageRootsPresent () : bool =
        PackageResources.exists "provider/en"
        && PackageResources.exists "provider/zh-CN"
