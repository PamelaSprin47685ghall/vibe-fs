namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop

module ManagerConfig =

    /// 0.5.0 config hook:
    /// - validate Host-final agent inventory (20 fast/deep agents)
    /// - apply Wanxiangshu-owned prompt/permission fields
    /// - never create missing agents
    /// - never write or overwrite model bindings
    /// - reject legacy unprefixed / build / plan names
    let configureManager (config: obj) : ManagedAgentConfig.ManagedAgentInventory =
        match ManagedAgentConfig.configureFromHostConfig config with
        | Ok inventory -> inventory
        | Error err ->
            Diagnostic.fatal "managed-agent-config-invalid" [ "result", err ]
            failwith ("unreachable after Diagnostic.fatal: " + err)
