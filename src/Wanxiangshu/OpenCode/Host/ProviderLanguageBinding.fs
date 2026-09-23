namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider

/// HOST-026: observes the global preference; the provider owner interprets and binds it.
[<RequireQualifiedAccess>]
module ProviderLanguageBinding =

    let mutable private hostConfigPreference: string option = None

    let setHostConfigPreference (raw: string) : unit =
        if not (isNull (box raw)) then
            hostConfigPreference <- Some raw

    let clearHostConfigPreferenceForTests () : unit = hostConfigPreference <- None

    let private valueOrRaise =
        function
        | Ok value -> value
        | Error error -> raise (InvalidOperationException error)

    [<Emit("typeof Intl !== 'undefined' && Intl.DateTimeFormat ? (Intl.DateTimeFormat().resolvedOptions().locale || '') : ''")>]
    let private jsIntlLocale () : string = jsNative

    let readGlobalPreference () : ProviderLanguage =
        let explicit =
            Environment.GetEnvironmentVariable "WANXIANGSHU_PROVIDER_LANGUAGE"
            |> Option.ofObj

        let vscodeNls =
            Environment.GetEnvironmentVariable "VSCODE_NLS_CONFIG" |> Option.ofObj

        let posixLocale =
            Environment.GetEnvironmentVariable "LC_ALL"
            |> Option.ofObj
            |> Option.orElseWith (fun () -> Environment.GetEnvironmentVariable "LC_MESSAGES" |> Option.ofObj)
            |> Option.orElseWith (fun () -> Environment.GetEnvironmentVariable "LANG" |> Option.ofObj)

        let intlLocale =
            try
                let loc = jsIntlLocale ()
                if String.IsNullOrEmpty loc then None else Some loc
            with _ ->
                None

        ProviderLanguage.fromObservationLadder explicit hostConfigPreference vscodeNls posixLocale intlLocale
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
