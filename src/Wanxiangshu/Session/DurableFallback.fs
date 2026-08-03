namespace Wanxiangshu.Session

open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Journal
open Wanxiangshu.Domain

type FallbackJournalPort =
    { AppendFact: StreamId -> AgentFact -> Result<ProjectionSet, string> }

module FallbackJournalPort =

    let fromAgentJournal (journal: AgentJournal) : FallbackJournalPort =
        { AppendFact =
            fun stream fact ->
                match AgentJournal.appendAgent stream None fact journal with
                | Ok projection -> Ok projection
                | Error failure -> Error(sprintf "%A" failure) }

/// Reads the durable fallback cursor. Never writes it — FALLBACK-003 gives the
/// only advance to the FallbackController.
module DurableFallback =

    /// The whole durable fallback state for a session.
    ///
    /// FALLBACK-001 has the Authority Root create this, so it already holds the
    /// only correct LogicalRunId and AuthorityRootUserMessageId for the run. The
    /// controller reads them from here rather than being passed them, because a
    /// second source for those two values can disagree with this one.
    let tryCurrentState (sessionId: SessionId) (projection: ProjectionSet) : FallbackProjection option =
        AgentProjection.tryFind sessionId projection.AgentProjections
        |> Option.bind (fun session -> session.Fallback)

    /// The cursor as the journal has it.
    ///
    /// Returns the projection's cursor directly. The previous version rebuilt one
    /// field by field, so every new cursor field had to be copied here as well or
    /// would silently read as its default — which is exactly how
    /// ConsecutiveFailureCount would have arrived as 0 on every read, making
    /// FALLBACK-005's budget permanently full.
    ///
    /// `None` when the session has no fallback projection. FALLBACK-001 says the
    /// cursor is created by the Authority Root, so its absence means no root was
    /// accepted; substituting `initial` would make "no proven authority" look like
    /// "a fresh run at Offset 0".
    let tryCurrentCursor (sessionId: SessionId) (projection: ProjectionSet) : AgentPairCursor.FallbackCursor option =
        tryCurrentState sessionId projection
        |> Option.map (fun fallback -> fallback.Cursor)

    /// FALLBACK-002: which side the next attempt lands on.
    let tryCurrentSide (sessionId: SessionId) (projection: ProjectionSet) : AgentPairCursor.ModelSide option =
        tryCurrentCursor sessionId projection
        |> Option.map (fun cursor -> AgentPairCursor.side cursor.Offset)

    /// The EffectiveAgent a continuation must physically use (PROMPT-003).
    ///
    /// No cursor means no accepted Authority Root (FALLBACK-001), so there is no
    /// fallback state to consult and SelectedAgent is the only defensible answer.
    ///
    /// The single source for this question: both the busy nudge and the guard nudge
    /// asked it, and a second copy could answer with the other side of the pair for
    /// the same cursor.
    let effectiveAgentForActiveCursor
        (sessionId: SessionId)
        (projection: ProjectionSet)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : string =
        tryCurrentCursor sessionId projection
        |> Option.map (PromptAuthority.effectiveAgentFor profile)
        |> Option.defaultValue profile.SelectedAgent

    /// FALLBACK-005: whether the automatic recovery budget still permits an
    /// attempt.
    ///
    /// `false` for an unknown session: no proven authority means no automatic
    /// physical request.
    let mayContinue (budget: int) (sessionId: SessionId) (projection: ProjectionSet) : bool =
        AgentProjection.tryFind sessionId projection.AgentProjections
        |> Option.bind (fun session -> session.Fallback)
        |> Option.map (FallbackProjection.mayContinue budget)
        |> Option.defaultValue false
