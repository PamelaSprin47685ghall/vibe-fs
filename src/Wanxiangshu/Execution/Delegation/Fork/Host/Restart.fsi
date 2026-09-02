namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Restart recovery for linked children. Terminal path: ChildRecoveryWorkflow
/// → ChildRecoveryResult → recordCompletion → PulseAgentHandle. Fail closed on proof.
/// Clean-break: legacy abort blobs never publish; retired false terminals refuse (no replacement).
/// GREEN-4: returns HandleFamilyRecovery (query result), never option/missing-port.
/// EXEC-024: agent mailbox is wake-only (PulseAgentHandle); no agent completion payload path.
module HostForkRestart =

    /// Active handle: Domain recoverChild via production interpreter.
    val recoverChild:
        runtime: ForkRuntime ->
        snapshot: ISessionSnapshotPort option ->
        journal: AgentJournal option ->
        parentId: SessionId ->
        agentId: string ->
        childSessionId: SessionId ->
        role: Role ->
        agent: string ->
            Task<ChildRecoveryResult>

    /// EXEC-009 restart recovery: rebuild parent join mailbox from durable handles.
    /// Returns HandleFamilyRecovery for SessionRecovery RestoreHandles (GREEN-4).
    val restoreLinkedChildren:
        runtime: ForkRuntime ->
        snapshot: ISessionSnapshotPort option ->
        journal: AgentJournal ->
        parentId: SessionId ->
        children: Dictionary<string, SessionId> ->
        childCreatedDir: (string -> SessionId -> string option -> unit) ->
        directoryOf: (string -> string option) ->
            Task<HandleFamilyRecovery>
