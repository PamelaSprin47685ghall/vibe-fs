namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Session

/// Direct-CE join ops (FLOW-001 / PR5).
/// Permit-gated only — no bare runtime.Join, no Command/Reply AST.
module Join =

    /// Single-result join (FamilyRecoveryPermit required).
    let joinAny (runtime: HostForkRuntime) (permit: FamilyRecoveryPermit) : Task<Result<RunCompletion, ForkError>> =
        runtime.JoinWithPermit(permit)

    /// EXEC-018 batch join: maxCount + local interrupt (≠ runtime.Cancel).
    let joinAvailable
        (runtime: HostForkRuntime)
        (permit: FamilyRecoveryPermit)
        (maxCount: int)
        (interrupt: Task<unit>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        runtime.JoinAvailableWithPermit(permit, maxCount, interrupt)
