// primary_owner: delegation — Delegation.ChildOutcome/Delegation.Contract — SPLIT — Join outcome slice surface
namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host

/// Direct-CE join ops (FLOW-001 / PR5).
/// Permit-gated only — no bare runtime.Join, no Command/Reply AST.
module Join =

    /// EXEC-018 batch join: maxCount + typed local interrupt (≠ runtime.Cancel).
    let joinAvailable
        (runtime: HostForkRuntime)
        (permit: FamilyRecoveryPermit)
        (maxCount: int)
        (interrupt: Task<JoinInterruptReason>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        HostForkJoin.joinAvailableWithPermit runtime permit maxCount interrupt
