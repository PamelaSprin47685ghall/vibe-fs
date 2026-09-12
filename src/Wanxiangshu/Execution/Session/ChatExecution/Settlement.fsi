namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Foundation
open System.Threading.Tasks

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
    | PreProviderTerminalWitness of ChatExecutionKey * ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
module PreProviderTerminalWitness =
    val key: PreProviderTerminalWitness -> ChatExecutionKey
    val disposition: PreProviderTerminalWitness -> ChatExecutionTerminalDisposition

type PreProviderSettlementPersistence =
    { ReadExact: ChatExecutionKey -> ChatExecutionState option
      AppendTerminal:
          ChatExecutionKey
              -> AcceptedChatExecutionEvidence
              -> ChatExecutionTerminalDisposition
              -> Task<Result<unit, JournalAppendFailure>> }

[<RequireQualifiedAccess>]
module PreProviderSettlement =
    val settleWith:
        persistence: PreProviderSettlementPersistence ->
        key: ChatExecutionKey ->
        evidence: AcceptedChatExecutionEvidence ->
        disposition: ChatExecutionTerminalDisposition ->
            Task<Result<PreProviderTerminalWitness, PreProviderSettlementError>>

