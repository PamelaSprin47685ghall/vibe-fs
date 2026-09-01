namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module ProviderLanguageBinding =
    val readGlobalPreference: unit -> ProviderLanguage
    val ensureRoot: sessionId: SessionId -> ProviderLanguage
    val ensureInherited: ownerId: SessionId -> childId: SessionId -> ProviderLanguage
    val forSessionText: sessionText: string -> ProviderLanguage
