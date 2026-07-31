namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// The one production path that turns a reconciled turn into side effects
/// (NotifyTerminal, dispose runtime, nudges, fallback advance).
module TurnCompletionProgram =

    /// FALLBACK-008: one repair per unusable terminal.
    ///
    /// The task is awaited rather than discarded. `|> ignore` on the task also
    /// discarded the claim/abandon bookkeeping inside it, so a failed repair left
    /// a Claimed fact with nothing after it and no terminal for the caller.
    let private sendRepair
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (prompt: string)
        (repairKind: string)
        : Task =
        task {
            let! sent =
                HostSessionNudge.trySendInteractionRepair
                    sessionPort
                    turn.SessionId
                    prompt
                    turn.Directory
                    journal
                    turn.ProviderRun
                    repairKind
                    None

            match sent with
            | Ok _ -> ()
            | Error _ ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "MISSING_FINAL_REPORT")
                |> ignore
        }
        :> Task

    /// FALLBACK-003 + FALLBACK-004: a settled failed turn.
    ///
    /// The reconciled snapshot is what proves the attempt failed (HOST-004), so
    /// this is where the cursor advances — not in the Host retry event handler,
    /// which only wakes. `FallbackController` is the single writer.
    ///
    /// FALLBACK-004 then decides whether a continuation follows: only when the
    /// budget still permits one. The continuation itself produces no second
    /// advance, which is why nothing here writes again.
    let private continueAfterProviderFailure
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (error: string)
        : Task =
        task {
            let fail reason =
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                |> ignore

            match journal with
            | None -> fail error
            | Some durable ->
                match
                    FallbackController.recordConfirmedFailure
                        durable
                        AgentPairCursor.DefaultAutoRecoveryBudget
                        turn.SessionId
                        turn.ProviderRun
                        error
                with
                | Error reason -> fail reason
                | Ok outcome when not (FallbackController.mayContinue outcome) ->
                    // FALLBACK-005: budget spent, or no proven authority. Either way
                    // no further automatic physical request may be issued.
                    fail error
                | Ok _ ->
                    let! continuation =
                        HostSessionNudge.sendContinuationResult
                            sessionPort
                            turn.SessionId
                            RuntimeNudge.providerRetry
                            PromptAuthority.ProviderRetryAttempt
                            turn.Directory
                            journal
                            None

                    match continuation with
                    | Ok _ -> ()
                    | Error _ -> fail error
        }
        :> Task

    /// Apply the full terminal completion program for a reconciled turn.
    let applyWithContinuation
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (verdictSessions: HashSet<string>)
        (nudgeSent: HashSet<string>)
        (managerGuardNudges: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        (disposeExecutorRuntime: string -> unit)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        : Task =
        let sessionKey = SessionId.value turn.SessionId
        disposeExecutorRuntime sessionKey

        // EXEC-009 + PROMPT-008: a reconciled linked child has a host-proven physical
        // user message even when the Host omitted agent metadata from `chat.message`,
        // so its AgentOwner Root can be registered here. The managed agent name comes
        // from the durable `HandleLinked.TargetAgent` and nowhere else — rebuilding it
        // from the child's AgentRole invented tier Fast, so a `deep-coder` child
        // acquired a root naming `fast-coder` and FALLBACK-002's A/B pair was wrong
        // for the whole Logical Run.
        //
        // A session known only through the in-memory `sessionParents` map has no
        // durable record and therefore no defensible agent name. It is skipped rather
        // than registered from a guess: the turn still completes through its
        // reconciled AgentRole, and the missing authority stays visibly missing.
        TerminalPolicy.tryLinkedChild journal sessionKey
        |> Option.iter (fun record ->
            HostSessionNudge.ensureAgentOwnerAuthority
                journal
                turn.SessionId
                turn.PhysicalUserMessageId
                record.TargetAgent
            |> ignore)

        let completeReviewerOrAssistant (forceConfirmedReviewer: bool) =
            let wasAborted = abortedSessions.Contains sessionKey
            abortedSessions.Remove sessionKey |> ignore

            // HOST-005: session-wide A is this turn's text plus reasoning appended
            // to everything the Session accumulated. An empty intermediate turn
            // does not wipe prior A.
            let sessionWide = TerminalSessionA.accumulateTurn eventPort turn

            let sessionWideText =
                if not (String.IsNullOrWhiteSpace sessionWide) then
                    sessionWide
                elif forceConfirmedReviewer then
                    // A confirmed double-PERFECT often ends on a tool-only frame.
                    // The witness is already Confirmed, so expose a minimal A rather
                    // than failing a review that actually succeeded.
                    "Review confirmed."
                else
                    sessionWide

            // REVIEW-006: nothing is written here. Confirmation is a fact
            // ReviewController already journalled from the seal evidence, so the
            // completion path only reports the run. The previous code wrote its own
            // confirmation fact keyed by the confirmation prompt's physical message
            // id, which is REVIEW-003's forbidden physical-message match wearing a
            // different name.
            // PROMPT-008: the Role comes from the reconciled turn, and there is no
            // default. Defaulting to Coder — as the previous `"coder"` string did —
            // reports a completion under a role nobody selected.
            match turn.AgentRole with
            | None ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "completed with no resolved role")
                |> ignore
            | Some role ->
                let runResult: AgentRunResult =
                    { SessionId = turn.SessionId
                      AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                      ProviderRun = turn.ProviderRun
                      Role = AgentRoleIdentity.toRole role
                      Directory = turn.Directory
                      SessionWideText = sessionWideText
                      TurnFormalText = CompletedTurnClassifier.partsText turn.Parts }

                // EXEC-006: `IsValid` is the single place that decides whether a
                // completed run carries session-wide A. Re-testing the text here
                // would be a second copy of that rule.
                if runResult.IsValid then
                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                    |> ignore
                else
                    eventPort.NotifyTerminal
                        turn.SessionId
                        (TerminalOutcome.Failed "completed with empty session-wide text")
                    |> ignore

            wasAborted

        let reviewerAlreadyConfirmed =
            turn.AgentRole = Some AgentRole.Reviewer
            && ReviewerGuardState.isConfirmedReviewer journal sessionKey

        match turn.Outcome with
        | TurnUnknown -> AsyncSupport.completedTask ()
        | TurnInProgress when reviewerAlreadyConfirmed ->
            // A second PERFECT is frequently a tool-only provider step. Once the
            // witness is Confirmed, finish the physical reviewer run so
            // OrchestratorHost.reverify and Manager `join` observe completion.
            completeReviewerOrAssistant true |> ignore
            AsyncSupport.completedTask ()
        | TurnInProgress ->
            // The Host settled a provider step with tool calls only. Interaction
            // repair continues the Logical Run; this is never provider fallback.
            if CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                sendRepair sessionPort eventPort journal turn "\u200B" "zero-width"
            else
                AsyncSupport.completedTask ()
        | TurnNeedsContinuation _ when reviewerAlreadyConfirmed ->
            completeReviewerOrAssistant true |> ignore
            AsyncSupport.completedTask ()
        | TurnNeedsContinuation _ ->
            // Absorb text and reasoning into session-wide A even though this turn is
            // not completable, then ask for the missing report. Still not fallback.
            TerminalSessionA.accumulateTurn eventPort turn |> ignore

            sendRepair sessionPort eventPort journal turn RuntimeNudge.missingFinalReport "missing-final-report"
        | TurnAborted reason ->
            abortedSessions.Add sessionKey |> ignore
            Pty.abortParent sessionKey
            sessionPort.AbortChildren turn.SessionId |> ignore

            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
            |> ignore

            AsyncSupport.completedTask ()
        | TurnFailed error -> continueAfterProviderFailure sessionPort eventPort journal turn error
        | TurnCompleted ->
            let wasAborted = completeReviewerOrAssistant reviewerAlreadyConfirmed

            if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                AsyncSupport.completedTask ()
            else
                match turn.AgentRole with
                // REVIEW-003: a first PERFECT must enter its causal confirmation
                // round-trip before the generic missing-verdict branch.
                // `verdictSessions` is terminal bookkeeping only and must never
                // suppress the pending confirmation transition.
                | Some AgentRole.Reviewer when ReviewerGuardState.pendingConfirmation journal sessionKey ->
                    verdictSessions.Remove sessionKey |> ignore

                    HostReviewGuard.requestPerfectConfirmation
                        sessionPort
                        journal
                        nudgeSent
                        turn.SessionId
                        turn.ProviderRun
                    :> Task
                | Some AgentRole.Reviewer when
                    not (verdictSessions.Remove sessionKey)
                    && not (ReviewerGuardState.submitted journal sessionKey)
                    ->
                    HostReviewGuard.nudgeReviewer sessionPort journal nudgeSent turn.SessionId turn.ProviderRun :> Task
                | Some AgentRole.Manager when TerminalPolicy.isTopLevelManager sessionParents journal sessionKey ->
                    match HostReviewGuard.missingTree journal gitTreePort sessionKey with
                    | HostReviewGuard.ReviewGuardMissing treeHash ->
                        HostReviewGuard.nudgeManager
                            sessionPort
                            journal
                            managerGuardNudges
                            turn.SessionId
                            turn.ProviderRun
                            treeHash
                        :> Task
                    | HostReviewGuard.ReviewGuardConfirmed -> AsyncSupport.completedTask ()
                    // ORCH-008 / REVIEW-007 fail closed: an unavailable guard must not
                    // let a Manager finish unreviewed. Reported as a terminal failure
                    // rather than raised, because raising here escapes into whichever
                    // Host callback happens to be on the stack.
                    | HostReviewGuard.ReviewGuardUnavailable reason ->
                        eventPort.NotifyTerminal
                            turn.SessionId
                            (TerminalOutcome.Failed(sprintf "Review guard unavailable: %s" reason))
                        |> ignore

                        AsyncSupport.completedTask ()
                | _ -> AsyncSupport.completedTask ()
