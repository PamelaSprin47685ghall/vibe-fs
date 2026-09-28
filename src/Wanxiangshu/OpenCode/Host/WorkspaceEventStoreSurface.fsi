namespace Wanxiangshu.OpenCode

module WorkspaceEventStoreSurface =
    val acquire: commonDir: string -> obj
    val allHeadsCount: store: obj -> int
    val release: commonDir: string -> unit
