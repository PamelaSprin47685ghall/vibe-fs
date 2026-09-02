namespace Wanxiangshu.OpenCode

/// JS-native static contracts for capability-owned tools. Dynamic Host schemas
/// remain exercised through the real plugin; this surface exposes only the
/// owner-defined identity and catalog facts needed by semantic unit laws.
module ToolSurface =

    val toolSpecNames: unit -> string array
    val bashHoneypotContract: unit -> obj
    val chronicleContract: unit -> obj
