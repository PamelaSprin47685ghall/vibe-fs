namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Recovery
open Wanxiangshu.Session

/// Confirmed-failure recovery: wait for blogger coverage material, then continue.
/// Temporal shape is journal predicate + journal change signal + one IDeadlineHandle
/// (G4R-CE S2 / CausalAwait.untilSignalOrDeadline). No slice poll, no UtcNow.
module ProviderRecoveryWorkflow =

    /// CTX-006 hasMaterial for X: BlogObservationCommitted coverage on the main session.
    let private sessionHasCoverage (durable: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot durable).AgentProjections
        |> Option.bind (fun state -> state.Blog)
        |> Option.map BlogProjection.hasCoverage
        |> Option.defaultValue false

    /// True when a Companion Blogger is linked — only then is waiting for coverage
    /// meaningful. Sessions without a blogger never grow coverage; waiting would
    /// only burn the A′ budget on a clock.
    let private expectsCoverage (durable: AgentJournal) (sessionId: SessionId) =
        SessionAssociationProjection.tryBloggerOf
            sessionId
            (AgentJournal.snapshot durable).AgentProjections.Associations
        |> Option.isSome

    /// Bound: after a confirmed failure, the next A′/B′ ProviderRetry is the only
    /// recovery slot that may probe (CTX-006/010). Blog frames often land a few
    /// tens of ms after the failed main turn's companion request — racing the
    /// continue send made hasMaterial=false, so AttemptPlanner skipped the probe
    /// and ClearRecovery burned the armed slot.
    ///
    /// Wait on journal folds until coverage exists or the injectable deadline
    /// fires. Fail open: WaitTimedOut still sends the ordinary main
    /// (CTX-011 no-candidate path).
    let awaitRecoveryMaterial (timerPort: ITimerPort) (durable: AgentJournal) (sessionId: SessionId) : Task =
        task {
            if not (expectsCoverage durable sessionId) then
                return ()
            else
                let tryRead () =
                    if sessionHasCoverage durable sessionId then
                        Some()
                    else
                        None

                match tryRead () with
                | Some() -> return ()
                | None ->
                    let sessionKey = SessionId.value sessionId

                    let descriptor =
                        DiagnosticWait.create
                            "provider-recovery-material"
                            (CausalOwner.create "ProviderRecoveryWorkflow" [ "session", sessionKey ])
                            [ "session", sessionKey; "name", "coverage" ]
                            (ExternalProducer("journal", [ "session", sessionKey ]))
                            [ WaitEscape.OpenEndedExternal ]
                            "ProviderRecoveryWorkflow.awaitRecoveryMaterial"

                    let deadline = timerPort.Delay 2000

                    let awaitSignal () =
                        task {
                            let fromRev = AgentJournal.revision durable
                            let! _ = AgentJournal.awaitChangeFrom fromRev durable
                            return ()
                        }

                    match!
                        CausalAwait.untilSignalOrDeadline CausalWaitHub.observer descriptor deadline tryRead awaitSignal
                    with
                    | Ok() -> return ()
                    | Error DiagnosticWaitExit.WaitTimedOut -> return ()
                    | Error _ -> return ()
        }

    /// FALLBACK-003 + FALLBACK-004: a settled failed turn.
    ///
    /// The reconciled snapshot is what proves the attempt failed (HOST-004), so
    /// this is where the cursor advances — not in the Host retry event handler,
    /// which only wakes. `FallbackLedger` is the Application single writer.
    ///
    /// FALLBACK-004 then decides whether a continuation follows: only when the
    /// budget still permits one. The continuation itself produces no second
    /// advance, which is why nothing here writes again.
    let continueAfterConfirmedFailure
        (timerPort: ITimerPort)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (error: string)
        (continuationPrompt: string)
        : Task =
        task {
            let fail reason =
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                |> ignore

            match journal with
            | None -> fail error
            | Some durable ->
                match
                    FallbackLedger.recordConfirmedFailure
                        durable
                        AgentPairCursor.DefaultAutoRecoveryBudget
                        turn.SessionId
                        turn.ProviderRun
                        error
                with
                | Error reason -> fail reason
                | Ok ConfirmedFailureOutcome.RecoveryExhausted -> fail error
                // A second observe of the same APIError must not NotifyTerminal Failed or
                // issue a second continuation; the original admitted recovery remains owner.
                | Ok ConfirmedFailureOutcome.AlreadyRecorded
                | Ok ConfirmedFailureOutcome.NoActiveRun -> ()
                | Ok ConfirmedFailureOutcome.RecoveryAdvanced ->
                    // The cursor advanced and budget permits the A′ continuation.
                    // CTX-006: give the linked Blogger a chance to commit coverage
                    // before the armed A′/B′ continue is planned (XWire.applyTransform).
                    do! awaitRecoveryMaterial timerPort durable turn.SessionId

                    let! continuation =
                        HostSessionNudge.sendContinuationResult
                            sessionPort
                            turn.SessionId
                            continuationPrompt
                            PromptAuthority.ProviderRetryAttempt
                            turn.Directory
                            journal
                            PromptDispatcher.AwaitMode.Detached
                            None

                    match continuation with
                    | Ok _ ->
                        if error = "loop-kill" then
                            Diagnostic.emit
                                "loop-kill"
                                [ "session_id", SessionId.value turn.SessionId; "result", "continue-sent" ]
                    | Error _ -> fail error
        }
        :> Task

    /// LOOP-006: an abort we armed is provider failure for AABB purposes.
    let continueAfterLoopKill
        (timerPort: ITimerPort)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task =
        continueAfterConfirmedFailure
            timerPort
            sessionPort
            eventPort
            journal
            turn
            "loop-kill"
            (ProviderProse.documentFor turn.SessionId RuntimeNudge.LoopContinue Map.empty)
