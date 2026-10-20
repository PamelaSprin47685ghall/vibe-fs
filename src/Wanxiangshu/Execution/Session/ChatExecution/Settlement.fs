namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Persistence.Journal.JournalOutcome
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

    /// CUT-12: the pre-provider settlement decision is wanxiangli/chat-execution
    /// `settlePreProvider`; `Ok true` appends, `Ok false` is an exact replay.
    let private currentDecision key evidence disposition (state: ChatExecutionState option) =
        let sameAccepted =
            state |> Option.exists (fun current -> current.acceptedEvidence = evidence)

        let decision =
            ChatExecutionPhaseLaw.settlePreProvider state sameAccepted disposition

        let established () =
            match decision.Established with
            | Some established -> established
            | None -> invalidOp "wanxiangli/chat-execution terminalConflict without its established disposition"

        match decision.Verdict, state with
        | "invalidDisposition", _ -> Error(PreProviderSettlementError.InvalidDisposition disposition)
        | "missingAccepted", _ -> Error(PreProviderSettlementError.MissingAccepted key)
        | "evidenceConflict", Some current ->
            Error(PreProviderSettlementError.EvidenceConflict(current.acceptedEvidence, evidence))
        | "append", _ -> Ok true
        | "existing", _ -> Ok false
        | "providerAlreadyStarted", _ -> Error(PreProviderSettlementError.ProviderAlreadyStarted key)
        | "terminalConflict", _ -> Error(PreProviderSettlementError.TerminalConflict(established (), disposition))
        | other, _ -> invalidOp ("wanxiangli/chat-execution returned an unknown settlement verdict: " + other)

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
            let! append = currentDecision key evidence disposition (persistence.ReadExact key)

            if append then
                do!
                    persistence.AppendTerminal key evidence disposition
                    |> TaskResult.mapError PreProviderSettlementError.PersistenceFailed

            return! witness key evidence disposition persistence
        }
