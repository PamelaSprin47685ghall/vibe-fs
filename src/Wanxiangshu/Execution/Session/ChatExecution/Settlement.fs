namespace Wanxiangshu.Execution.Session.ChatExecution

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type PreProviderSettlementError =
    | MissingAccepted of ChatExecutionKey
    | EvidenceConflict of AcceptedChatExecutionEvidence * AcceptedChatExecutionEvidence
    | ProviderAlreadyStarted of ChatExecutionKey
    | TerminalConflict of ChatExecutionTerminalDisposition * ChatExecutionTerminalDisposition
    | InvalidDisposition of ChatExecutionTerminalDisposition
    | ProjectionMissingAfterCommit of ChatExecutionKey
    | ProjectionConflictAfterCommit of ChatExecutionState
    | PersistenceFailed of JournalAppendFailure

type PreProviderTerminalWitness = PreProviderTerminalWitness of ChatExecutionKey * ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
module PreProviderTerminalWitness =

    let key (PreProviderTerminalWitness(key, _)) = key
    let disposition (PreProviderTerminalWitness(_, disposition)) = disposition

type PreProviderSettlementPersistence =
    { ReadExact: ChatExecutionKey -> ChatExecutionState option
      AppendTerminal:
          ChatExecutionKey
              -> AcceptedChatExecutionEvidence
              -> ChatExecutionTerminalDisposition
              -> Task<Result<unit, JournalAppendFailure>> }

[<RequireQualifiedAccess>]
module PreProviderSettlement =

    let private validDisposition =
        function
        | ChatExecutionTerminalDisposition.Cancelled
        | ChatExecutionTerminalDisposition.Rejected
        | ChatExecutionTerminalDisposition.Failed -> true
        | ChatExecutionTerminalDisposition.Completed -> false

    let private currentDecision key evidence disposition (state: ChatExecutionState option) =
        match state with
        | None -> Error(PreProviderSettlementError.MissingAccepted key)
        | Some current when current.acceptedEvidence <> evidence ->
            Error(PreProviderSettlementError.EvidenceConflict(current.acceptedEvidence, evidence))
        | Some(ChatExecutionState.Accepted _) -> Ok true
        | Some(ChatExecutionState.Started _) -> Error(PreProviderSettlementError.ProviderAlreadyStarted key)
        | Some(ChatExecutionState.EndedBeforeStart(establishedEvidence, establishedOutcome)) when
            PreStartOutcome.disposition establishedOutcome = disposition
            && establishedEvidence = evidence
            ->
            Ok false
        | Some(ChatExecutionState.EndedBeforeStart(_, establishedOutcome)) ->
            Error(
                PreProviderSettlementError.TerminalConflict(PreStartOutcome.disposition establishedOutcome, disposition)
            )
        | Some(ChatExecutionState.EndedAfterStart(_, establishedDisposition)) ->
            Error(PreProviderSettlementError.TerminalConflict(establishedDisposition, disposition))

    let private witness key evidence disposition persistence =
        match persistence.ReadExact key with
        | Some(ChatExecutionState.EndedBeforeStart(establishedEvidence, establishedOutcome)) when
            establishedEvidence = evidence
            && PreStartOutcome.disposition establishedOutcome = disposition
            ->
            Ok(PreProviderTerminalWitness(key, PreStartOutcome.disposition establishedOutcome))
        | None -> Error(PreProviderSettlementError.ProjectionMissingAfterCommit key)
        | Some state -> Error(PreProviderSettlementError.ProjectionConflictAfterCommit state)

    let settleWith
        (persistence: PreProviderSettlementPersistence)
        (key: ChatExecutionKey)
        (evidence: AcceptedChatExecutionEvidence)
        (disposition: ChatExecutionTerminalDisposition)
        : Task<Result<PreProviderTerminalWitness, PreProviderSettlementError>> =
        taskResult {
            do!
                if validDisposition disposition then
                    Ok()
                else
                    Error(PreProviderSettlementError.InvalidDisposition disposition)

            let! append = currentDecision key evidence disposition (persistence.ReadExact key)

            if append then
                do!
                    persistence.AppendTerminal key evidence disposition
                    |> TaskResult.mapError PreProviderSettlementError.PersistenceFailed

            return! witness key evidence disposition persistence
        }
