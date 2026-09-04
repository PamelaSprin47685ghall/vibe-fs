namespace Wanxiangshu.OpenCode

/// JS-native boundary for HOST-BOUNDARY-010 / HOST-012 shared cross-instance
/// state. The physical Map/Set/atom singletons in SharedState stay opaque;
/// only narrow put/get/clear/root operations cross the edge.
module SharedStateSurface =

    /// SessionParents: cross-instance parent registry.
    val putSessionParent: childId: string -> parentId: string -> unit

    val getSessionParent: childId: string -> string

    val clearSessionParents: unit -> unit

    /// RootWorkspace: mutable atom set by whichever plugin instance boots first.
    val setRootWorkspace: path: string -> unit

    val clearRootWorkspace: unit -> unit

    val tryGetRootWorkspace: unit -> string
