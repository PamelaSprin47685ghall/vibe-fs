namespace Wanxiangshu.OpenCode

/// JS-native boundary for HOST-BOUNDARY-010 shared cross-instance state and
/// HOST-BOUNDARY-031 root workspace first-binding.
module SharedStateSurface =

    /// SessionParents: cross-instance parent registry.
    val putSessionParent: childId: string -> parentId: string -> unit

    val getSessionParent: childId: string -> string

    val clearSessionParents: unit -> unit

    val tryBindRootWorkspace: path: string -> bool

    val tryGetRootWorkspace: unit -> string

    val firstBoundRootWorkspace: candidates: string array -> string

    val selectContinuationDirectory: candidate: string -> candidateExists: bool -> rootWorkspace: string -> string
