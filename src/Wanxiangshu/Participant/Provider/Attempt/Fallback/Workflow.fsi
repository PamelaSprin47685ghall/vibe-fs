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

    /// PAR-021: the one durable fact the recovery target settlement consumes —
    /// the failed attempt itself was the LWR retry.
    val failedAttemptWasLwrRetry:
        durable: AgentJournal ->
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        providerRun: ProviderRunIdentity ->
            bool

    val continueAfterConfirmedFailure:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        scope: IBloggerRuntimeHost ->
        turn: ReconciledTurn ->
        failure: ExecutionFailure ->
        error: string ->
        continuationPrompt: string ->
            Task

    val continueDelegateCallAfterConfirmedFailure:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        scope: IBloggerRuntimeHost ->
        durable: AgentJournal ->
        turn: ReconciledTurn ->
        failure: ExecutionFailure ->
        error: string ->
            Task<RetryVerdict>
