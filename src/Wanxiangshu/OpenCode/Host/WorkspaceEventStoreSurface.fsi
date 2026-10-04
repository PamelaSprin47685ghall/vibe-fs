namespace Wanxiangshu.OpenCode

module WorkspaceEventStoreSurface =
    val acquire: commonDir: string -> obj
    val activate: store: obj -> unit
    val release: commonDir: string -> unit
    /// durable-events-019 oracle: isolated writer over the production program
    /// minus one named registration; Structural/Journal removal fails closed.
    val createIsolatedWithoutRegistration: commonDir: string -> writerId: string -> ruleName: string -> obj
