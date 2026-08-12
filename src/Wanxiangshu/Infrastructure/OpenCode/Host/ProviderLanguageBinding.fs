namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// HOST-026: global preference → root bind; child inherits owner.
/// Preference source: `WANXIANGSHU_PROVIDER_LANGUAGE` (`en` | `zh-CN`). Default `en`.
[<RequireQualifiedAccess>]
module ProviderLanguageBinding =

    let readGlobalPreference () : ProviderLanguage =
        match Environment.GetEnvironmentVariable "WANXIANGSHU_PROVIDER_LANGUAGE" with
        | null
        | "" -> ProviderLanguage.English
        | raw ->
            match ProviderLanguage.tryParse raw with
            | Some lang -> lang
            | None ->
                raise (
                    InvalidOperationException(sprintf "WANXIANGSHU_PROVIDER_LANGUAGE unrecognized: %s (HOST-026)" raw)
                )

    /// Root / first-touch: bind from global preference once.
    let ensureRoot (sessionId: SessionId) : ProviderLanguage =
        match SessionProviderLanguage.tryGet sessionId with
        | Some lang -> lang
        | None ->
            match SessionProviderLanguage.bindOnce sessionId (readGlobalPreference ()) with
            | Ok lang -> lang
            | Error msg -> raise (InvalidOperationException msg)

    /// Child / attached / InternalLeaf: inherit owner|commissioner; never re-read global.
    let ensureInherited (ownerId: SessionId) (childId: SessionId) : ProviderLanguage =
        let ownerLang = ensureRoot ownerId

        match SessionProviderLanguage.inheritFromOwner ownerLang childId with
        | Ok lang -> lang
        | Error msg -> raise (InvalidOperationException msg)
