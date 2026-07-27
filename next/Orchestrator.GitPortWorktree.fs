namespace Wanxiangshu.Next.Orchestrator

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Process

module GitPortWorktree =

    /// Check whether the given path has uncommitted changes.
    let isDirty (runner: Command -> Task<int * string * string>) (targetPath: string) : Task<bool> =
        task {
            let cmd =
                { FileName = "git"
                  Arguments = [ "status"; "--porcelain" ]
                  WorkingDirectory = Some targetPath
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! (code, stdout, _) = runner cmd
            return code = 0 && not (String.IsNullOrWhiteSpace stdout)
        }

    /// Create a manager worktree on a fresh `manager/<id>` branch.
    let createWorktree
        (runner: Command -> Task<int * string * string>)
        (targetRepoPath: string)
        (managerId: string)
        (targetPath: string)
        : Task<Result<unit, string>> =
        task {
            let cmd =
                { FileName = "git"
                  Arguments = [ "worktree"; "add"; targetPath; "-b"; sprintf "manager/%s" managerId ]
                  WorkingDirectory = Some targetRepoPath
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! (code, stdout, stderr) = runner cmd

            if code = 0 then
                return Ok()
            else
                return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
        }

    /// Force-remove a worktree.
    let removeWorktree
        (runner: Command -> Task<int * string * string>)
        (worktreePath: string)
        : Task<Result<unit, string>> =
        task {
            let cmd =
                { FileName = "git"
                  Arguments = [ "worktree"; "remove"; "--force"; worktreePath ]
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! (code, stdout, stderr) = runner cmd

            if code = 0 then
                return Ok()
            else
                return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
        }

    /// List all worktrees in the repo (path, optional branch).
    let listWorktrees
        (runner: Command -> Task<int * string * string>)
        (repoPath: string)
        ()
        : Task<Result<(string * string option) list, string>> =
        task {
            let cmd =
                { FileName = "git"
                  Arguments = [ "worktree"; "list"; "--porcelain" ]
                  WorkingDirectory = Some repoPath
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! (code, stdout, stderr) = runner cmd

            if code = 0 then
                let lines =
                    stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.toList

                return Ok(GitPortHelpers.parseWorktreeList lines)
            else
                return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
        }

    /// List manager-owned branches.
    let listManagerBranches
        (runner: Command -> Task<int * string * string>)
        (repoPath: string)
        ()
        : Task<Result<string list, string>> =
        task {
            let cmd =
                { FileName = "git"
                  Arguments = [ "branch"; "--list"; "manager/*" ]
                  WorkingDirectory = Some repoPath
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! (code, stdout, stderr) = runner cmd

            if code = 0 then
                let branches =
                    stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun s -> s.Trim().TrimStart('*').Trim())
                    |> Array.filter (fun s -> not (System.String.IsNullOrWhiteSpace s))
                    |> Array.toList

                return Ok branches
            else
                return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
        }

    /// Delete a branch by name from the repo.
    let deleteBranch
        (runner: Command -> Task<int * string * string>)
        (repoPath: string)
        (branch: string)
        : Task<Result<unit, string>> =
        task {
            let cmd =
                { FileName = "git"
                  Arguments = [ "branch"; "-D"; branch ]
                  WorkingDirectory = Some repoPath
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! (code, stdout, stderr) = runner cmd

            if code = 0 then
                return Ok()
            else
                return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
        }
