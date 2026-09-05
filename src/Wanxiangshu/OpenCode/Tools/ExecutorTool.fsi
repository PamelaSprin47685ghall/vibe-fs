namespace Wanxiangshu.OpenCode

/// Bounded command execution. Provider verbs: `run` (DevOps) and `query-shell` (Inspector).
module ExecutorTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module Run =
            [<Literal>]
            val Description: string = "tool/run/description"

            [<Literal>]
            val ArgCommand: string = "tool/run/arg-command"

            [<Literal>]
            val ArgDeadlineSeconds: string = "tool/run/arg-deadline_seconds"

            [<Literal>]
            val ArgOutputBudgetBytes: string = "tool/run/arg-output_budget_bytes"

            [<Literal>]
            val ArgWorldLock: string = "tool/run/arg-world_lock"

            [<Literal>]
            val MissingCommand: string = "tool/run/missing-command"

            [<Literal>]
            val FinitePositive: string = "tool/run/finite-positive"

            [<Literal>]
            val FiniteNonNegativeInteger: string = "tool/run/finite-non-negative-integer"

            [<Literal>]
            val MustBeInteger: string = "tool/run/must-be-integer"

            [<Literal>]
            val Timeout: string = "tool/run/timeout"

            [<Literal>]
            val SpawnFailed: string = "tool/run/spawn-failed"

            [<Literal>]
            val Cancelled: string = "tool/run/cancelled"

            [<Literal>]
            val ExecutionFailed: string = "tool/run/execution-failed"

            [<Literal>]
            val CannotRunFromContext: string = "tool/run/cannot-run-from-context"

            [<Literal>]
            val CannotCondenseUntilAuthority: string = "tool/run/cannot-condense-until-authority"

            [<Literal>]
            val LargeOutputRecoveryBlocked: string = "tool/run/large-output-recovery-blocked"

        [<RequireQualifiedAccess>]
        module QueryShell =
            [<Literal>]
            val Description: string = "tool/query-shell/description"

            [<Literal>]
            val ArgCommand: string = "tool/query-shell/arg-command"

            [<Literal>]
            val MissingCommand: string = "tool/query-shell/missing-command"

    /// Provider-visible execution verb. Distillation is invoked inside this
    /// tool and is never a separate provider verb (PROC-011 / DISTILL-010).
    [<Literal>]
    val RunToolName: string = "run"

    type Request =
        { Command: string
          DeadlineSeconds: float
          OutputBudgetBytes: int64
          WorldLock: bool }

    val runAdmission: ToolAdmission
    val queryShellAdmission: ToolAdmission
    val runSpec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
    val queryShellSpec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec

    val internal formatSpooledOutcome: exitCode: int -> summary: string -> string
