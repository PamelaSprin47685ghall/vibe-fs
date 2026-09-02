namespace Wanxiangshu.OpenCode

/// JS-native Host-config boundary for the managed-agent capability projection.
///
/// ManagedAgentConfig owns validation and writes; this surface only translates
/// its Result and catalog to plain data. Model fields remain in the caller's
/// config object and are never copied into the inventory.
module ManagedAgentConfigSurface =

    /// Host plugin boot normally performs this installation. The explicit
    /// boundary is also useful to a pure config-contract consumer that does
    /// not construct a plugin instance.
    val installDefaultResources: unit -> unit

    /// Validate without crossing the F# Result or Map representation.
    val validate: config: obj -> obj

    /// Validate, then project all Wanxiangshu-owned fields onto the live config.
    val configure: config: obj -> obj

    /// Apply the same owned projection as the manager hook. Invalid legacy
    /// names remain a fatal boundary for this JSON consumer.
    val configureManager: config: obj -> obj
