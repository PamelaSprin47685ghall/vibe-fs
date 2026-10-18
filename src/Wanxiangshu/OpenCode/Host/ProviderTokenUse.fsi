namespace Wanxiangshu.OpenCode

/// The provider's own billing for a session's most recent assistant turn, read
/// from the host's message events and disclosed to the model as one datum.
module ProviderTokenUse =

    type Usage = { Cached: int; Uncached: int }

    val record: sessionId: string -> cached: int -> uncached: int -> unit
    val tryFind: sessionId: string -> Usage option
    val clearSession: sessionId: string -> unit
    val clearAll: unit -> unit
