namespace Wanxiangshu.Repository.Programming.Js

module JsMutationFs =
    val resolveToolPath: root: string -> path: string -> string
    val existsPath: path: string -> bool
    val undoIfMatches: root: string -> path: string -> expectedCurrent: string -> restoreTo: string option -> unit
    val commitPlan: root: string -> plan: JsCommitMutation list -> Result<unit, JsFailure>
    val rollbackPlan: root: string -> plan: JsRollbackMutation list -> unit
    /// Delete a file or an empty directory (JS-020/JS-030). Non-empty is
    /// DIRECTORY_NOT_EMPTY; missing target is FILE_NOT_FOUND. As-is: the
    /// effect lands immediately; durable accounting is the workflow's.
    val removePath: path: string -> Result<unit, JsFailure>
    /// Move or rename a file or directory (POSIX mv, overwrite allowed).
    /// EXDEV is CROSS_DEVICE_MOVE with neither side changed. As-is: same
    /// ownership boundary as removePath.
    val movePath: fromPath: string -> toPath: string -> Result<unit, JsFailure>
    /// Enumerate one directory in sorted order; directory entries end with "/".
    val listDirectory: path: string -> Result<string list, JsFailure>
    /// Metadata for one path; a missing path is `exists = false`, not an error.
    val statPath: path: string -> Result<obj, JsFailure>
    /// Create one directory (non-recursive). Existing is DIRECTORY_EXISTS;
    /// a missing parent is PARENT_DIRECTORY_MISSING.
    val makeDirectory: path: string -> Result<unit, JsFailure>
