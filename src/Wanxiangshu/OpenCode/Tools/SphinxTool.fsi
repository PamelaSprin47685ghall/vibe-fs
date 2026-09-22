namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Delegation.SyncDelegate

module SphinxTool =
    val admission: ToolAdmission

    val createExecution:
        sessions: ISessionHostPort ->
        directory: string option ->
        delegates: SyncDelegateRuntime option ->
        logicalOwnerFor: (Wanxiangshu.Foundation.Identity.SessionId -> Wanxiangshu.Foundation.Identity.SessionId) ->
            SphinxExecution option

    val spec: factory: HostToolFactory -> execution: SphinxExecution option -> ToolSpec
