namespace Wanxiangshu.OpenCode

open Wanxiangshu.Git

/// Git tree hash for the current workspace.
module GitTree =

    /// HEAD tree object when clean; otherwise HEAD tree + dirty payload.
    val create: directory: string -> GitTreePort
