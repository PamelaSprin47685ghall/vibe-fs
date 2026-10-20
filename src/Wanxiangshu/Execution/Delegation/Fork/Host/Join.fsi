namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Execution.Session.Wait

module HostForkJoin =
    /// Retained admitted work and current process handles from the owner's projection.
    val ownerWorkHandle: runtime: HostForkRuntime -> record: HandleRecord -> bool

    val joinAvailable:
        runtime: HostForkRuntime ->
        maxCount: int ->
        interrupt: Task<JoinInterruptReason> ->
            Task<Result<JoinWaitOutcome<JoinItem>, ForkError>>

    val joinAvailableForFissionLane:
        runtime: HostForkRuntime ->
        groupId: string ->
        laneIndex: int ->
        maxCount: int ->
        interrupt: Task<JoinInterruptReason> ->
            Task<Result<JoinWaitOutcome<JoinItem>, ForkError>>

    val joinAvailableWithPermit:
        runtime: HostForkRuntime ->
        permit: FamilyRecoveryPermit ->
        maxCount: int ->
        interrupt: Task<JoinInterruptReason> ->
            Task<Result<JoinWaitOutcome<JoinItem>, ForkError>>

    val awaitAgent:
        runtime: HostForkRuntime -> agentId: string -> timeoutMs: int option -> Task<Result<RunCompletion, string>>

    val awaitAgentWithPermit:
        runtime: HostForkRuntime ->
        permit: FamilyRecoveryPermit ->
        agentId: string ->
        timeoutMs: int option ->
            Task<Result<RunCompletion, ForkError>>

    val cancelAgent: runtime: HostForkRuntime -> agentId: string -> unit
