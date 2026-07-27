namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module OrchestratorIntegrationTests =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private trueThat condition message =
        if not condition then
            failwith message

    let private falseThat condition message =
        if condition then
            failwith message

    let private createStubGitPort () =
        { IsDirty = fun _ -> Task.FromResult false
          CreateWorktree = fun _ _ _ -> Task.FromResult(Ok())
          Rebase = fun _ _ -> Task.FromResult(Ok())
          FfMerge = fun _ _ _ -> Task.FromResult(Ok "commit-123456")
          ConflictedFiles = fun _ -> Task.FromResult(Ok [])
          RemoveWorktree = fun _ -> Task.FromResult(Ok())
          HasRebaseHead = fun _ -> Task.FromResult false
          ListWorktrees = fun () -> Task.FromResult(Ok [])
          ListManagerBranches = fun () -> Task.FromResult(Ok [])
          DeleteBranch = fun _ -> Task.FromResult(Ok()) }

    let private createStubManagerPort () =
        { RunManager = fun _ _ _ -> Task.FromResult(Ok())
          Reverify = fun _ _ _ -> Task.FromResult(Ok()) }

    let ``serialized publish order under SemaphoreSlim`` () =
        task {
            let mutable active = 0
            let mutable maxActive = 0
            let entered = TaskCompletionSource<unit>()
            let release = TaskCompletionSource<unit>()
            let mutable calls = 0

            let git =
                { createStubGitPort () with
                    Rebase =
                        fun _ _ ->
                            task {
                                calls <- calls + 1
                                active <- active + 1
                                maxActive <- max maxActive active

                                if calls = 1 then
                                    entered.SetResult(()) |> ignore

                                do! release.Task
                                active <- active - 1
                                return Ok()
                            } }

            let orch = Orchestrator(git, createStubManagerPort (), "/repo", "main")
            let! r1 = orch.ForkManager("m1", "task", worktreePath = "/repo/.worktrees/m1")
            let! r2 = orch.ForkManager("m2", "task", worktreePath = "/repo/.worktrees/m2")

            match r1, r2 with
            | Ok _, Ok _ -> ()
            | _ -> failwith "Fork managers failed"

            let j1 = orch.JoinPublished()
            let j2 = orch.JoinPublished()
            do! entered.Task
            release.SetResult(())
            let! v1 = j1
            let! v2 = j2

            for v in [ v1; v2 ] do
                match v with
                | OrchestratorVerdict.Published _ -> ()
                | other -> failwithf "Expected Published, got %A" other

            equal 1 maxActive
        }

    let ``default worktree path is outside repository`` () =
        task {
            let mutable path = ""

            let git =
                { createStubGitPort () with
                    CreateWorktree =
                        fun _ _ p ->
                            path <- p
                            Task.FromResult(Ok()) }

            let orch = Orchestrator(git, createStubManagerPort (), "/repo", "main")
            let! result = orch.ForkManager("outside-default", "task")

            match result with
            | Error e -> failwithf "Unexpected fork failure: %A" e
            | Ok _ -> ()

            trueThat (path.StartsWith(IO.Path.GetTempPath())) "worktree must be outside repo"
            falseThat (path.Contains "/repo/") "worktree must not be inside repo"
        }

    let ``ProcessGitPort builds expected git command records`` () =
        task {
            let commands = ResizeArray<Command>()

            let runner cmd =
                commands.Add cmd

                if cmd.Arguments |> List.contains "status" then
                    // IsDirty runs status on the target path (returns dirty);
                    // FfMerge's clean-check runs status on repoPath "." (must be clean).
                    if cmd.WorkingDirectory = Some "." then
                        Task.FromResult(0, "", "")
                    else
                        Task.FromResult(0, "M file.txt", "")
                elif cmd.Arguments |> List.contains "symbolic-ref" then
                    Task.FromResult(0, "main", "")
                elif cmd.Arguments |> List.contains "rev-parse" then
                    Task.FromResult(0, "abc1234", "")
                elif cmd.Arguments |> List.contains "merge-base" then
                    Task.FromResult(0, "", "")
                elif cmd.Arguments |> List.contains "merge" then
                    Task.FromResult(0, "", "")
                else
                    Task.FromResult(0, "", "")

            let git = ProcessGitPort.createWithRunner runner
            let! dirty = git.IsDirty "/my/repo"
            trueThat dirty "Expected dirty status"
            equal [ "status"; "--porcelain" ] commands.[0].Arguments
            let! create = git.CreateWorktree "/my/repo" "m1" "/my/repo/.worktrees/m1"
            equal (Ok()) create
            let! merge = git.FfMerge "/my/repo/.worktrees/m1" "main" None
            equal (Ok "abc1234") merge
        }

    let ``GetTargetHead fails closed when branch not found`` () =
        task {
            let authority = OrchestratorAuthority.createPort ()
            // A nonexistent repo path makes `git rev-parse refs/heads/<branch>`
            // exit non-zero; the port must fail closed (no HEAD fallback).
            let! result = authority.GetTargetHead "/nonexistent/wanxiangshu-auth-xyz" "nonexistent-branch-xyz"

            match result with
            | Ok head -> failwithf "GetTargetHead must fail closed for missing branch, but returned Ok %s" head
            | Error err -> trueThat (err.Length > 0) "error message must be non-empty"
        }

    let ``publish lock serializes two contending runtimes`` () =
        task {
            // Same repo+branch → same lock path. Runtime A holds the lock at a
            // barrier; Runtime B must WAIT (not fail, not enter the critical
            // section); after A releases, B must proceed.
            let lockPath = PublishLock.lockPath "/tmp/wanxiangshu-contention-repo" "main"

            let! releaseA = PublishLock.acquire lockPath

            let mutable bAcquired = false

            let bTask =
                task {
                    let! releaseB = PublishLock.acquire lockPath
                    bAcquired <- true
                    return releaseB
                }

            do! PtyTiming.timerTask 600
            trueThat (not bAcquired) "contender B must not acquire the lock while A holds it"

            do! PublishLock.release releaseA
            let! releaseB = bTask
            do! PublishLock.release releaseB
            trueThat bAcquired "contender B must acquire the lock after A releases"
        }
