namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module ProviderLanguageBinding =
    val setHostConfigPreference: raw: string -> unit
    val clearHostConfigPreferenceForTests: unit -> unit
    val readGlobalPreference: unit -> ProviderLanguage
    val refreshGlobalLanguage: unit -> unit
    val forSession: _sessionId: SessionId -> ProviderLanguage
    val forSessionText: sessionText: string -> ProviderLanguage
