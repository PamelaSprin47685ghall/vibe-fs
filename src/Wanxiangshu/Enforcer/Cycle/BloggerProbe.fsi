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
open Wanxiangshu.Mission.Obligation.Todo

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

    [<Literal>]
    val BloggerMissingToolRepairKind: string = "blogger-missing-tool"

    [<Literal>]
    val BloggerAabbRepairKind: string = "blogger-aabb"

    val repairClaimedForKind:
        journal: Wanxiangshu.Persistence.Journal.AgentJournal ->
        bloggerSessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        requestId: Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        terminalRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        repairKind: string ->
            bool

    val repairClaimedFor:
        journal: Wanxiangshu.Persistence.Journal.AgentJournal ->
        bloggerSessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        requestId: Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        terminalRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
            bool

    val repairDispatchExists:
        projections: Wanxiangshu.Composition.Durable.AgentProjectionSet ->
        bloggerSessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        requestId: Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        terminalRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        repairKind: string ->
            bool

    val repairIssuedForKind:
        journal: Wanxiangshu.Persistence.Journal.AgentJournal ->
        bloggerSessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        requestId: Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        terminalRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        repairKind: string ->
            bool

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

    val isCompletedChronicle: part: Wanxiangshu.OpenCode.SessionToolPart -> bool

    val hasExactlyOneCompletedChronicle: parts: Wanxiangshu.OpenCode.SessionToolPart array -> bool

    val completedAssistantEvidence: messages: Wanxiangshu.OpenCode.SessionMessage list -> (string * bool) list
