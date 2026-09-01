namespace Wanxiangshu.Participant.Provider

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module SessionProviderLanguage =
    val clearAllForTests: unit -> unit
    val tryGet: sessionId: SessionId -> ProviderLanguage option
    val languageOf: sessionId: SessionId -> ProviderLanguage
    val drop: sessionId: SessionId -> unit
    val bindOnce: sessionId: SessionId -> language: ProviderLanguage -> Result<ProviderLanguage, string>
    val inheritFromOwner: ownerLanguage: ProviderLanguage -> childId: SessionId -> Result<ProviderLanguage, string>
