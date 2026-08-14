namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Direct-CE join ops (FLOW-001 / PR5).
/// Permit-gated only — no bare runtime.Join, no Command/Reply AST.
module Join =

    /// Single-result join (FamilyRecoveryPermit required).
    let joinAny (runtime: HostForkRuntime) (permit: FamilyRecoveryPermit) : Task<Result<RunCompletion, ForkError>> =
        HostForkJoin.joinWithPermit runtime permit None

    /// EXEC-018 batch join: maxCount + typed local interrupt (≠ runtime.Cancel).
    let joinAvailable
        (runtime: HostForkRuntime)
        (permit: FamilyRecoveryPermit)
        (maxCount: int)
        (interrupt: Task<JoinInterruptReason>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        HostForkJoin.joinAvailableWithPermit runtime permit maxCount interrupt
