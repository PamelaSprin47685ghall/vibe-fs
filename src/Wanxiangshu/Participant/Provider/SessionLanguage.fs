namespace Wanxiangshu.Participant.Provider

open Wanxiangshu.Foundation.Identity

/// HOST-026: the live global preference is the only language authority.
///
/// There is no per-session binding and no durable language record: every
/// Class A render reads the current global preference, so changing the
/// setting changes the language of every subsequent request, even mid-session.
[<RequireQualifiedAccess>]
module SessionProviderLanguage =

    /// Language of a session: always the live global preference. The session
    /// id is accepted so call sites keep one resolution shape; it decides
    /// nothing.
    let languageOf (_sessionId: SessionId) : ProviderLanguage = GlobalProviderLanguage.current ()
