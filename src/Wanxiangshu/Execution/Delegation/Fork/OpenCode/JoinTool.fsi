namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open Wanxiangshu.OpenCode

/// join() waits for the owning runtime's next physical completion batch.
/// Orchestrator join routes to ManagerJob verdict mailbox by authority role.
/// P0-RECOVERY-JOIN-001: FamilyReady permit → Join.joinAvailable (no bare Join, no AST).
/// EXEC-017: tool abort → JoinInterrupt.Signal only (≠ runtime.Cancel).
/// DevOps join: 10s timeout budget (NodeTiming.timerTask 10000). Orch/Manager join remains untimed.
module JoinTool =

    [<Literal>]
    val DevOpsJoinTimeoutMs: int = 10_000

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/join/description"

        [<Literal>]
        val RecoveryBlocked: string = "tool/join/recovery-blocked"

        [<Literal>]
        val RecoveryWaiting: string = "tool/join/recovery-waiting"

        [<Literal>]
        val UnavailableUntilAuthority: string = "tool/join/unavailable-until-authority"

        [<Literal>]
        val OrchestratorNotReady: string = "tool/join/orchestrator-not-ready"

        [<Literal>]
        val UnavailableFromContext: string = "tool/join/unavailable-from-context"

    val admission: ToolAdmission
    val spec: scope: ToolRuntimeScope -> ToolSpec
