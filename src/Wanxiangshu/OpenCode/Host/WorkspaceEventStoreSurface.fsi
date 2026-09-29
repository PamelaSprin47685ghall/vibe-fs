namespace Wanxiangshu.OpenCode

module WorkspaceEventStoreSurface =
    val acquire: commonDir: string -> obj
    val activate: store: obj -> unit
    val release: commonDir: string -> unit
