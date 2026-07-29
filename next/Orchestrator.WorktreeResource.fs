namespace Wanxiangshu.Next.Orchestrator

open System
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Next.Process

module private WorktreeDisposal =
#if FABLE_COMPILER
    [<Emit("$0")>]
    let asValueTask (operation: Task) : ValueTask = jsNative
#else
    let asValueTask (operation: Task) = ValueTask(operation)
#endif

/// Owned manager worktree. Release is idempotent; DisposeAsync performs the
/// same physical cleanup when a program exits before publish.
type WorktreeResource private (path: string, branch: string, git: GitPort) =
    let mutable released = false

    member _.Path = path
    member _.Branch = branch

    member _.Release() =
        task {
            if released then
                return Ok()
            else
                let! worktree = git.RemoveWorktree path
                let! branchResult = git.DeleteBranch branch

                match worktree, branchResult with
                | Ok(), Ok() ->
                    released <- true
                    return Ok()
                | Error left, Error right -> return Error(sprintf "worktree=%s; branch=%s" left right)
                | Error error, _ -> return Error("worktree=" + error)
                | _, Error error -> return Error("branch=" + error)
        }

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            WorktreeDisposal.asValueTask (
                task {
                    try
                        let! _ = this.Release()
                        ()
                    with _ ->
                        ()
                }
            )

    static member Create(git: GitPort, repoPath: string, managerId: string, path: string) =
        task {
            match! git.CreateWorktree repoPath managerId path with
            | Error error -> return Error error
            | Ok() -> return Ok(WorktreeResource(path, sprintf "manager/%s" managerId, git))
        }

    static member Adopt(git: GitPort, managerId: string, path: string) =
        WorktreeResource(path, sprintf "manager/%s" managerId, git)

/// Process-backed worktree verbs used by GitOperations.
module WorktreeCommands =

    let private command cwd args =
        { FileName = "git"
          Arguments = args
          WorkingDirectory = cwd
          Environment = None
          Stdin = None
          Deadline = None
          PtyOptions = None }

    let private outcome code stdout stderr =
        if code = 0 then Ok()
        else Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)

    let isDirty runner path =
        task {
            let! code, stdout, _ = runner (command (Some path) [ "status"; "--porcelain" ])
            return code = 0 && not (String.IsNullOrWhiteSpace stdout)
        }

    let create runner repo managerId path =
        task {
            let! code, stdout, stderr =
                runner
                    (command
                        (Some repo)
                        [ "worktree"; "add"; path; "-b"; sprintf "manager/%s" managerId ])

            return outcome code stdout stderr
        }

    let remove runner path =
        task {
            let! code, stdout, stderr =
                runner (command None [ "worktree"; "remove"; "--force"; path ])

            return outcome code stdout stderr
        }

    let private parseList lines =
        let rec loop acc currentPath currentBranch rest =
            match rest with
            | [] ->
                match currentPath with
                | Some path -> List.rev ((path, currentBranch) :: acc)
                | None -> List.rev acc
            | "" :: tail ->
                let next = currentPath |> Option.map (fun path -> path, currentBranch) |> Option.toList
                loop (List.append next acc) None None tail
            | line :: tail when line.StartsWith("worktree ") ->
                loop acc (Some(line.Substring(9).Trim())) currentBranch tail
            | line :: tail when line.StartsWith("branch ") ->
                loop acc currentPath (Some(line.Substring(7).Trim())) tail
            | _ :: tail -> loop acc currentPath currentBranch tail

        loop [] None None lines

    let list runner repo () =
        task {
            let! code, stdout, stderr =
                runner (command (Some repo) [ "worktree"; "list"; "--porcelain" ])

            if code <> 0 then
                return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
            else
                return
                    stdout.Split([| '\n'; '\r' |], StringSplitOptions.None)
                    |> Array.toList
                    |> parseList
                    |> Ok
        }

    let listBranches runner repo () =
        task {
            let! code, stdout, stderr = runner (command (Some repo) [ "branch"; "--list"; "manager/*" ])

            if code <> 0 then
                return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
            else
                return
                    stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun value -> value.Trim().TrimStart('*').Trim())
                    |> Array.filter (String.IsNullOrWhiteSpace >> not)
                    |> Array.toList
                    |> Ok
        }

    let deleteBranch runner repo branch =
        task {
            let! code, stdout, stderr = runner (command (Some repo) [ "branch"; "-D"; branch ])
            return outcome code stdout stderr
        }
