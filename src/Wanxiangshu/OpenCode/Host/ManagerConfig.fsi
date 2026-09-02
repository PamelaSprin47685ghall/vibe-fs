namespace Wanxiangshu.OpenCode

module ManagerConfig =

    /// Config hook:
    /// - project the required managed catalog onto the live Host config
    /// - apply Wanxiangshu-owned prompt/permission fields
    /// - create missing catalog agents
    /// - never write or overwrite model bindings
    /// - reject legacy unprefixed / build / plan names
    val configureManager: config: obj -> ManagedAgentConfig.ManagedAgentInventory
