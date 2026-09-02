namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Execution.Session.Wait

/// Direct-CE join ops (FLOW-001 / PR5).
/// Permit-gated only — no bare runtime.Join, no Command/Reply AST.
module Join =

    /// EXEC-018 batch join: maxCount + typed local interrupt (≠ runtime.Cancel).
    val joinAvailable:
        runtime: HostForkRuntime ->
        permit: FamilyRecoveryPermit ->
        maxCount: int ->
        interrupt: Task<JoinInterruptReason> ->
            Task<Result<JoinWaitOutcome<JoinItem>, ForkError>>
