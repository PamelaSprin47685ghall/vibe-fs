namespace Wanxiangshu.Repository.Knowledge.Casebook

/// One question/answer turn captured while building a Case draft.
type CasebookTurn = { Q: string; A: string option }

/// A per-session list of captured draft turns.
type CasebookDraft = { Turns: CasebookTurn list }

module CasebookDraftStore =

    /// Set or update the current question for the session draft.
    val setQ: sessionId: string -> q: string -> unit

    /// Set or update the current answer for the session draft.
    val setA: sessionId: string -> a: string -> unit

    /// Remove and return the draft for the given session, if present.
    val tryTake: sessionId: string -> CasebookDraft option

    /// Remove the draft for the given session.
    val clear: sessionId: string -> unit

    /// Render a list of turns as a canonical Q/A transcript.
    val transcript: turns: CasebookTurn list -> string
