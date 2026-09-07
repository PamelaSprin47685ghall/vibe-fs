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
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Pure fact readers for Blogger repair dispatch receipts, terminal ownership,
/// and completion chronicles. Stage DUs and reconstruction are removed (R04).
module BloggerRecoveryProbe =

    /// Must match EnforcerHost interactionNudge repairKind (ENFORCER-066 claim scope).
    [<Literal>]
    let BloggerMissingToolRepairKind = "blogger-missing-tool"

    /// Durable marker for one request+terminal-scoped AABB repair continuation.
    [<Literal>]
    let BloggerAabbRepairKind = "blogger-aabb"

    let terminalRequestOwnershipForPhysicalMessage
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (request: BloggerRequestContext)
        (physicalUserMessageId: PhysicalUserMessageId)
        : BloggerTerminalRequestOwnership =
        let projections = (AgentJournal.snapshot journal).AgentProjections
        let mainSessionId = BloggerRequestContext.mainSessionId request
        let requestId = BloggerRequestContext.requestId request

        let openRequest =
            projections.Sessions
            |> Map.tryFind mainSessionId
            |> Option.bind (fun session -> session.BloggerCycles)
            |> Option.bind (BloggerCycleProjection.tryOpenByBlogger bloggerSessionId)

        let parent =
            PromptAuthorityLedger.acceptedDispatchForPhysicalMessage bloggerSessionId physicalUserMessageId projections
            |> Option.map (fun dispatch ->
                { PromptKey = dispatch.PromptKey
                  IsRequestScopedRepair =
                    match dispatch.Origin with
                    | PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.InteractionRepair ->
                        PromptAuthority.repairPayloadBelongsToRequest requestId dispatch.PayloadDigest
                    | _ -> false })

        BloggerRequestOwnership.decide
            requestId
            (openRequest |> Option.map (fun current -> current.RequestId))
            (openRequest |> Option.bind (fun current -> current.PromptKey))
            parent

    let terminalRequestOwnershipForProviderRun
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (request: BloggerRequestContext)
        (providerRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : BloggerTerminalRequestOwnership =
        ProviderWireCapture.tryPhysicalParentOfProviderRun providerRun rawMessages
        |> Option.map (terminalRequestOwnershipForPhysicalMessage journal bloggerSessionId request)
        |> Option.defaultValue BloggerTerminalRequestOwnership.Unproven

    let isCompletedChronicle (part: SessionToolPart) =
        part.ToolName = "chronicle"
        && match part.State with
           | SnapshotToolPartState.Completed _ -> true
           | _ -> false

    let hasExactlyOneCompletedChronicle (parts: SessionToolPart array) =
        match parts |> Array.filter (fun part -> part.ToolName = "chronicle") with
        | [| part |] -> isCompletedChronicle part
        | _ -> false

    /// Completed assistant terminals: (message id = ProviderRunIdentity, exact-one chronicle success).
    let completedAssistantEvidence (messages: SessionMessage list) : (string * bool) list =
        messages
        |> List.choose (fun m ->
            if
                m.Role = "assistant"
                && m.Completed
                && not (System.String.IsNullOrWhiteSpace m.Id)
            then
                Some(m.Id, hasExactlyOneCompletedChronicle m.ToolParts)
            else
                None)

    /// Durable claim for repairKind against one Blogger request + terminal run.
    let repairClaimedForKind
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        (terminalRun: ProviderRunIdentity)
        (repairKind: string)
        : bool =
        let projections = (AgentJournal.snapshot journal).AgentProjections

        match
            PromptAuthorityLedger.activeProfile bloggerSessionId projections,
            PromptAuthorityLedger.projectionFor bloggerSessionId projections
        with
        | Some profile, Some authProj ->
            PromptAuthority.repairAlreadyClaimed
                profile.SessionId
                profile.LogicalRunId
                requestId
                terminalRun
                repairKind
                authProj
        | _ -> false

    let repairClaimedFor journal bloggerSessionId requestId terminalRun =
        repairClaimedForKind journal bloggerSessionId requestId terminalRun BloggerMissingToolRepairKind

    let repairDispatchExists
        (projections: AgentProjectionSet)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        (terminalRun: ProviderRunIdentity)
        (repairKind: string)
        : bool =
        let payloadDigest =
            PromptAuthority.repairPayloadDigest requestId terminalRun repairKind

        match PromptAuthorityLedger.dispatchStatusFor bloggerSessionId payloadDigest projections with
        | PromptAuthorityLedger.DispatchStatus.Dispatchable -> false
        | PromptAuthorityLedger.DispatchStatus.Pending
        | PromptAuthorityLedger.DispatchStatus.Accepted _ -> true

    let repairIssuedForKind
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        (terminalRun: ProviderRunIdentity)
        (repairKind: string)
        =
        let projections = (AgentJournal.snapshot journal).AgentProjections

        repairClaimedForKind journal bloggerSessionId requestId terminalRun repairKind
        && repairDispatchExists projections bloggerSessionId requestId terminalRun repairKind
