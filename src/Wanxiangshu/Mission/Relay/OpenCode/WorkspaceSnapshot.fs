namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open Wanxiangshu.Host
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
    let private nulEntries (text: string) =
        text.Split([| '\u0000' |], StringSplitOptions.RemoveEmptyEntries)

    let private headState (git: WorkspaceSnapshotGitCapability) directory =
        match git.TryRevParseHeadTree directory with
        | Some headTree -> headTree, git.DiffHeadBinary directory
        | None -> "NO_HEAD_TREE", "NO_HEAD_DIFF"

    let private untrackedEntries (git: WorkspaceSnapshotGitCapability) directory =
        git.LsFilesUntrackedZ directory
        |> nulEntries
        |> Array.sort
        |> Array.map (fun path -> path + "\u001f" + git.HashObjectNoFilters directory path)
        |> String.concat "\u001e"

    /// Exact worktree/index state used by Relay certificate binding.
    ///
    /// - HEAD tree prevents a clean tree from collapsing to an empty digest.
    /// - binary HEAD diff covers staged + unstaged tracked content.
    /// - porcelain-v2 covers dirty/untracked/conflict classification.
    /// - ls-files --stage records index stage 0/1/2/3 identities.
    /// - untracked files are represented by exact git blob hashes, not decoded text.
    let canonical (git: WorkspaceSnapshotGitCapability) directory =
        let headTree, headDiff = headState git directory

        String.concat
            "\u001d"
            [ "head=" + headTree
              "status=" + git.StatusPorcelainV2Z directory
              "index=" + git.LsFilesStageZ directory
              "diff=" + headDiff
              "untracked=" + untrackedEntries git directory ]

    let capture (git: WorkspaceSnapshotGitCapability) directory =
        canonical git directory |> HostDigest.sha256Hex |> WorkspaceSnapshotId.create
