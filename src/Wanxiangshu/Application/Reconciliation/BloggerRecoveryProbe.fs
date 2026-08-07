namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// ENFORCER-153: Blogger missing-tool recovery stage is DERIVED, never stored.
///
/// The stage of the one repair budget (nudge once, then one AABB) is a pure
/// function of the durable InteractionRepair claim and the provider-visible
/// transcript. Recovery is never stored on a runtime cell: the hot path
/// (EnforcerHost.handleContinuation) reads `repairState`, and the crash window
/// (BloggerCrashRecovery.reconcile) reads `rejudgeToolRecovery`. A restart
/// re-derives the same stage from the same evidence, so the budget cannot be
/// stolen or duplicated across a crash.
module BloggerRecoveryProbe =

    /// Must match EnforcerHost interactionNudge repairKind (ENFORCER-066 claim scope).
    [<Literal>]
    let BloggerMissingToolRepairKind = "blogger-missing-tool"

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
    ///   InteractionNudgeIssued claimed
    /// - claim + valid blog after claim → NoRecovery (cycle completed / success)
    ///
    /// AabbRepairConsumed is never derived on cold rejudge: AABB is memory-only
    /// (markAabbRepairConsumed + transform injection, no journal fact). A second
    /// pure-prose terminal is the trigger for aabbRepair (ENFORCER-067), not its
    /// receipt — deriving consumed here would let the hot path fatalEnd without
    /// ever injecting the AABB repair (budget stolen across a crash).
    let rejudgeFromEvidence
        (claimedTerminalRun: string option)
        (completedAssistants: (string * bool) list)
        : BloggerToolRecovery =
        match claimedTerminalRun with
        | None -> BloggerToolRecovery.NoRecovery
        | Some claimed ->
            let afterClaimed =
                completedAssistants
                |> List.skipWhile (fun (id, _) -> id <> claimed)
                |> function
                    | _ :: rest -> rest
                    | [] ->
                        // Claimed run absent from transcript: keep nudge stage, never invent AABB.
                        []

            let hasBlogAfter = afterClaimed |> List.exists (fun (_, hasBlog) -> hasBlog)

            if hasBlogAfter then
                BloggerToolRecovery.NoRecovery
            else
                // No durable AABB evidence exists (AABB = memory mark + transform
                // injection only): never invent AabbRepairConsumed. Restore as
                // InteractionNudgeIssued claimed; the hot path re-runs aabbRepair
                // on the next *new* pure-prose terminal (issuedRun <> terminalRun).
                BloggerToolRecovery.InteractionNudgeIssued(ProviderRunIdentity.create claimed)


    let private hasBlogToolCall (parts: MessagePart array) : bool =
        parts
        |> Array.exists (function
            | MessagePart.ToolCall(_, name, _) when name = "blog" -> true
            | _ -> false)

    /// Completed assistant terminals: (message id = ProviderRunIdentity, has blog tool call).
    let private completedAssistantEvidence (messages: SessionMessage list) : (string * bool) list =
        messages
        |> List.choose (fun m ->
            if
                m.Role = "assistant"
                && m.Completed
                && not (System.String.IsNullOrWhiteSpace m.Id)
            then
                Some(m.Id, hasBlogToolCall m.Parts)
            else
                None)

    /// Durable claim for repairKind against a terminal run (ClaimSequences read).
    let private repairClaimedFor
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (terminalRun: ProviderRunIdentity)
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
                terminalRun
                BloggerMissingToolRepairKind
                authProj
        | _ -> false

    /// When the claimed terminal is absent from the Host snapshot, recover its run id
    /// from ClaimSequences scopes (session \u001f run \u001f InteractionRepair \u001f run \u001f kind).
    let private claimedRunFromSequences (journal: AgentJournal) (bloggerSessionId: SessionId) : string option =
        let projections = (AgentJournal.snapshot journal).AgentProjections

        match PromptAuthorityLedger.projectionFor bloggerSessionId projections with
        | None -> None
        | Some authProj ->
            let suffix = "\u001f" + BloggerMissingToolRepairKind

            authProj.ClaimSequences
            |> Map.toList
            |> List.tryPick (fun (scope, seq) ->
                if seq < 1 then
                    None
                elif not (scope.EndsWith(suffix, System.StringComparison.Ordinal)) then
                    None
                else
                    let withoutKind = scope.Substring(0, scope.Length - suffix.Length)
                    let sep = withoutKind.LastIndexOf('\u001f')

                    if sep < 0 then
                        None
                    else
                        let runId = withoutKind.Substring(sep + 1)

                        if System.String.IsNullOrWhiteSpace runId then
                            None
                        else
                            Some runId)


    /// ENFORCER-153: rejudge BloggerToolRecovery from claim + Host transcript.
    let rejudgeToolRecovery
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (messages: SessionMessage list)
        : BloggerToolRecovery =
        let terminals = completedAssistantEvidence messages

        let claimedFromTerminals =
            terminals
            |> List.tryPick (fun (id, _) ->
                let run = ProviderRunIdentity.create id

                if repairClaimedFor journal bloggerSessionId run then
                    Some id
                else
                    None)

        let claimedTerminalRun =
            match claimedFromTerminals with
            | Some _ as hit -> hit
            | None -> claimedRunFromSequences journal bloggerSessionId

        rejudgeFromEvidence claimedTerminalRun terminals


    /// True when a raw Host message is an AABB repair instruction we injected for `requestKey`.
    let private aabbRepairInjected (requestKey: string) (rawMessages: obj list) : bool =
        rawMessages
        |> List.choose (fun m ->
            if isNull m then
                None
            else
                let info = if isNull m?info then m else m?info

                if
                    not (isNull info)
                    && not (isNull info?source)
                    && unbox<string> info?source = "interaction-repair"
                    && not (isNull info?synthetic)
                    && unbox<bool> info?synthetic
                    && not (isNull info?requestKey)
                    && unbox<string> info?requestKey = requestKey
                then
                    Some()
                else
                    None)
        |> List.isEmpty
        |> not


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
        if aabbRepairInjected requestKey rawMessages then
            BloggerToolRecovery.AabbRepairConsumed
        elif repairClaimedFor journal bloggerSessionId terminalRun then
            BloggerToolRecovery.InteractionNudgeIssued terminalRun
        else
            // A claim exists for an earlier terminal: nudge was accepted, this is
            // a new failure terminal → semantic failure, AABB applies. The payload
            // must be the CLAIMED run — handleContinuation compares it against the
            // current terminal to tell re-entry from a new failure.
            match claimedRunFromSequences journal bloggerSessionId with
            | Some claimedRun -> BloggerToolRecovery.InteractionNudgeIssued(ProviderRunIdentity.create claimedRun)
            | None -> BloggerToolRecovery.NoRecovery
