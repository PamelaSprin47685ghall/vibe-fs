namespace Wanxiangshu.OpenCode

/// The provider's own declaration of how many context tokens the session's
/// model is configured for — read where opencode hands over the resolved model
/// (the chat.params barrier), never derived from our own measurements.
module DeclaredContextLimit =
    val record: sessionId: string -> contextTokens: int -> unit
    val tryFind: sessionId: string -> int option
    val clearSession: sessionId: string -> unit
    val clearAll: unit -> unit
