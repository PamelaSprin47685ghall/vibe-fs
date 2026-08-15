namespace Wanxiangshu.OpenCode

/// Cross-process tool recovery is deliberately not wired into ordinary plugin
/// lifecycle. Interrupted tools remain visible failures. Future session resume
/// is an explicit /continue workflow, not composition-root startup behavior.
module PluginRecoveryWiring =
    let disabled = true
