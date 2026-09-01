namespace Wanxiangshu.Enforcer.Cycle

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

module BloggerRecoveryProbe =

    [<RequireQualifiedAccess>]
    type InvalidTerminalRepairState =
        | NoRecovery
        | InteractionNudgeIssued of ProviderRunIdentity
        | AabbRepairIssued of ProviderRunIdentity

    [<Literal>]
    val BloggerMissingToolRepairKind: string = "blogger-missing-tool"

    [<Literal>]
    val BloggerAabbRepairKind: string = "blogger-aabb"

    val terminalRequestOwnershipForPhysicalMessage:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext ->
        Wanxiangshu.Foundation.Identity.PhysicalUserMessageId ->
            Wanxiangshu.Context.Companion.Blogger.BloggerTerminalRequestOwnership

    val terminalRequestOwnershipForProviderRun:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext ->
        Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        obj list ->
            Wanxiangshu.Context.Companion.Blogger.BloggerTerminalRequestOwnership

    val rejudgeFromEvidence:
        string option -> (string * bool) list -> Wanxiangshu.Context.Companion.Blogger.Runtime.BloggerToolRecovery

    val repairStateForInvalidTerminal:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
            InvalidTerminalRepairState

    val rejudgeToolRecovery: AgentJournal -> SessionId -> BloggerRequestId -> SessionMessage list -> BloggerToolRecovery

    val repairState:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        string ->
        Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        obj list ->
            Wanxiangshu.Context.Companion.Blogger.Runtime.BloggerToolRecovery
