namespace Wanxiangshu.Interaction.Dispatch

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Authority

type IPromptJournal =
    abstract RuntimeId: RuntimeId
    abstract ProjectionFor: sessionId: SessionId -> PromptAuthority.PromptAuthorityProjection

    abstract Append:
        sessionId: SessionId ->
        providerRun: ProviderRunIdentity option ->
        fact: PromptSessionFact ->
            Task<Result<unit, JournalAppendFailure>>

    abstract HandleForChild: sessionId: SessionId -> Wanxiangshu.Execution.Delegation.HandleRecord option

    abstract ChatAcceptancePersistence:
        unit -> Wanxiangshu.Execution.Session.ChatExecution.ManagedChatAcceptancePersistence
