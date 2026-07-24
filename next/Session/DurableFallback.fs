namespace Wanxiangshu.Next.Session

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

    let nextDecision (sessionId: SessionId) (projSet: ProjectionSet) : FallbackDecision =
        let state = currentState sessionId projSet

        match state.Side, state.Failures with
        | ModelSide.A, 1 -> FallbackDecision.NextAttempt { Side = ModelSide.A; Failures = 1 }
        | ModelSide.B, 2 -> FallbackDecision.NextAttempt { Side = ModelSide.B; Failures = 2 }
        | ModelSide.B, 3 -> FallbackDecision.NextAttempt { Side = ModelSide.B; Failures = 3 }
        | _, failures when failures >= 4 -> FallbackDecision.Dead
        | _ -> Fallback.nextAttempt state

    let recordFailure
        (journalPort: FallbackJournalPort)
        (sessionId: SessionId)
        (reason: string)
        : Result<ProjectionSet * FallbackDecision, string> =
        let fact =
            AgentFact.FallbackFailureRecorded
                {| SessionId = sessionId
                   Reason = reason |}

        match journalPort.AppendFact (StreamId.Session sessionId) fact with
        | Ok updatedProj ->
            let decision = nextDecision sessionId updatedProj
            Ok(updatedProj, decision)
        | Error err -> Error err
