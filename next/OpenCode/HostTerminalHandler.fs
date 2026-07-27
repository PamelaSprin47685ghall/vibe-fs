namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

/// Terminal side-effect block for a consumed assistant message. Runs only when
/// the terminal actually consumed an assistant (take-and-remove returned Some)
/// and the session was not aborted. A dead session (4 consecutive fallback
/// failures, SSOT §6) is skipped silently at every prompt-send site.
module HostTerminalHandler =

    /// A session is dead after 4 consecutive fallback failures (SSOT §6:
    /// DurableFallback.nextDecision = FallbackDecision.Dead). Dead sessions must
    /// not receive internal nudges — the router has no diagnostics channel, so a
    /// dead session is skipped silently at the prompt-send sites below.
    let private sessionDead (journal: AgentJournal option) (sessionId: string) : bool =
        match journal with
        | Some j ->
            j.IsPoisoned
            || DurableFallback.isDead (SessionId.create sessionId) (AgentJournal.snapshot j)
        | None -> false

    /// reviewer missing-verdict nudge, manager review-guard evaluation, and the
    /// single zero-width continuation. `role` is the resolved agent role
    /// (message info.agent, else the known DSL role); `nudgeContinue` is the
    /// router's zero-width continuation callback (sessionId -> messageId -> unit).
    let handle
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (verdictSessions: HashSet<string>)
        (nudgeSent: HashSet<string>)
        (managerGuardNudges: HashSet<string>)
        (nudgeContinue: string -> string -> unit)
        (sessionParents: Dictionary<string, string>)
        (aborted: bool)
        (sessionId: string)
        (takenAssistant: obj option)
        (completedAssistant: obj option)
        (terminalMessageId: string)
        (terminalModel: OpencodeModel option)
        (role: string option)
        : unit =
        if not aborted && takenAssistant.IsSome then
            match role with
            | Some agent when
                agent.Equals("reviewer", StringComparison.OrdinalIgnoreCase)
                && not (verdictSessions.Remove sessionId)
                ->
                if not (sessionDead journal sessionId) then
                    HostReviewGuard.nudgeReviewer
                        sessionPort
                        journal
                        nudgeSent
                        sessionId
                        terminalMessageId
                        terminalModel
            | Some agent when
                agent.Equals("manager", StringComparison.OrdinalIgnoreCase)
                && not (sessionParents.ContainsKey sessionId)
                ->
                // Every unconfirmed manager terminal re-evaluates the guard.
                // Send is deferred one microtask so Host idle is fully released;
                // failed sends do not lock the guard key, so the next terminal retries.
                if not (sessionDead journal sessionId) then
                    match HostReviewGuard.missingTree journal gitTreePort sessionId with
                    | HostReviewGuard.ReviewGuardMissing treeHash ->
                        HostReviewGuard.nudgeManager
                            sessionPort
                            journal
                            managerGuardNudges
                            sessionId
                            terminalMessageId
                            treeHash
                            terminalModel
                    | HostReviewGuard.ReviewGuardConfirmed -> ()
                    | HostReviewGuard.ReviewGuardUnavailable reason ->
                        raise (InvalidOperationException(sprintf "Review guard unavailable: %s" reason))
            | _ -> ()

            match completedAssistant with
            | Some completeMsg when
                (completedAssistant |> Option.exists FallbackDetect.isTerminalAssistant)
                && FallbackDetect.isFailedAssistant completeMsg
                ->
                if not (sessionDead journal sessionId) then
                    nudgeContinue sessionId terminalMessageId
            | _ -> ()
