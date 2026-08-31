namespace Wanxiangshu.Participant.Provider

open System.Collections.Generic
open Wanxiangshu.Foundation.Identity

/// HOST-026 / PROMPT-017: process-local session language bind-once authority.
[<RequireQualifiedAccess>]
module SessionProviderLanguage =

    let private gate = obj ()
    // DSL-MUTABLE: resource — per-session language registry
    let private bySession = Dictionary<string, ProviderLanguage>()

    let clearAllForTests () = lock gate (fun () -> bySession.Clear())

    let tryGet (sessionId: SessionId) : ProviderLanguage option =
        lock gate (fun () ->
            match bySession.TryGetValue(SessionId.value sessionId) with
            | true, language -> Some language
            | false, _ -> None)

    /// Bound session → that language. Unbound → English (HOST-026 first-touch).
    /// This observation does not bind the session.
    let languageOf (sessionId: SessionId) : ProviderLanguage =
        tryGet sessionId |> Option.defaultValue ProviderLanguage.English

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
