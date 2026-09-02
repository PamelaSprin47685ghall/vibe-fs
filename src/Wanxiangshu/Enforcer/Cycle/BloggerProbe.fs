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

/// ENFORCER-153: Blogger missing-tool recovery stage is DERIVED, never stored.
///
/// The repair stage (one nudge, then request-scoped AABB occasions while the
/// shared fallback budget remains) is a pure function of durable
/// InteractionRepair claims and the provider-visible
/// transcript. Recovery is never stored on a runtime cell: the hot path
/// (EnforcerHost.handleContinuation) reads `repairState`, and the crash window
/// (BloggerCrashRecovery.reconcile) reads `rejudgeToolRecovery`. A restart
/// re-derives the same stage from the same evidence, so the budget cannot be
/// stolen or duplicated across a crash.
module BloggerRecoveryProbe =

    /// Must match EnforcerHost interactionNudge repairKind (ENFORCER-066 claim scope).
    [<Literal>]
    let BloggerMissingToolRepairKind = "blogger-missing-tool"

    /// Durable marker for one request+terminal-scoped fallback/AABB continuation.
    [<Literal>]
    let BloggerAabbRepairKind = "blogger-aabb"

    [<RequireQualifiedAccess>]
    type InvalidTerminalRepairState =
        | NoRecovery
        | InteractionNudgeIssued of ProviderRunIdentity
        | AabbRepairIssued of ProviderRunIdentity

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

    /// ENFORCER-153 pure rejudge from already-resolved evidence.
    ///
    /// `claimedTerminalRun`: durable InteractionRepair claim for blogger-missing-tool
    /// (payload digests terminal run). `completedAssistants`: chronological completed
    /// assistant terminals as (runId, hasBlogToolCall).
    ///
    /// Conservative (no AABB re-spend without second pure-prose evidence; no second
    /// nudge when claim exists):
    /// - no claim → NoRecovery
    /// - claim + no blog after claim (any number of pure-prose terminals) →
    let private recoveryAfterClaimed (claimed: string) (afterClaimed: (string * bool) list) : BloggerToolRecovery =
        let hasBlogAfter = afterClaimed |> List.exists (fun (_, hasBlog) -> hasBlog)

        if hasBlogAfter then
            BloggerToolRecovery.NoRecovery
        else
            // No durable AABB evidence exists here: never invent AabbRepairIssued. Restore as
            // InteractionNudgeIssued claimed; the hot path re-runs aabbRepair
            // on the next *new* pure-prose terminal (issuedRun <> terminalRun).
            BloggerToolRecovery.InteractionNudgeIssued(ProviderRunIdentity.create claimed)

    ///   InteractionNudgeIssued claimed
    /// - claim + valid blog after claim → NoRecovery (cycle completed / success)
    ///
    /// Pure transcript evidence never derives AabbRepairIssued. Idle-issued
    /// AABB has a separate durable `blogger-aabb` InteractionRepair claim; the
    /// journal-aware probes below may restore that stage with the exact terminal
    /// identity, but this pure function must never invent it from prose terminals alone.
    let private entriesAfterClaimed (claimed: string) (completedAssistants: (string * bool) list) =
        match completedAssistants |> List.skipWhile (fun (id, _) -> id <> claimed) with
        | _ :: rest -> rest
        | [] ->
            // Claimed run absent from transcript: keep nudge stage, never invent AABB.
            []

    let private recoveryForClaimedEvidence (claimed: string) (completedAssistants: (string * bool) list) =
        match completedAssistants |> List.tryFind (fun (id, _) -> id = claimed) with
        | Some(_, true) -> BloggerToolRecovery.NoRecovery
        | _ -> recoveryAfterClaimed claimed (entriesAfterClaimed claimed completedAssistants)

    let rejudgeFromEvidence
        (claimedTerminalRun: string option)
        (completedAssistants: (string * bool) list)
        : BloggerToolRecovery =
        match claimedTerminalRun with
        | None -> BloggerToolRecovery.NoRecovery
        | Some claimed -> recoveryForClaimedEvidence claimed completedAssistants

    let private isCompletedChronicle (part: SessionToolPart) =
        part.ToolName = "chronicle"
        && match part.State with
           | SnapshotToolPartState.Completed _ -> true
           | _ -> false

    let private hasExactlyOneCompletedChronicle (parts: SessionToolPart array) =
        match parts |> Array.filter (fun part -> part.ToolName = "chronicle") with
        | [| part |] -> isCompletedChronicle part
        | _ -> false

    /// Completed assistant terminals: (message id = ProviderRunIdentity, exact-one chronicle success).
    let private completedAssistantEvidence (messages: SessionMessage list) : (string * bool) list =
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
    let private repairClaimedForKind
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

    let private repairClaimedFor journal bloggerSessionId requestId terminalRun =
        repairClaimedForKind journal bloggerSessionId requestId terminalRun BloggerMissingToolRepairKind

    let private repairDispatchExists
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

    let private repairIssuedForKind
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        (terminalRun: ProviderRunIdentity)
        (repairKind: string)
        =
        let projections = (AgentJournal.snapshot journal).AgentProjections

        repairClaimedForKind journal bloggerSessionId requestId terminalRun repairKind
        && repairDispatchExists projections bloggerSessionId requestId terminalRun repairKind

    let private claimRunCandidate (prefix: string) (suffix: string) (scope: string) (sequence: int) : string option =
        let runLength = scope.Length - prefix.Length - suffix.Length
        let runStart = min prefix.Length scope.Length
        let runId = scope.Substring(runStart, max 0 runLength)

        if
            sequence <= 0
            || runLength <= 0
            || not (scope.StartsWith(prefix, System.StringComparison.Ordinal))
            || not (scope.EndsWith(suffix, System.StringComparison.Ordinal))
            || System.String.IsNullOrWhiteSpace runId
        then
            None
        else
            Some runId

    /// When the claimed terminal is absent from the Host snapshot, recover its run id
    /// from the exact request-scoped ClaimSequences shape:
    /// session \u001f logical-run \u001f InteractionRepair \u001f request \u001f terminal \u001f kind.
    let private claimedRunFromSequencesFor
        (repairKind: string)
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        : string option =
        let projections = (AgentJournal.snapshot journal).AgentProjections

        match
            PromptAuthorityLedger.activeProfile bloggerSessionId projections,
            PromptAuthorityLedger.projectionFor bloggerSessionId projections
        with
        | Some profile, Some authProj ->
            let prefix =
                System.String.Join(
                    "\u001f",
                    [| SessionId.value profile.SessionId
                       LogicalRunId.value profile.LogicalRunId
                       "InteractionRepair"
                       BloggerRequestId.value requestId |]
                )
                + "\u001f"

            let suffix = "\u001f" + repairKind

            authProj.ClaimSequences
            |> Map.toList
            |> List.tryPick (fun (scope, seq) ->
                claimRunCandidate prefix suffix scope seq
                |> Option.filter (fun runId ->
                    repairDispatchExists
                        projections
                        bloggerSessionId
                        requestId
                        (ProviderRunIdentity.create runId)
                        repairKind))
        | _ -> None

    let private claimedRunFromSequences journal bloggerSessionId requestId =
        claimedRunFromSequencesFor BloggerMissingToolRepairKind journal bloggerSessionId requestId

    let private aabbClaimedRun journal bloggerSessionId requestId =
        claimedRunFromSequencesFor BloggerAabbRepairKind journal bloggerSessionId requestId
        |> Option.map ProviderRunIdentity.create

    /// Invalid-terminal stage preserves WHICH terminal most recently proves an
    /// issued AABB occasion. Idle may be delivered repeatedly for one terminal,
    /// so exact-current identity is part of the idempotency proof; a prior AABB
    /// on another terminal means "already in AABB", not "budget exhausted".
    let repairStateForInvalidTerminal
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        (terminalRun: ProviderRunIdentity)
        : InvalidTerminalRepairState =
        let aabb =
            if repairIssuedForKind journal bloggerSessionId requestId terminalRun BloggerAabbRepairKind then
                Some(InvalidTerminalRepairState.AabbRepairIssued terminalRun)
            else
                claimedRunFromSequencesFor BloggerAabbRepairKind journal bloggerSessionId requestId
                |> Option.map (fun claimedRun ->
                    InvalidTerminalRepairState.AabbRepairIssued(ProviderRunIdentity.create claimedRun))

        let nudge =
            if repairClaimedFor journal bloggerSessionId requestId terminalRun then
                Some(InvalidTerminalRepairState.InteractionNudgeIssued terminalRun)
            else
                claimedRunFromSequences journal bloggerSessionId requestId
                |> Option.map (fun claimedRun ->
                    InvalidTerminalRepairState.InteractionNudgeIssued(ProviderRunIdentity.create claimedRun))

        aabb
        |> Option.orElse nudge
        |> Option.defaultValue InvalidTerminalRepairState.NoRecovery

    let private toolRecoveryOfInvalidTerminal =
        function
        | InvalidTerminalRepairState.NoRecovery -> BloggerToolRecovery.NoRecovery
        | InvalidTerminalRepairState.InteractionNudgeIssued run -> BloggerToolRecovery.InteractionNudgeIssued run
        | InvalidTerminalRepairState.AabbRepairIssued run -> BloggerToolRecovery.AabbRepairIssued run

    /// ENFORCER-153: rejudge BloggerToolRecovery from claim + Host transcript.
    let rejudgeToolRecovery
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        (messages: SessionMessage list)
        : BloggerToolRecovery =
        let terminals = completedAssistantEvidence messages

        let claimedFromTerminals =
            terminals
            |> List.tryPick (fun (id, _) ->
                let run = ProviderRunIdentity.create id

                if repairClaimedFor journal bloggerSessionId requestId run then
                    Some id
                else
                    None)

        let claimedTerminalRun =
            match claimedFromTerminals with
            | Some _ as hit -> hit
            | None -> claimedRunFromSequences journal bloggerSessionId requestId

        match aabbClaimedRun journal bloggerSessionId requestId with
        | Some run -> BloggerToolRecovery.AabbRepairIssued run
        | None -> rejudgeFromEvidence claimedTerminalRun terminals

    let private providerRunFromHostValue (value: obj) : ProviderRunIdentity option =
        let runIdOpt = if isNull value then None else Some(unbox<string> value)

        match runIdOpt with
        | Some runId when not (System.String.IsNullOrWhiteSpace runId) -> Some(ProviderRunIdentity.create runId)
        | _ -> None

    let private hostInfo (message: obj) =
        if isNull message then None
        elif isNull message?info then Some message
        else Some message?info

    let private repairMarkerTargetRun (requestKey: string) (message: obj) : ProviderRunIdentity option =
        hostInfo message
        |> Option.bind (fun info ->
            let matches =
                not (isNull info)
                && not (isNull info?source)
                && unbox<string> info?source = "interaction-repair"
                && not (isNull info?synthetic)
                && unbox<bool> info?synthetic
                && not (isNull info?requestKey)
                && unbox<string> info?requestKey = requestKey

            if matches then
                providerRunFromHostValue info?repairTerminalRun
            else
                None)

    /// Exact terminal targeted by a raw Host AABB repair instruction injected for `requestKey`.
    /// The marker is provider-visible evidence, but its terminal identity is Host-only metadata.
    let private aabbRepairTargetRun (requestKey: string) (rawMessages: obj list) : ProviderRunIdentity option =
        rawMessages |> List.rev |> List.tryPick (repairMarkerTargetRun requestKey)

    /// ENFORCER-153 hot path: derive recovery state from durable claim + visible
    /// transcript. No mutable runtime field is consulted.
    ///
    /// The claim check is two-tier: the same terminal re-entering the transform
    /// is a re-fire (InteractionNudgeIssued for that terminal), while a NEW pure
    /// prose terminal after any InteractionRepair claim means the nudge
    /// semantically failed and the AABB budget is spent on injection — not on
    /// a second nudge.
    let repairState
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (requestKey: string)
        (terminalRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : BloggerToolRecovery =
        match aabbRepairTargetRun requestKey rawMessages with
        | Some run -> BloggerToolRecovery.AabbRepairIssued run
        | None ->
            repairStateForInvalidTerminal journal bloggerSessionId (BloggerRequestId.create requestKey) terminalRun
            |> toolRecoveryOfInvalidTerminal
