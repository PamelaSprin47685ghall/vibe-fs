namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.Process

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
          FfMerge = fun _ _ -> Task.FromResult(Ok "commit-123456")
          RemoveWorktree = fun _ -> Task.FromResult(Ok()) }

    let private createStubManagerPort () =
        { RunManager = fun _ _ -> Task.FromResult(Ok())
          Reverify = fun _ _ -> Task.FromResult(Ok()) }

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

            let mgr = createStubManagerPort ()
            let orch = Orchestrator(git, mgr, "/repo", "main")

            let! forkResult = orch.ForkManager("m1", "/repo/.worktrees/m1")

            match forkResult with
            | Ok _ -> failwith "Expected forkManager to fail on dirty worktree"
            | Error verdict ->
                match verdict with
                | OrchestratorVerdict.RejectedDirty reason ->
                    trueThat (reason.Contains("dirty")) "Reason should indicate dirty worktree"
                | other -> failwithf "Expected RejectedDirty, got %A" other

            falseThat worktreeCreated "CreateWorktree must not be called when dirty"
        }

    let ``failed review prevents merge and returns NeedsReview`` () =
        task {
            let mutable ffMergeCalled = false
            let mutable removeWorktreeCalled = false

            let git =
                { createStubGitPort () with
                    FfMerge =
                        fun _ _ ->
                            ffMergeCalled <- true
                            Task.FromResult(Ok "should-not-merge")
                    RemoveWorktree =
                        fun _ ->
                            removeWorktreeCalled <- true
                            Task.FromResult(Ok()) }

            let mgr =
                { createStubManagerPort () with
                    Reverify = fun id path -> Task.FromResult(Error "Review failed: lint errors detected") }

            let orch = Orchestrator(git, mgr, "/repo", "main")

            let! forkResult = orch.ForkManager("m1", "/repo/.worktrees/m1")

            match forkResult with
            | Error e -> failwithf "Fork failed unexpectedly: %A" e
            | Ok _ -> ()

            let! joinVerdict = orch.JoinPublished()

            match joinVerdict with
            | OrchestratorVerdict.NeedsReview(id, details) ->
                equal "m1" id
                equal "Review failed: lint errors detected" details
            | other -> failwithf "Expected NeedsReview verdict, got %A" other

            falseThat ffMergeCalled "FF merge must NOT be called when review fails"
            falseThat removeWorktreeCalled "Remove worktree must NOT be called when review fails"
        }

    let ``serialized publish order under SemaphoreSlim`` () =
        task {
            let mutable activePublishCount = 0
            let mutable maxConcurrentPublish = 0
            let rebaseEntered = TaskCompletionSource<unit>()
            let releaseRebase = TaskCompletionSource<unit>()
            let mutable rebaseCalls = 0

            let recordStart () =
                activePublishCount <- activePublishCount + 1
                maxConcurrentPublish <- max maxConcurrentPublish activePublishCount

            let recordEnd () =
                activePublishCount <- activePublishCount - 1

            let git =
                { createStubGitPort () with
                    Rebase =
                        fun _ _ ->
                            task {
                                rebaseCalls <- rebaseCalls + 1
                                recordStart ()

                                if rebaseCalls = 1 then
                                    rebaseEntered.SetResult(())
                                    do! releaseRebase.Task

                                recordEnd ()
                                return Ok()
                            } }

            let mgr = createStubManagerPort ()
            let orch = Orchestrator(git, mgr, "/repo", "main")

            let! res1 = orch.ForkManager("m1", "/repo/.worktrees/m1")
            let! res2 = orch.ForkManager("m2", "/repo/.worktrees/m2")

            match res1, res2 with
            | Ok _, Ok _ -> ()
            | _ -> failwith "Fork managers failed"

            let joinTask1 = orch.JoinPublished()
            let joinTask2 = orch.JoinPublished()
            do! rebaseEntered.Task

            releaseRebase.SetResult(())
            let! verdict1 = joinTask1
            let! verdict2 = joinTask2
            let verdicts = [| verdict1; verdict2 |]

            equal 2 verdicts.Length

            for v in verdicts do
                match v with
                | OrchestratorVerdict.Published _ -> ()
                | other -> failwithf "Expected Published verdict, got %A" other

            equal 1 maxConcurrentPublish
        }

    let ``default worktree path is outside repository`` () =
        task {
            let mutable createdPath = ""

            let git =
                { createStubGitPort () with
                    CreateWorktree =
                        fun _ _ path ->
                            createdPath <- path
                            Task.FromResult(Ok()) }

            let orch = Orchestrator(git, createStubManagerPort (), "/repo", "main")

            let! result = orch.ForkManager("outside-default")

            match result with
            | Error error -> failwithf "Unexpected fork failure: %A" error
            | Ok _ -> ()

            trueThat (createdPath.StartsWith(IO.Path.GetTempPath())) "Default worktree must be outside the repository"
            falseThat (createdPath.Contains("/repo/")) "Default worktree must not be inside the repository"
        }

    let ``ProcessGitPort builds expected git command records`` () =
        task {
            let executedCommands = ResizeArray<Command>()

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
            trueThat isDirty "Expected dirty status"
            equal "git" executedCommands.[0].FileName
            equal [ "status"; "--porcelain" ] executedCommands.[0].Arguments

            let! createRes = gitPort.CreateWorktree "/my/repo" "m1" "/my/repo/.worktrees/m1"
            equal (Ok()) createRes

            let! mergeRes = gitPort.FfMerge "/my/repo/.worktrees/m1" "main"
            equal (Ok "abc1234") mergeRes
        }
