namespace Wanxiangshu.Next.Orchestrator

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Process

module GitPortHelpers =
    let parseWorktreeList (lines: string list) : (string * string option) list =
        let rec loop acc currentPath currentBranch rest =
            match rest with
            | [] ->
                match currentPath with
                | Some path -> List.rev ((path, currentBranch) :: acc)
                | None -> List.rev acc
            | "" :: tail ->
                let nextAcc =
                    match currentPath with
                    | Some path -> (path, currentBranch) :: acc
                    | None -> acc

                loop nextAcc None None tail
            | line :: tail when line.StartsWith("worktree ") ->
                loop acc (Some(line.Substring("worktree ".Length).Trim())) currentBranch tail
            | line :: tail when line.StartsWith("branch ") ->
                loop acc currentPath (Some(line.Substring("branch ".Length).Trim())) tail
            | _ :: tail -> loop acc currentPath currentBranch tail

        loop [] None None lines

    let private cmd repoPath args =
        { FileName = "git"
          Arguments = args
          WorkingDirectory = Some repoPath
          Environment = None
          Stdin = None
          Deadline = None
          PtyOptions = None }

    /// Verify the target worktree is clean before ff-only merge.
    let checkClean (runner: Command -> Task<int * string * string>) (repoPath: string) : Task<Result<unit, string>> =
        task {
            let! sCode, sStdout, _ = runner (cmd repoPath [ "status"; "--porcelain" ])

            if sCode = 0 && not (String.IsNullOrWhiteSpace sStdout) then
                return Error "target worktree is dirty; refusing ff-only merge"
            else
                return Ok()
        }

    /// Verify HEAD moved to the expected commit after ff-only merge.
    let verifyHead
        (runner: Command -> Task<int * string * string>)
        (repoPath: string)
        (expected: string)
        : Task<Result<string, string>> =
        task {
            let! vCode, vStdout, vStderr = runner (cmd repoPath [ "rev-parse"; "HEAD" ])

            if vCode <> 0 then
                return
                    Error(
                        if String.IsNullOrWhiteSpace vStderr then
                            vStdout
                        else
                            vStderr
                    )
            elif vStdout.Trim() <> expected then
                return
                    Error(
                        sprintf "ff-only merge did not advance HEAD to candidate %s (got %s)" expected (vStdout.Trim())
                    )
            else
                return Ok expected
        }
