namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// Business decisions for a fully reconciled turn are now sequenced by
/// `TurnCompletionProgram`.  This module remains as a thin public facade for
/// existing call sites; the side effects live in `TurnCompletionProgram`.
module TerminalPolicies =

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
        (continuationAccepted: SessionId -> MessageId -> unit)
        (fallbackFailures: HashSet<string>)
        (turn: ReconciledTurn)
        =
        TurnCompletionProgram.applyWithContinuation
            sessionPort
            eventPort
            journal
            gitTreePort
            verdictSessions
            nudgeSent
            managerGuardNudges
            sessionParents
            disposeExecutorRuntime
            abortedSessions
            continuationAccepted
            fallbackFailures
            turn

    let apply
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
        =
        applyWithContinuation
            sessionPort
            eventPort
            journal
            gitTreePort
            verdictSessions
            nudgeSent
            managerGuardNudges
            sessionParents
            disposeExecutorRuntime
            abortedSessions
            (fun _ _ -> ())
            (HashSet<string>())
            turn
