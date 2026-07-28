namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostReviewGuard =

    type ReviewGuardAvailability =
        | ReviewGuardMissing of treeHash: string
        | ReviewGuardConfirmed
        | ReviewGuardUnavailable of reason: string

    let missingTree (journal: AgentJournal option) (gitTreePort: GitTreePort option) sessionId =
        match journal, gitTreePort with
        | None, _ -> ReviewGuardUnavailable "Review guard requires an AgentJournal"
        | _, None -> ReviewGuardUnavailable "Review guard requires a GitTreePort"
        | Some journal, Some port ->
            try
                let treeHash = port.GetTreeHash()

                let emptyTree = "4b825dc642cb6eb9a060e54bf8d69288fbee4904"
                let treeHash = treeHash.Trim()

                let isEmpty =
                    String.IsNullOrWhiteSpace treeHash
                    || treeHash.Equals("NO_HEAD_TREE", StringComparison.Ordinal)
                    || treeHash.Equals(emptyTree, StringComparison.Ordinal)

                if isEmpty then
                    ReviewGuardMissing treeHash
                else
                    let snapshot = AgentJournal.snapshot journal

                    let sessionOpt =
                        Map.tryFind (SessionId.create sessionId) snapshot.AgentProjections.Sessions

                    match sessionOpt with
                    | None -> ReviewGuardMissing treeHash
                    | Some session ->
                        match session.ReviewGuard with
                        | Some guard when guard.IsConfirmed && guard.LastGitTreeHash = Some(GitTreeHash.create treeHash) ->
                            ReviewGuardConfirmed
                        | _ -> ReviewGuardMissing treeHash
            with ex ->
                ReviewGuardUnavailable(sprintf "Review guard dependency failed: %s" ex.Message)

    let private hasAcceptedGuardKey (journal: AgentJournal) (targetSessionId: SessionId) (guardKey: string) =
        AgentJournal.snapshot journal
        |> fun projection ->
            match Map.tryFind targetSessionId projection.AgentProjections.Sessions with
            | Some session ->
                match session.ReviewGuard with
                | Some guard -> guard.AcceptedGuardKey = Some guardKey
                | None -> false
            | None -> false

    let private createGuardKey (targetSessionId: SessionId) (triggerMessageId: string) (reason: string) =
        sprintf "review-guard:%s:%s:%s" (SessionId.value targetSessionId) triggerMessageId reason

    let private sendGuardNudge
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (sessionId: string)
        (triggerMessageId: string)
        (reason: string)
        (prompt: string)
        (agent: string)
        (model: OpencodeModel option)
        (onContinuationAccepted: SessionId -> MessageId -> unit)
        =
        let journal =
            match journal with
            | Some journal -> journal
            | None -> raise (InvalidOperationException "Review guard nudge requires an AgentJournal")

        let targetSessionId = SessionId.create sessionId
        let guardKey = createGuardKey targetSessionId triggerMessageId reason

        // Durable + in-memory dedupe only after a successful send. Adding the
        // key before SendPrompt permanently blocks retries when the host rejects
        // the first attempt (SSOT: failure must not write acceptance).
        if
            not (hasAcceptedGuardKey journal targetSessionId guardKey)
            && not (nudgeKeys.Contains guardKey)
        then
            let continuationKind =
                match agent, reason with
                | "reviewer", r when r.Contains("confirm-perfect") -> PromptAuthority.ReviewConfirmation
                | "reviewer", _ -> PromptAuthority.ReviewerGuard
                | _ -> PromptAuthority.ManagerGuard

            HostSessionNudge.sendContinuation
                sessionPort
                targetSessionId
                prompt
                continuationKind
                { Model = model
                  Agent = Some agent
                  Directory = None
                  Metadata = None }
                (Some journal)
                (Some(fun hostMessageId ->
                    nudgeKeys.Add guardKey |> ignore

                    // prompt_async may only return a synthetic admission id
                    // (accepted-<session>). That must not become the durable
                    // ConfirmationPromptMessageId: the second PERFECT proves
                    // causality against the real chat.message user id. Persist
                    // only real host message ids here; the chat.message hook
                    // rewrites/records the authoritative id when metadata lands.
                    let hostId = MessageId.value hostMessageId

                    if not (hostId.StartsWith("accepted-")) then
                        let fact =
                            AgentFact.GuardPromptAccepted
                                {| TargetSessionId = targetSessionId
                                   GuardKey = guardKey
                                   HostMessageId = hostId |}

                        match AgentJournal.appendAgent (StreamId.Session targetSessionId) None fact journal with
                        | Ok _ -> onContinuationAccepted targetSessionId hostMessageId
                        | Error failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf "Failed to persist review guard prompt acceptance: %A" failure.Failure
                                )
                            )
                    else
                        // Keep the claim key marked so we do not re-send, but
                        // leave ConfirmationPromptMessageId unset until chat.message.
                        ()))

    let nudgeManager
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        sessionId
        messageId
        treeHash
        (model: OpencodeModel option)
        (onContinuationAccepted: SessionId -> MessageId -> unit)
        =
        sendGuardNudge
            sessionPort
            journal
            nudgeKeys
            sessionId
            messageId
            (sprintf "missing-review:%s" treeHash)
            "Review is required before completion. Fork or nudge a Reviewer until the current Git tree has two distinct PERFECT verdicts."
            "manager"
            model
            onContinuationAccepted

    let nudgeReviewer
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        sessionId
        messageId
        (model: OpencodeModel option)
        (onContinuationAccepted: SessionId -> MessageId -> unit)
        =
        sendGuardNudge
            sessionPort
            journal
            nudgeKeys
            sessionId
            messageId
            "missing-verdict"
            "Submit a structured verdict with the verdict tool: PERFECT or REVISE. Do not put a verdict in prose."
            "reviewer"
            model
            onContinuationAccepted

    /// First PERFECT on this tree is recorded but not yet confirmed (KISS-N07:
    /// "PERFECT requires confirmation"). This is the ONLY code path that produces
    /// a ConfirmationPromptMessageId; the resulting HostMessageId becomes the
    /// causal proof the second PERFECT's root user message must match. Without
    /// this nudge, two independent PERFECT calls could never be distinguished
    /// from a genuine confirmed round-trip -- fail-closed confirmation requires
    /// this to actually fire.
    let confirmPerfect
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        sessionId
        messageId
        (model: OpencodeModel option)
        (onContinuationAccepted: SessionId -> MessageId -> unit)
        =
        sendGuardNudge
            sessionPort
            journal
            nudgeKeys
            sessionId
            messageId
            "confirm-perfect"
            "PERFECT requires confirmation. Re-read the current tree and call verdict(PERFECT) again to confirm."
            "reviewer"
            model
            onContinuationAccepted
