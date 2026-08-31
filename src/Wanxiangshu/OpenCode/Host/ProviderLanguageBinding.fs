namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider

/// HOST-026: observes the global preference; the provider owner interprets and binds it.
[<RequireQualifiedAccess>]
module ProviderLanguageBinding =

    let private valueOrRaise =
        function
        | Ok value -> value
        | Error error -> raise (InvalidOperationException error)

    let readGlobalPreference () : ProviderLanguage =
        Environment.GetEnvironmentVariable "WANXIANGSHU_PROVIDER_LANGUAGE"
        |> Option.ofObj
        |> ProviderLanguage.fromPreferenceObservation
        |> valueOrRaise

    /// Root / first-touch: bind from the observed global preference once.
    let ensureRoot (sessionId: SessionId) : ProviderLanguage =
        match SessionProviderLanguage.tryGet sessionId with
        | Some language -> language
        | None ->
            SessionProviderLanguage.bindOnce sessionId (readGlobalPreference ())
            |> valueOrRaise

    /// Child / attached / InternalLeaf: inherit owner|commissioner; never re-read global.
    let ensureInherited (ownerId: SessionId) (childId: SessionId) : ProviderLanguage =
        let ownerLanguage = ensureRoot ownerId
        SessionProviderLanguage.inheritFromOwner ownerLanguage childId |> valueOrRaise

    /// Host tool contexts without a session use the current preference; sessions bind once.
    let forSessionText (sessionText: string) : ProviderLanguage =
        if String.IsNullOrEmpty sessionText then
            readGlobalPreference ()
        else
            ensureRoot (SessionId.create sessionText)
