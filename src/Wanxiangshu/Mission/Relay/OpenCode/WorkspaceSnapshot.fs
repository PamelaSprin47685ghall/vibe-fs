namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open Wanxiangshu.Git
open Wanxiangshu.Host
open Wanxiangshu.Mission.Relay

module WorkspaceSnapshot =
    let private nulEntries (text: string) =
        text.Split([| '\u0000' |], StringSplitOptions.RemoveEmptyEntries)

    let private headTree directory =
        try
            GitSubject.revParseHeadTree directory
        with _ ->
            "NO_HEAD_TREE"

    let private untrackedEntries directory =
        GitSubject.lsFilesUntrackedZ directory
        |> nulEntries
        |> Array.sort
        |> Array.map (fun path -> path + "\u001f" + GitSubject.hashObjectNoFilters directory path)
        |> String.concat "\u001e"

    /// Exact worktree/index state used by Relay certificate binding.
    ///
    /// - HEAD tree prevents a clean tree from collapsing to an empty digest.
    /// - binary HEAD diff covers staged + unstaged tracked content.
    /// - porcelain-v2 covers dirty/untracked/conflict classification.
    /// - ls-files --stage records index stage 0/1/2/3 identities.
    /// - untracked files are represented by exact git blob hashes, not decoded text.
    let canonical directory =
        String.concat
            "\u001d"
            [ "head=" + headTree directory
              "status=" + GitSubject.statusPorcelainV2Z directory
              "index=" + GitSubject.lsFilesStageZ directory
              "diff=" + GitSubject.diffHeadBinary directory
              "untracked=" + untrackedEntries directory ]

    let capture directory =
        canonical directory |> HostDigest.sha256Hex |> WorkspaceSnapshotId.create

