namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module OrchestratorTests =

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
          DeleteBranch = fun _ -> Task.FromResult(Ok())
          ReadHead = fun _ -> Task.FromResult(Ok "commit-123456")
          GetTargetHead = fun _ -> Task.FromResult(Ok "commit-123456") }

    let private createStubManagerPort () =
        { RunManager = fun _ _ _ -> Task.FromResult(Ok())
          Reverify = fun _ _ _ -> Task.FromResult(Ok()) }

    let ``forkManager rejects dirty worktree before fork`` () =
        task {
            let mutable worktreeCreated = false

            let git =
                { createStubGitPort () with
                    IsDirty = fun _ -> Task.FromResult true
                    CreateWorktree =
                        fun _ _ _ ->
                            worktreeCreated <- true
                            Task.FromResult(Ok()) }

            let orch = Orchestrator(git, createStubManagerPort (), "/repo", "main")
            let! result = orch.ForkManager("m1", "task", worktreePath = "/repo/.worktrees/m1")

            match result with
            | Error(OrchestratorVerdict.RejectedDirty reason) ->
                trueThat (reason.Contains "dirty") "dirty reason missing"
            | other -> failwithf "Expected RejectedDirty, got %A" other

            falseThat worktreeCreated "CreateWorktree must not run for dirty tree"
        }

    let ``conflict returns same manager for continuation`` () =
        task {
            let mutable managerCalls = 0
            let mutable rebaseCalls = 0

            let git =
                { createStubGitPort () with
                    Rebase =
                        fun _ _ ->
                            rebaseCalls <- rebaseCalls + 1

                            if rebaseCalls = 1 then
                                Task.FromResult(Error "conflict")
                            else
                                Task.FromResult(Ok()) }

            let mgr =
                { createStubManagerPort () with
                    RunManager =
                        fun _ _ _ ->
                            managerCalls <- managerCalls + 1
                            Task.FromResult(Ok()) }

            let orch = Orchestrator(git, mgr, "/repo", "main")
            let! fork = orch.ForkManager("m1", "task", worktreePath = "/repo/.worktrees/m1")

            match fork with
            | Error e -> failwithf "Fork failed: %A" e
            | Ok _ -> ()

            let! verdict = orch.JoinPublished()

            match verdict with
            | OrchestratorVerdict.Published _ -> ()
            | other -> failwithf "Expected Published, got %A" other

            equal 2 managerCalls
        }

    let ``rebase after conflict requires double perfect`` () =
        task {
            let mutable reverifyCalls = 0
            let mutable rebaseCalls = 0

            let git =
                { createStubGitPort () with
                    Rebase =
                        fun _ _ ->
                            rebaseCalls <- rebaseCalls + 1

                            if rebaseCalls = 1 then
                                Task.FromResult(Error "conflict")
                            else
                                Task.FromResult(Ok()) }

            let mgr =
                { createStubManagerPort () with
                    Reverify =
                        fun _ _ _ ->
                            reverifyCalls <- reverifyCalls + 1
                            Task.FromResult(Ok()) }

            let orch = Orchestrator(git, mgr, "/repo", "main")
            let! fork = orch.ForkManager("m1", "task", worktreePath = "/repo/.worktrees/m1")

            match fork with
            | Error e -> failwithf "Fork failed: %A" e
            | Ok _ -> ()

            let! verdict = orch.JoinPublished()

            match verdict with
            | OrchestratorVerdict.Published _ -> ()
            | other -> failwithf "Expected Published, got %A" other
            // reverifyTwice calls manager.Reverify once per phase (pre-rebase +
            // post-rebase); each call performs the double-PERFECT check internally.
            equal 2 reverifyCalls
        }

    let ``failed review prevents merge and returns NeedsReview`` () =
        task {
            let mutable ffCalled = false
            let mutable removeCalled = false

            let git =
                { createStubGitPort () with
                    FfMerge =
                        fun _ _ _ ->
                            ffCalled <- true
                            Task.FromResult(Ok "unexpected")
                    RemoveWorktree =
                        fun _ ->
                            removeCalled <- true
                            Task.FromResult(Ok()) }

            let mgr =
                { createStubManagerPort () with
                    Reverify = fun _ _ _ -> Task.FromResult(Error "Review failed: lint errors detected") }

            let orch = Orchestrator(git, mgr, "/repo", "main")
            let! fork = orch.ForkManager("m1", "task", worktreePath = "/repo/.worktrees/m1")

            match fork with
            | Error e -> failwithf "Fork failed: %A" e
            | Ok _ -> ()

            let! verdict = orch.JoinPublished()

            match verdict with
            | OrchestratorVerdict.NeedsReview(id, details) ->
                equal "m1" id
                equal "Review failed: lint errors detected" details
            | other -> failwithf "Expected NeedsReview, got %A" other

            falseThat ffCalled "FF merge must not run after review failure"
            trueThat removeCalled "worktree resource must release after review failure"
        }
