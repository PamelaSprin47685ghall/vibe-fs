namespace Wanxiangshu.Participant.Provider

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module SessionProviderLanguage =
    /// Language of a session: always the live global preference.
    val languageOf: _sessionId: SessionId -> ProviderLanguage
