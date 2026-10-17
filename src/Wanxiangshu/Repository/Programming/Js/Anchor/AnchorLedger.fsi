namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// Process-live ANCHOR ledger and view store (PLAN phase 3). Numbers are a
/// per-process monotonic stream; a session's message ids map to stable numbers
/// until the process dies. The view declaration is one-turn: the consumer takes
/// it exactly once.
module AnchorLedger =
    /// Stable anchor number for one message id inside one session.
    val numberMessage: sessionId: string -> messageId: string -> int

    /// Anchors known for a session, sorted ascending.
    val anchors: sessionId: string -> (int * string) list

    /// Store the next-turn view declaration for a session.
    val setView: sessionId: string -> declaration: AnchorViewDeclaration -> unit

    /// Consume the pending declaration exactly once.
    val takeView: sessionId: string -> AnchorViewDeclaration option

    val clearSession: sessionId: string -> unit
    val clearAll: unit -> unit
