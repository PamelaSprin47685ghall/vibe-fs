namespace Wanxiangshu.Mission.Relay.OpenCode

open Wanxiangshu.Mission.Relay

/// Narrow git operations required for workspace snapshot capture.
/// Supplied by composition; WorkspaceSnapshot never calls physical adapters directly.
type WorkspaceSnapshotGitCapability =
    { TryRevParseHeadTree: string -> string option
      DiffHeadBinary: string -> string
      LsFilesUntrackedZ: string -> string
      HashObjectNoFilters: string -> string -> string
      StatusPorcelainV2Z: string -> string
      LsFilesStageZ: string -> string }

module WorkspaceSnapshot =
    val canonical: git: WorkspaceSnapshotGitCapability -> directory: string -> string
    val capture: git: WorkspaceSnapshotGitCapability -> directory: string -> WorkspaceSnapshotId
