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

    let currentState (sessionId: SessionId) (projSet: ProjectionSet) : FallbackState =
        match Map.tryFind sessionId projSet.AgentProjections.Sessions with
        | Some sessionProj ->
            match sessionProj.Fallback with
            | Some fb ->
                let side =
                    match fb.Side with
                    | Wanxiangshu.Next.Journal.ModelSide.SideA -> ModelSide.A
                    | Wanxiangshu.Next.Journal.ModelSide.SideB -> ModelSide.B

                { Side = side
                  Failures = fb.TotalFailures }
            | None -> Fallback.initial
        | None -> Fallback.initial

    let isDead (sessionId: SessionId) (projSet: ProjectionSet) : bool =
        match Map.tryFind sessionId projSet.AgentProjections.Sessions with
        | Some session -> session.Fallback |> Option.exists (fun fallback -> fallback.IsDead)
        | None -> false

    let nextDecision (sessionId: SessionId) (projSet: ProjectionSet) : FallbackDecision =
        // SSOT §6: A(0) → A retry(1) → permanent switch B(2) → B retry(3) → Dead(4).
        // nextDecision returns the model to use for the NEXT attempt given the
        // current cumulative failure count. A gets two attempts (original + retry);
        // B gets two attempts (original + retry); the 5th failure is Dead.
        // The returned Failures = current count + 1 (the count if this attempt fails).
        // ModelResolver reads the fold's Side directly for production model
        // selection; nextDecision is the decision contract for callers and tests.
        if isDead sessionId projSet then
            FallbackDecision.Dead
        else
            let state = currentState sessionId projSet

            match state.Failures with
            | 0 -> FallbackDecision.NextAttempt { Side = ModelSide.A; Failures = 1 }
            | 1 -> FallbackDecision.NextAttempt { Side = ModelSide.A; Failures = 2 }
            | 2 -> FallbackDecision.NextAttempt { Side = ModelSide.B; Failures = 3 }
            | 3 -> FallbackDecision.NextAttempt { Side = ModelSide.B; Failures = 4 }
            | _ -> FallbackDecision.Dead

    let recordFailure
        (journalPort: FallbackJournalPort)
        (sessionId: SessionId)
        (reason: string)
        : Result<ProjectionSet * FallbackDecision, string> =
        let fact =
            AgentFact.FallbackFailureRecorded
                {| SessionId = sessionId
                   Reason = reason
                   AssistantMessageId =
                    sprintf "manual-%s-%s" (SessionId.value sessionId) (Guid.NewGuid().ToString("N"))
                   ProviderAttempt = "manual" |}

        match journalPort.AppendFact (StreamId.Session sessionId) fact with
        | Ok updatedProj ->
            let decision = nextDecision sessionId updatedProj
            Ok(updatedProj, decision)
        | Error err -> Error err
