namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity

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
        (tryPhysicalParent: ProviderRunIdentity -> obj list -> PhysicalUserMessageId option)
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (request: BloggerRequestContext)
        (providerRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : BloggerTerminalRequestOwnership =
        tryPhysicalParent providerRun rawMessages
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
