namespace Wanxiangshu.OpenCode

/// Managed agent Host-config gate. This owns catalog projection and Wanxiangshu-owned
/// non-model fields. Physical model routing is exclusively owned by ModelRouting.
/// Missing catalog names are created on the live Host config; opencode.json is not
/// the inventory, and Host agent.model is never routing truth.
module ManagedAgentConfig =

    type ManagedAgentBinding = { Agent: ManagedAgent }

    type ManagedAgentInventory =
        { Bindings: Map<string, ManagedAgentBinding> }

    type ConfigGateError =
        | MissingHostConfig
        | LegacyAgentPresent of string
        | InvalidManagedAgent of string * detail: string

    /// Validate the config object against the managed agent catalog.
    val validate: config: obj -> Result<ManagedAgentInventory, string>

    /// Project Wanxiangshu-owned non-model fields onto Host agent entries.
    /// Creates missing catalog names on the live config object, never writes/overwrites model.
    /// AGENT-007's first layer is fail-closed: a validation failure elsewhere in the
    /// config must not silently drop every permission write.
    val applyOwnedFields: config: obj -> inventory: ManagedAgentInventory -> unit

    val configureFromHostConfig: config: obj -> Result<ManagedAgentInventory, string>
