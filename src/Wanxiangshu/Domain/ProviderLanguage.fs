namespace Wanxiangshu.Domain

open System
open System.Collections.Generic
open Wanxiangshu.Kernel.Identity

/// PROMPT-017 / HOST-026: provider-facing world language.
[<RequireQualifiedAccess>]
type ProviderLanguage =
    | English
    | SimplifiedChinese

[<RequireQualifiedAccess>]
module ProviderLanguage =

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

/// HOST-026 / PROMPT-017: session-create bind-once (process-local Phase 2).
/// Durable journal fact lands with Phase 17 resource parity.
[<RequireQualifiedAccess>]
module SessionProviderLanguage =

    let private gate = obj ()
    let private bySession = Dictionary<string, ProviderLanguage>()

    let clearAllForTests () = lock gate (fun () -> bySession.Clear())

    let tryGet (sessionId: SessionId) : ProviderLanguage option =
        lock gate (fun () ->
            match bySession.TryGetValue(SessionId.value sessionId) with
            | true, lang -> Some lang
            | false, _ -> None)

    let drop (sessionId: SessionId) : unit =
        lock gate (fun () -> bySession.Remove(SessionId.value sessionId) |> ignore)

    /// Bind-once. Same language → Ok; conflict → Error; unbound → bind.
    let bindOnce (sessionId: SessionId) (language: ProviderLanguage) : Result<ProviderLanguage, string> =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match bySession.TryGetValue key with
            | true, existing when existing = language -> Ok existing
            | true, existing ->
                Error(
                    sprintf
                        "SessionProviderLanguage already bound to %s; refusing %s (HOST-026)"
                        (ProviderLanguage.label existing)
                        (ProviderLanguage.label language)
                )
            | false, _ ->
                bySession.[key] <- language
                Ok language)

    /// HOST-026: child inherits owner language (no second global read).
    let inheritFromOwner (ownerLanguage: ProviderLanguage) (childId: SessionId) =
        bindOnce childId (ProviderLanguage.inheritFrom ownerLanguage)
