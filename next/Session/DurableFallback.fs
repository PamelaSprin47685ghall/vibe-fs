namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Domain

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

    let currentState (sessionId: SessionId) (projSet: ProjectionSet) : AgentPairCursor.FallbackCursor =
        match Map.tryFind sessionId projSet.AgentProjections.Sessions with
        | Some sessionProj ->
            match sessionProj.Fallback with
            | Some fb ->
                { AgentPairCursor.FallbackCursor.Offset = fb.Offset
                  AgentPairCursor.FallbackCursor.LastProviderAttempt = fb.LastProviderAttempt }
            | None -> AgentPairCursor.initial
        | None -> AgentPairCursor.initial

    /// 0.5.0: provider retry count never kills a Logical Run.
    /// Returns the cursor for the next provider attempt.
    let nextDecision (sessionId: SessionId) (projSet: ProjectionSet) : AgentPairCursor.FallbackCursor =
        currentState sessionId projSet

    let currentSide (sessionId: SessionId) (projSet: ProjectionSet) : AgentPairCursor.ModelSide =
        AgentPairCursor.side (currentState sessionId projSet).Offset
