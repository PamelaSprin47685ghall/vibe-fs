namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

module ProviderRecoveryWorkflow =
    val awaitRecoveryMaterial: host: IBloggerRuntimeHost -> durable: AgentJournal -> mainSessionId: SessionId -> Task

    val admitPolicyAuthorizedFailure:
        durable: AgentJournal ->
        turn: ReconciledTurn ->
        failure: ExecutionFailure ->
        requestKind: ProviderRequestKind ->
        error: string ->
            Task<Result<FailureAdmissionOutcome, string>>

    val continueAfterConfirmedFailure:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        recoveryPort: ProviderRecoveryJournalPort option ->
        scope: IBloggerRuntimeHost ->
        turn: ReconciledTurn ->
        failure: ExecutionFailure ->
        error: string ->
        continuationPrompt: string ->
            Task
