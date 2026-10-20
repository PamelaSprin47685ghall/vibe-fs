namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// Process-live state. Replacements retain the bounds of the interval they changed.
module AnchorLedger =
    val numberMessage: sessionId: string -> messageId: string -> int
    val knownAnchors: sessionId: string -> int list
    val remember: sessionId: string -> messages: (string option * string) list -> unit
    val context: sessionId: string -> (string option * string) list
    val anchors: sessionId: string -> (int * string) list
    val setView: sessionId: string -> declaration: AnchorViewDeclaration -> unit
    val takeView: sessionId: string -> AnchorViewDeclaration option
    val replacements: sessionId: string -> AnchorReplacement list
    val clearSession: sessionId: string -> unit
    val clearAll: unit -> unit
