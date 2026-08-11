namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Session

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
