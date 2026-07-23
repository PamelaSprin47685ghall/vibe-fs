namespace Wanxiangshu.Next.Tests

open System
open System.Diagnostics
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.Session

module ManagerCanaryTests =

    let private createStubGitPort () =
        { IsDirty = fun _ -> Task.FromResult false
          CreateWorktree = fun _ _ _ -> Task.FromResult(Ok())
          Rebase = fun _ _ -> Task.FromResult(Ok())
          FfMerge = fun _ _ -> Task.FromResult(Ok "commit-canary-123")
          RemoveWorktree = fun _ -> Task.FromResult(Ok()) }

    let private createStubManagerPort () =
        { RunManager = fun _ _ -> Task.FromResult(Ok())
          Reverify = fun _ _ -> Task.FromResult(Ok()) }

    [<Fact>]
    let ``Exact_manager_role_surface_has_no_file_or_exec_permissions`` () =
        let expectedPermissions =
            set [ ToolPermission.Fork; ToolPermission.Join; ToolPermission.List ]

        let actualPermissions = Roles.permissions Role.Manager
        Assert.Equal<ToolPermission Set>(expectedPermissions, actualPermissions)

        // Allowed
        Assert.True(Roles.isAllowed Role.Manager ToolPermission.Fork)
        Assert.True(Roles.isAllowed Role.Manager ToolPermission.Join)
        Assert.True(Roles.isAllowed Role.Manager ToolPermission.List)

        // Strictly forbidden file / exec / inspection / verdict surface
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Read)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Write)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Edit)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Exec)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Glob)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Grep)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Inspector)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Verdict)

    [<Fact>]
    let ``Coder_one_shot_inspector_role_surface`` () =
        // Coder has Inspector tool permission to request inspection, but not Exec
        Assert.True(Roles.isAllowed Role.Coder ToolPermission.Inspector)
        Assert.False(Roles.isAllowed Role.Coder ToolPermission.Exec)
        Assert.False(Roles.isAllowed Role.Coder ToolPermission.Fork)
        Assert.False(Roles.isAllowed Role.Coder ToolPermission.Join)

        // Inspector role has Exec permission
        Assert.True(Roles.isAllowed Role.Inspector ToolPermission.Exec)
        Assert.False(Roles.isAllowed Role.Inspector ToolPermission.Read)
        Assert.False(Roles.isAllowed Role.Inspector ToolPermission.Write)
        Assert.False(Roles.isAllowed Role.Inspector ToolPermission.Edit)

    [<Fact>]
    let ``Manager_nonblocking_fork_and_any_child_join`` () =
        task {
            let tcs1 = TaskCompletionSource<Result<unit, string>>()
            let tcs2 = TaskCompletionSource<Result<unit, string>>()

            let managerPort =
                { RunManager =
                    fun mgrId _ ->
                        if mgrId = "mgr-inspector" then tcs1.Task
                        elif mgrId = "mgr-coder" then tcs2.Task
                        else Task.FromResult(Ok())
                  Reverify = fun _ _ -> Task.FromResult(Ok()) }

            let gitPort = createStubGitPort ()
            let orch = Orchestrator(gitPort, managerPort, "/repo", "main")

            // Fork two managers nonblocking
            let sw = Stopwatch.StartNew()
            let! fork1 = orch.ForkManager("mgr-inspector", "/repo/.worktrees/mgr-inspector")
            let! fork2 = orch.ForkManager("mgr-coder", "/repo/.worktrees/mgr-coder")
            sw.Stop()

            Assert.True(
                sw.ElapsedMilliseconds < 200L,
                sprintf "Forking managers should be nonblocking, took %d ms" sw.ElapsedMilliseconds
            )

            match fork1, fork2 with
            | Ok h1, Ok h2 ->
                Assert.Equal("mgr-inspector", h1.ManagerId)
                Assert.Equal("mgr-coder", h2.ManagerId)
            | _ -> Assert.True(false, "ForkManager returned error unexpectedly")

            // Join when no child completed returns Empty
            let! initialJoin = orch.JoinPublished()

            match initialJoin with
            | OrchestratorVerdict.Empty -> ()
            | other -> Assert.True(false, sprintf "Expected Empty verdict, got %A" other)

            // Complete mgr-coder first (any-child join order)
            tcs2.SetResult(Ok())
            do! Task.Delay(20)

            let! join1 = orch.JoinPublished()

            match join1 with
            | OrchestratorVerdict.Published(mgrId, hash) ->
                Assert.Equal("mgr-coder", mgrId)
                Assert.Equal("commit-canary-123", hash)
            | other -> Assert.True(false, sprintf "Expected Published for mgr-coder, got %A" other)

            // Complete mgr-inspector second
            tcs1.SetResult(Ok())
            do! Task.Delay(20)

            let! join2 = orch.JoinPublished()

            match join2 with
            | OrchestratorVerdict.Published(mgrId, hash) ->
                Assert.Equal("mgr-inspector", mgrId)
                Assert.Equal("commit-canary-123", hash)
            | other -> Assert.True(false, sprintf "Expected Published for mgr-inspector, got %A" other)

            // Further join returns Empty
            let! finalJoin = orch.JoinPublished()

            match finalJoin with
            | OrchestratorVerdict.Empty -> ()
            | other -> Assert.True(false, sprintf "Expected Empty, got %A" other)
        }

    [<Fact>]
    let ``Companion_busy_skip_preserves_baseline_for_next_delta`` () =
        task {
            let companion = Companion()
            let inFlightTcs = TaskCompletionSource<string>()
            let p1 = "{\"step\":1}"
            let p2 = "{\"step\":2,\"data\":\"intermediate\"}"
            let p3 = "{\"step\":3,\"data\":\"final\"}"

            // Submit 1 when idle
            let res1 = companion.Submit(p1, (fun (_: string) -> inFlightTcs.Task))
            Assert.Equal(Submitted, res1)
            Assert.True(companion.IsBusy)

            // Submit 2 while busy -> returns SkippedBusy
            let res2 =
                companion.Submit(p2, (fun (_: string) -> Task.FromResult "ShouldNotBeCalled"))

            Assert.Equal(SkippedBusy, res2)

            // Complete in-flight job 1
            inFlightTcs.SetResult("Summary 1")
            do! companion.WaitInFlightAsync()

            Assert.False(companion.IsBusy)
            // Baseline after job 1 should be p1 (since p2 was skipped)
            Assert.Equal(Some p1, companion.Memory.LastSuccessfulProjection)

            // Submit 3 when idle again -> delta compares baseline p1 to p3
            let mutable computedDelta = ""

            let res3 =
                companion.Submit(
                    p3,
                    fun delta ->
                        computedDelta <- delta
                        Task.FromResult "Summary 3"
                )

            Assert.Equal(Submitted, res3)
            do! companion.WaitInFlightAsync()

            Assert.Contains("\"step\":3", computedDelta)
            Assert.Contains("\"data\":\"final\"", computedDelta)
            Assert.Equal(Some p3, companion.Memory.LastSuccessfulProjection)
            Assert.Equal(Some "Summary 3", companion.Memory.CurrentB)
        }

    [<Fact>]
    let ``Reviewer_double_perfect_guard_and_manager_reverify_integration`` () =
        task {
            // Part A: ReviewGuard unit logic
            let g0 = ReviewGuard.empty
            Assert.Equal(ReviewFinishResult.NeedsReview, ReviewGuard.tryFinish g0)

            let g1 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "tree-hash-aaa" g0
            Assert.Equal(ReviewFinishResult.NeedsReview, ReviewGuard.tryFinish g1)

            // Second consecutive perfect on same tree hash confirms finish
            let g2 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "tree-hash-aaa" g1
            Assert.Equal(ReviewFinishResult.Confirmed, ReviewGuard.tryFinish g2)

            // Hash change invalidates confirmed state
            let g3 = ReviewGuard.recordVerdict ReviewVerdict.Perfect "tree-hash-bbb" g2
            Assert.Equal(ReviewFinishResult.NeedsReview, ReviewGuard.tryFinish g3)

            // Part B: Orchestrator integration returning NeedsReview when reverify fails guard
            let mgrPort =
                { createStubManagerPort () with
                    Reverify = fun mgrId _ -> Task.FromResult(Error(sprintf "Review required for %s" mgrId)) }

            let orch = Orchestrator(createStubGitPort (), mgrPort, "/repo", "main")
            let! forkRes = orch.ForkManager("mgr-review-test", "/repo/.worktrees/mgr-review-test")
            Assert.True(forkRes.IsOk)

            do! Task.Delay(20)

            let! joinRes = orch.JoinPublished()

            match joinRes with
            | OrchestratorVerdict.NeedsReview(mgrId, details) ->
                Assert.Equal("mgr-review-test", mgrId)
                Assert.Contains("Review required", details)
            | other -> Assert.True(false, sprintf "Expected NeedsReview verdict, got %A" other)
        }
