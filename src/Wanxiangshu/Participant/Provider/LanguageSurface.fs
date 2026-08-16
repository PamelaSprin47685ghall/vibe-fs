namespace Wanxiangshu.Participant.Provider

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Resources

/// Provider-language owner boundary. Language values, session binding and
/// localized resources cross as strings and plain objects; ProviderLanguage,
/// SessionProviderLanguage and host/runtime state stay private.
module ProviderLanguageSurface =

    let private languageOf (raw: string) : ProviderLanguage =
        match raw with
        | "English" -> ProviderLanguage.English
        | "SimplifiedChinese" -> ProviderLanguage.SimplifiedChinese
        | _ -> ProviderLanguage.parse raw

    let private languageName =
        function
        | ProviderLanguage.English -> "English"
        | ProviderLanguage.SimplifiedChinese -> "SimplifiedChinese"

    let private resultOf result =
        match result with
        | Ok value -> box {| ok = true; value = languageName value; error = "" |}
        | Error error -> box {| ok = false; value = ""; error = error |}

    let private mapOf (value: obj) : Map<string, string> =
        if isNull value then
            Map.empty
        else
            let keys: string array = emitJsExpr value "Object.keys($0)"

            keys
            |> Array.toList
            |> List.map (fun key -> key, string (emitJsExpr (value, key) "$0[$1]"))
            |> Map.ofList

    let parse (raw: string) : string = ProviderLanguage.parse raw |> languageName

    let nameOf (raw: string) : string = languageOf raw |> languageName

    let tryParse (raw: string) : obj =
        match ProviderLanguage.tryParse raw with
        | Some language -> box (languageName language)
        | None -> null

    let label (raw: string) : string = ProviderLanguage.label (languageOf raw)

    let resourceDirectory (raw: string) : string = ProviderLanguage.resourceDirectory (languageOf raw)

    let resourceFileName (raw: string) : string = ProviderLanguage.resourceFileName (languageOf raw)

    let inheritFrom (raw: string) : string = ProviderLanguage.inheritFrom (languageOf raw) |> languageName

    let clearAllForTests () : unit = SessionProviderLanguage.clearAllForTests ()

    let tryGet (sessionId: string) : obj =
        match SessionProviderLanguage.tryGet (SessionId.create sessionId) with
        | Some language -> box (languageName language)
        | None -> null

    let bindOnce (sessionId: string) (language: string) : obj =
        SessionProviderLanguage.bindOnce (SessionId.create sessionId) (languageOf language) |> resultOf

    let inheritFromOwner (ownerLanguage: string) (childSessionId: string) : obj =
        SessionProviderLanguage.inheritFromOwner (languageOf ownerLanguage) (SessionId.create childSessionId)
        |> resultOf

    let readGlobalPreference () : string = ProviderLanguageBinding.readGlobalPreference () |> languageName

    let ensureRoot (sessionId: string) : string =
        ProviderLanguageBinding.ensureRoot (SessionId.create sessionId) |> languageName

    let ensureInherited (ownerSessionId: string) (childSessionId: string) : string =
        ProviderLanguageBinding.ensureInherited (SessionId.create ownerSessionId) (SessionId.create childSessionId)
        |> languageName

    let languageOfSession (sessionId: string) : string =
        ProviderProse.languageOf (SessionId.create sessionId) |> languageName

    let languageRootsPresent () : bool = ProviderResources.languageRootsPresent ()

    let relativePath (language: string) (semanticPath: string) : string =
        ProviderResources.relativePath (languageOf language) semanticPath

    let exists (language: string) (semanticPath: string) : bool =
        ProviderResources.exists (languageOf language) semanticPath

    let readText (language: string) (semanticPath: string) : string =
        ProviderResources.readText (languageOf language) semanticPath

    let requireLanguagePair (semanticPath: string) : unit = ProviderResources.requireLanguagePair semanticPath

    let substitute (template: string) (substitutions: obj) : string =
        ProviderProse.substitute template (mapOf substitutions)

    let loadBookkeeperSystem (language: string) : string =
        PromptResources.loadBookkeeperSystemFor (languageOf language)

    /// Exercise the real host transform at the provider-language boundary for
    /// the Bookkeeper-owned system segment. The attachment fixture is private;
    /// host-owned system bytes remain caller data and are never rewritten.
    let transformBookkeeperSystem (sessionId: string) (system: string array) : Task<obj> =
        task {
            let sid = SessionId.create sessionId
            ProviderLanguageBinding.ensureRoot sid |> ignore
            BookkeeperRuntime.bindSession sessionId "provider-language-surface" "provider-language-surface"

            try
                let input = createObj [ "sessionID" ==> sessionId; "model" ==> createObj [] ]
                let output = createObj [ "system" ==> system ]
                let! _ = ProviderSystemTransform.create None input output
                return box {| system = unbox<string array> output?system |}
            finally
                BookkeeperRuntime.unbindSession sessionId
        }
