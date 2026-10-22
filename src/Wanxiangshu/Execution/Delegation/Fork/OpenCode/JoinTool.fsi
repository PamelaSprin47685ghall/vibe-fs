namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Host
open Wanxiangshu.OpenCode

/// join() waits for the owning runtime's next physical completion batch.
/// Orchestrator join routes to ManagerJob verdict mailbox by authority role.
/// Process-local join admission: FamilyReady permit → HostForkJoin.joinAvailableWithPermit (no bare Join, no AST).
/// EXEC-017: tool abort → JoinInterrupt.Signal only (≠ runtime.Cancel).
/// DevOps join: 10s timeout budget (NodeTiming.timerTask 10000). Orch/Manager join remains untimed.
module JoinTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/join/description"

        [<Literal>]
        val RecoveryBlocked: string = "tool/join/recovery-blocked"

        [<Literal>]
        val UnavailableFromContext: string = "tool/join/unavailable-from-context"

    /// The join wire record; the rules that produce it live in `JoinRules`.
    type JoinRecord = Wanxiangshu.Execution.Delegation.Fork.JoinRules.JoinRecord

    val spec: scope: ToolRuntimeScope -> ToolSpec

    val executeReceipt: scope: ToolRuntimeScope -> context: HostToolContext -> Task<Result<JoinRecord list, string>>
