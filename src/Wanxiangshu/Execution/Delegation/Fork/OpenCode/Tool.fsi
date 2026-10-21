namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork

/// OpenCode adapter for the fork / resume / commission commands
/// (`ForkCommand`): wire decode, the host port binding and registration.
module ForkTool =

    val managerSpec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
    val resumeSpec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
    val orchestratorSpec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec

    val executeReceipt:
        scope: ToolRuntimeScope ->
        context: HostToolContext ->
        args: HostToolArguments ->
            Task<Result<ForkReceipt, string>>

    val executeResumeReceipt:
        scope: ToolRuntimeScope ->
        context: HostToolContext ->
        args: HostToolArguments ->
            Task<Result<ForkReceipt, string>>

    /// The placement receipt a fork or a resume hands back. It is aliased here,
    /// in the adapter namespace, under a name of its own, so that a caller
    /// outside the delegation shard can name the receipt without reaching into
    /// the product namespace.
    type PlacementReceipt = ForkReceipt
