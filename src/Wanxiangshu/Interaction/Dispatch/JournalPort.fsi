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

    /// Actual ownership from a live attachment lease or a durable association.
    /// Missing evidence does not prove a lifecycle.
    abstract OwnershipEvidenceFor: sessionId: SessionId -> SessionOwnership option

    /// A live producer's exact ownership proof for an InternalLeaf child.
    abstract InternalLeafOwnerFor: sessionId: SessionId -> SessionId option

    abstract Append:
        sessionId: SessionId ->
        providerRun: ProviderRunIdentity option ->
        fact: PromptSessionFact ->
            Task<Result<unit, JournalAppendFailure>>

    abstract HandleForChild: sessionId: SessionId -> Wanxiangshu.Execution.Delegation.HandleRecord option

    abstract ChatAcceptancePersistence:
        unit -> Wanxiangshu.Execution.Session.ChatExecution.ManagedChatAcceptancePersistence
