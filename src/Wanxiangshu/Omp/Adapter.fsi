namespace Wanxiangshu.Omp

/// Host-neutral adapter over the omp (oh-my-pi) extension API.
///
/// The adapter translates the omp extension surface into
/// `Wanxiangshu.Host.Ports.HostPorts` and registers the product hooks the
/// plugin hands back with the omp event bus. It takes no product decision:
/// every verdict comes from the product hooks, and every request omp cannot
/// honour is answered with a typed `HostError`.
module Adapter =

    /// Activate a product plugin inside an omp extension runtime.
    ///
    /// `pi` is the omp `ExtensionAPI` object an extension factory receives.
    /// The plugin is activated on the first `session_start` event, which is the
    /// first point at which omp exposes a session context: the ports that need
    /// one answer `HostError.Unsupported` until then.
    ///
    /// `agents` is the product's catalogue of managed agent roles, exposed to
    /// the product through the `IAgentCatalog` port.
    val activateOnOmp:
        pi: obj
        -> plugin: Wanxiangshu.Host.Ports.IHostPlugin
        -> agents: Wanxiangshu.Host.Ports.AgentRoleSpec list
        -> System.Threading.Tasks.Task<unit>
