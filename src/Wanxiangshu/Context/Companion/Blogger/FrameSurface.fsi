namespace Wanxiangshu.Context.Companion.Blogger

/// Context-compression frame owner. The JavaScript boundary exchanges only
/// JSON-shaped frame/projection snapshots; BlogProjection's list, map, DU and
/// identity representations stay inside this module.
[<RequireQualifiedAccess>]
module BlogFrameSurface =

    /// Construct one frame from plain JSON data.
    val frame: value: obj -> obj

    /// Empty durable frame projection.
    val empty: obj

    /// Apply one atomic BlogObservationCommitted projection line. `request.frame`
    /// carries the frame and the remaining fields are the frozen commit proof.
    val applyEntry: request: obj -> state: obj -> obj

    /// Apply one atomic BlogObservationsSquashed projection line.
    val applySquash: request: obj -> state: obj -> obj

    /// Host compaction containment: retire PrefixCoverage while retaining frames
    /// and RecordCoverage.
    val applyReanchor: state: obj -> obj

    val frameCount: state: obj -> int
    val frameEpochOf: state: obj -> int
    val frames: state: obj -> obj array
    val frameKinds: state: obj -> string array
    val coverableFrameKinds: state: obj -> string array
    val coverage: state: obj -> obj
    val hasCoverage: state: obj -> bool
    val squashWidth: state: obj -> int
