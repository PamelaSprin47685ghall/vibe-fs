namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop

module ManagerConfig =

    /// Config hook:
    /// - project the required managed catalog onto the live Host config
    /// - apply Wanxiangshu-owned prompt/permission fields
    /// - create missing catalog agents
    /// - never write or overwrite model bindings
    /// - reject legacy unprefixed / build / plan names
    let configureManager (config: obj) : ManagedAgentConfig.ManagedAgentInventory =
        match ManagedAgentConfig.configureFromHostConfig config with
        | Ok inventory -> inventory
        | Error err ->
            Diagnostic.fatal "managed-agent-config-invalid" [ "result", err ]
            failwith ("unreachable after Diagnostic.fatal: " + err)
