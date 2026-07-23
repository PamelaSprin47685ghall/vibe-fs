namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.Process

module OrchestratorTests =

    let private createStubGitPort () =
        { IsDirty = fun _ -> Task.FromResult false
          CreateWorktree = fun _ _ _ -> Task.FromResult(Ok())
          Rebase = fun _ _ -> Task.FromResult(Ok())
          FfMerge = fun _ _ -> Task.FromResult(Ok "commit-123456")
          RemoveWorktree = fun _ -> Task.FromResult(Ok()) }

    let private createStubManagerPort () =
        { RunManager = fun _ _ -> Task.FromResult(Ok())
          Reverify = fun _ _ -> Task.FromResult(Ok()) }

    [<Fact>]
    let ``forkManager rejects dirty worktree before fork`` () =
        task {
            let worktreeCreated = ref false

            let git =
                { createStubGitPort () with
                    IsDirty = fun _ -> Task.FromResult true
                    CreateWorktree =
                        fun _ _ _ ->
                            worktreeCreated := true
                            Task.FromResult(Ok()) }

            let mgr = createStubManagerPort ()
            let orch = Orchestrator(git, mgr, "/repo", "main")

            let! forkResult = orch.ForkManager("m1", "/repo/.worktrees/m1")

            match forkResult with
            | Ok _ -> Assert.True(false, "Expected forkManager to fail on dirty worktree")
            | Error verdict ->
                match verdict with
                | OrchestratorVerdict.RejectedDirty reason ->
                    Assert.True(reason.Contains("dirty"), "Reason should indicate dirty worktree")
                | other -> Assert.True(false, sprintf "Expected RejectedDirty, got %A" other)

            Assert.False(!worktreeCreated, "CreateWorktree must not be called when dirty")
        }

    [<Fact>]
    let ``failed review prevents merge and returns NeedsReview`` () =
        task {
            let ffMergeCalled = ref false
            let removeWorktreeCalled = ref false

            let git =
                { createStubGitPort () with
                    FfMerge =
                        fun _ _ ->
                            ffMergeCalled := true
                            Task.FromResult(Ok "should-not-merge")
                    RemoveWorktree =
                        fun _ ->
                            removeWorktreeCalled := true
                            Task.FromResult(Ok()) }

            let mgr =
                { createStubManagerPort () with
                    Reverify = fun id path -> Task.FromResult(Error "Review failed: lint errors detected") }

            let orch = Orchestrator(git, mgr, "/repo", "main")

            let! forkResult = orch.ForkManager("m1", "/repo/.worktrees/m1")

            match forkResult with
            | Error e -> Assert.True(false, sprintf "Fork failed unexpectedly: %A" e)
            | Ok _ -> ()

            let! joinVerdict = orch.JoinPublished("m1")

            match joinVerdict with
            | OrchestratorVerdict.NeedsReview(id, details) ->
                Assert.Equal("m1", id)
                Assert.Equal("Review failed: lint errors detected", details)
            | other -> Assert.True(false, sprintf "Expected NeedsReview verdict, got %A" other)

            Assert.False(!ffMergeCalled, "FF merge must NOT be called when review fails")
            Assert.False(!removeWorktreeCalled, "Remove worktree must NOT be called when review fails")
        }

    [<Fact>]
    let ``serialized publish order under SemaphoreSlim`` () =
        task {
            let activePublishCount = ref 0
            let maxConcurrentPublish = ref 0

            let recordStart () =
                let current = Interlocked.Increment(activePublishCount)
                let mutable prev = !maxConcurrentPublish

                while current > prev do
                    let swapped = Interlocked.CompareExchange(maxConcurrentPublish, current, prev)
                    if swapped = prev then () else prev <- !maxConcurrentPublish

            let recordEnd () =
                Interlocked.Decrement(activePublishCount) |> ignore

            let git =
                { createStubGitPort () with
                    Rebase =
                        fun _ _ ->
                            task {
                                recordStart ()
                                do! Task.Delay(50)
                                recordEnd ()
                                return Ok()
                            } }

            let mgr = createStubManagerPort ()
            let orch = Orchestrator(git, mgr, "/repo", "main")

            let! res1 = orch.ForkManager("m1", "/repo/.worktrees/m1")
            let! res2 = orch.ForkManager("m2", "/repo/.worktrees/m2")

            match res1, res2 with
            | Ok _, Ok _ -> ()
            | _ -> Assert.True(false, "Fork managers failed")

            let joinTask1 = orch.JoinPublished("m1")
            let joinTask2 = orch.JoinPublished("m2")

            let! verdicts = Task.WhenAll([| joinTask1; joinTask2 |])

            Assert.Equal(2, verdicts.Length)

            for v in verdicts do
                match v with
                | OrchestratorVerdict.Published _ -> ()
                | other -> Assert.True(false, sprintf "Expected Published verdict, got %A" other)

            Assert.Equal(1, !maxConcurrentPublish)
        }

    [<Fact>]
    let ``ProcessGitPort builds expected git command records`` () =
        task {
            let executedCommands = System.Collections.Generic.List<Command>()

            let runner (cmd: Command) =
                executedCommands.Add(cmd)

                if cmd.Arguments |> List.contains "status" then
                    Task.FromResult(0, "M file.txt", "")
                elif cmd.Arguments |> List.contains "rev-parse" then
                    Task.FromResult(0, "abc1234", "")
                else
                    Task.FromResult(0, "", "")

            let gitPort = ProcessGitPort.createWithRunner runner

            let! isDirty = gitPort.IsDirty "/my/repo"
            Assert.True(isDirty)
            Assert.Equal("git", executedCommands.[0].FileName)
            Assert.Equal<string list>([ "status"; "--porcelain" ], executedCommands.[0].Arguments)

            let! createRes = gitPort.CreateWorktree "/my/repo" "m1" "/my/repo/.worktrees/m1"
            Assert.Equal(Ok(), createRes)

            let! mergeRes = gitPort.FfMerge "/my/repo/.worktrees/m1" "main"
            Assert.Equal(Ok "abc1234", mergeRes)
        }
