namespace Wanxiangshu.OpenCode

module WorkspaceEventStoreSurface =
    val acquire: commonDir: string -> obj

    /// Consumes the canonical Journal Current. An empty initialized Current has
    /// available=true and no sessions; an absent Current has available=false.
    val journalCurrent: store: obj -> obj

    val release: commonDir: string -> unit
