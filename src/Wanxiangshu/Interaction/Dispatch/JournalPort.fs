namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Persistence.Journal.JournalOutcome
open Wanxiangshu.Execution.Session
open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Interaction.Authority

type IPromptJournal =
    abstract RuntimeId: RuntimeId
    abstract ProjectionFor: sessionId: SessionId -> PromptAuthority.PromptAuthorityProjection

    /// interaction-authority-018: the durable session-ownership evidence of
    /// one session, as the association projection records it.
    ///
    /// This is the evidence an agent-owner root's life is derived from: the
    /// association says whether the session is a fork child, an attached
    /// delegate or an internal leaf. It is durable, so a restart proves the
    /// same life.
    abstract OwnershipEvidenceFor: sessionId: SessionId -> SessionOwnership option

    abstract Append:
        sessionId: SessionId ->
        providerRun: ProviderRunIdentity option ->
        fact: PromptSessionFact ->
            Task<Result<unit, JournalAppendFailure>>

    abstract HandleForChild: sessionId: SessionId -> Wanxiangshu.Execution.Delegation.HandleRecord option

    abstract ChatAcceptancePersistence:
        unit -> Wanxiangshu.Execution.Session.ChatExecution.ManagedChatAcceptancePersistence
