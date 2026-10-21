namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

open Wanxiangshu.Participant.ManagedAgents

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

    val configureFromHostConfig: config: obj -> Result<ManagedAgentInventory, string>
