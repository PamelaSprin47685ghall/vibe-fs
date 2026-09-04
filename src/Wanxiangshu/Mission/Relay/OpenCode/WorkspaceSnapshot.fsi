namespace Wanxiangshu.Mission.Relay.OpenCode

open Wanxiangshu.Mission.Relay

module WorkspaceSnapshot =
    val canonical: directory: string -> string
    val capture: directory: string -> WorkspaceSnapshotId

