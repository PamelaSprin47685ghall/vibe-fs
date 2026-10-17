namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// JS-native ANCHOR boundary: numbering, one-turn view declarations and the
/// selection/token report used by the transform pipeline. Numbers are
/// process-live (a restart resets them); the ledger owns no durable fact.
[<RequireQualifiedAccess>]
module AnchorSurface =

    val numberMessage: sessionId: string -> messageId: string -> int
    val anchors: sessionId: string -> obj array
    val setView: sessionId: string -> declaration: obj -> unit
    val takeView: sessionId: string -> obj
    val countTokens: text: string -> int
    /// Apply the pending declaration to `[{ id, text }, ...]`; returns
    /// `{ messages, prose, report }` without consuming the declaration twice.
    val applyView: sessionId: string -> messages: obj array -> obj
    val clearSession: sessionId: string -> unit
    val clearAll: unit -> unit
