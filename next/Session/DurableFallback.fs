namespace Wanxiangshu.Next.Session

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

type FallbackJournalPort =
    { AppendFact: StreamId -> AgentFact -> Result<ProjectionSet, string> }

module FallbackJournalPort =

    let fromAgentJournal (journal: AgentJournal) : FallbackJournalPort =
        { AppendFact =
            fun stream fact ->
                match AgentJournal.appendAgent stream None fact journal with
                | Ok proj -> Ok proj
                | Error failure -> Error(sprintf "%A" failure.Failure) }

module DurableFallback =

    let currentState (sessionId: SessionId) (projSet: ProjectionSet) : FallbackMemory =
        match Map.tryFind sessionId projSet.AgentProjections.Sessions with
        | Some sessionProj ->
            match sessionProj.Fallback with
            | Some fb -> { Offset = fb.Offset }
            | None -> Fallback.initial
        | None -> Fallback.initial

    /// 0.5.0: provider retry count never kills a Logical Run.
    let isDead (_sessionId: SessionId) (_projSet: ProjectionSet) : bool = false

    /// Always NextAttempt with the projection Offset (upcoming request cursor).
    /// Fold advances Offset on each recorded failure; this does not advance again.
    /// After a recorded failure, Offset is already the advanced cursor for the
    /// next request (e.g. 4th/8th/12th → Offset 0 / A). Never Dead.
    let nextDecision (sessionId: SessionId) (projSet: ProjectionSet) : FallbackDecision =
        FallbackDecision.NextAttempt(currentState sessionId projSet)

    let currentSide (sessionId: SessionId) (projSet: ProjectionSet) : Wanxiangshu.Next.Session.ModelSide =
        let state = currentState sessionId projSet
        Fallback.currentSide state
