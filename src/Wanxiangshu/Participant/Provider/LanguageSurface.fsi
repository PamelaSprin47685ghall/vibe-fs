namespace Wanxiangshu.Participant.Provider

open System.Threading.Tasks

/// Provider-language owner boundary. Language values, session binding and
/// localized resources cross as strings and plain objects; ProviderLanguage,
/// SessionProviderLanguage and host/runtime state stay private.
module ProviderLanguageSurface =

    val parse: raw: string -> string
    val nameOf: raw: string -> string
    val tryParse: raw: string -> obj
    val label: raw: string -> string
    val resourceDirectory: raw: string -> string
    val resourceFileName: raw: string -> string
    val inheritFrom: raw: string -> string
    val clearAllForTests: unit -> unit
    val tryGet: sessionId: string -> obj
    val bindOnce: sessionId: string -> language: string -> obj
    val inheritFromOwner: ownerLanguage: string -> childSessionId: string -> obj
    val readGlobalPreference: unit -> string
    val ensureRoot: sessionId: string -> string
    val ensureInherited: ownerSessionId: string -> childSessionId: string -> string
    val languageOfSession: sessionId: string -> string
    val languageRootsPresent: unit -> bool
    val relativePath: language: string -> semanticPath: string -> string
    val exists: language: string -> semanticPath: string -> bool
    val readText: language: string -> semanticPath: string -> string
    val requireLanguagePair: semanticPath: string -> unit
    val substitute: template: string -> substitutions: obj -> string
    val loadBookkeeperSystem: language: string -> string

    /// Exercise the real host transform at the provider-language boundary for
    /// the Bookkeeper-owned system segment. The attachment fixture is private;
    /// host-owned system bytes remain caller data and are never rewritten.
    val transformBookkeeperSystem: sessionId: string -> system: string array -> Task<obj>
