namespace Wanxiangshu.Execution.Session.ChatExecution

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Persistence.Journal

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

type PreProviderTerminalWitness =
    private | PreProviderTerminalWitness of ChatExecutionKey * ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
module PreProviderTerminalWitness =

    let key (PreProviderTerminalWitness(key, _)) = key
    let disposition (PreProviderTerminalWitness(_, disposition)) = disposition

type internal PreProviderSettlementPersistence =
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

    let private currentDecision key evidence disposition =
        function
        | None -> Error(PreProviderSettlementError.MissingAccepted key)
        | Some state when state.Evidence <> evidence ->
            Error(PreProviderSettlementError.EvidenceConflict(state.Evidence, evidence))
        | Some { Lifecycle = ChatExecutionLifecycle.Accepted } -> Ok true
        | Some { Lifecycle = ChatExecutionLifecycle.ProviderStarted } ->
            Error(PreProviderSettlementError.ProviderAlreadyStarted key)
        | Some { Lifecycle = ChatExecutionLifecycle.Terminal established
                 TerminalEvidence = Some(ChatExecutionTerminalEvidence.PreProvider establishedEvidence) } when
            established = disposition && establishedEvidence = evidence
            ->
            Ok false
        | Some { Lifecycle = ChatExecutionLifecycle.Terminal established } ->
            Error(PreProviderSettlementError.TerminalConflict(established, disposition))

    let private witness key evidence disposition persistence =
        match persistence.ReadExact key with
        | Some { Evidence = establishedEvidence
                 Lifecycle = ChatExecutionLifecycle.Terminal established
                 TerminalEvidence = Some(ChatExecutionTerminalEvidence.PreProvider terminalEvidence) } when
            establishedEvidence = evidence
            && terminalEvidence = evidence
            && established = disposition
            ->
            Ok(PreProviderTerminalWitness(key, established))
        | None -> Error(PreProviderSettlementError.ProjectionMissingAfterCommit key)
        | Some state -> Error(PreProviderSettlementError.ProjectionConflictAfterCommit state)

    let internal settleWith
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

    let private forJournal (journal: AgentJournal) : PreProviderSettlementPersistence =
        { ReadExact =
            fun key ->
                AgentJournal.snapshot journal
                |> fun projection -> projection.AgentProjections.ChatExecutions
                |> ChatExecutionProjection.byKey key
          AppendTerminal =
            fun key evidence disposition ->
                task {
                    let fact =
                        ChatExecutionFactCases.Terminal
                            {| SchemaVersion = 1
                               Key = key
                               Evidence = ChatExecutionTerminalEvidence.PreProvider evidence
                               Disposition = disposition |}

                    let! appended =
                        AgentJournal.appendAgent
                            (StreamId.Session key.SessionId)
                            None
                            (AgentFact.ChatExecution fact)
                            journal

                    return appended |> Result.map ignore
                } }

    let settle
        (journal: AgentJournal)
        (key: ChatExecutionKey)
        (evidence: AcceptedChatExecutionEvidence)
        (disposition: ChatExecutionTerminalDisposition)
        =
        settleWith (forJournal journal) key evidence disposition
