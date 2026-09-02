namespace Wanxiangshu.Execution.Delegation.Handle.OpenCode

open System.Threading.Tasks
open Wanxiangshu.OpenCode

/// Complete lifecycle for synchronous one-shot Coder/Inspector tools: create,
/// subscribe-before-send, await one terminal, then physically abort/dispose.
module OneShotAgentTool =

    type Request = { Agent: string; Prompt: string }

    /// DSL-state-combination: domain — optional parent background and WorkRecord
    /// preserve evidence attached to one completed tool outcome; they do not
    /// represent independent runtime stages.
    type Outcome =
        {
            ChildId: string
            Managed: ManagedAgent
            ParentBackgroundDigest: string option
            Output: string
            /// EXEC-028: child LWR (includeOpening=false) on Completed; None otherwise.
            WorkRecord: string option
        }

    /// Same management bound as Distillation / HostForkRuntime join budget.
    /// Unbounded `completion.Task` hung callers when the child never went terminal.
    [<Literal>]
    val CompletionTimeoutMs: int = 600_000

    val run:
        scope: ToolRuntimeScope ->
        context: HostToolContext ->
        request: Request ->
        expectedNames: string list ->
        roleLabel: string ->
            Task<Result<Outcome, string>>
