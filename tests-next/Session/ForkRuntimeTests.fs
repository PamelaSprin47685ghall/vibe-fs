namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Session

module ForkRuntimeTests =

    [<Fact>]
    let ``ForkRuntime_fork_and_join_returns_ok_completion`` () =
        task {
            let agentId = "agent-alpha"
            let completionSource = TaskCompletionSource<RunCompletion>()
            let runtime = ForkRuntime(listener = completionSource.SetResult)

            let forkRes =
                runtime.Fork(agentId, AgentRole.Coder, runWork = (fun () -> task { return AgentCompletion.ofSimpleText agentId "run-x" AgentRole.Coder "hello-fork" }))

            match forkRes with
            | ForkResult.Created id -> Assert.Equal(agentId, id)
            | other -> Assert.True(false, sprintf "Expected Created result, got: %A" other)

            let! observed = completionSource.Task

            let! joined = runtime.Join()

            match joined with
            | Ok completion ->
                Assert.Equal(observed.RunId, completion.RunId)
                Assert.Equal(agentId, completion.AgentId)
                Assert.Equal(AgentRole.Coder, completion.Role)
                Assert.Equal("hello-fork", AgentCompletion.text completion.Outcome)
            | Error err -> Assert.True(false, sprintf "Expected completion, got Error: %A" err)
        }

    [<Fact>]
    let ``ForkRuntime_nudge_on_busy_agent_does_not_create_new_pending_run`` () =
        task {
            let agentId = "agent-busy-nudge"
            let pendingWork = TaskCompletionSource<AgentCompletionOutcome>()
            let runtime = ForkRuntime()

            // First fork creates a pending run.
            let firstFork =
                runtime.Fork(agentId, AgentRole.Inspector, runWork = (fun () -> pendingWork.Task))

            match firstFork with
            | ForkResult.Created id -> Assert.Equal(agentId, id)
            | other -> Assert.True(false, sprintf "Expected Created, got %A" other)

            Assert.Equal(1, runtime.ActiveRunCount)

            let secondFork =
                runtime.Fork(agentId, AgentRole.Inspector, runWork = (fun () -> task { return AgentCompletion.ofSimpleText agentId "run-y" AgentRole.Inspector "second" }))

            Assert.Equal(ForkResult.Nudged agentId, secondFork)
            Assert.Equal(1, runtime.ActiveRunCount)

            pendingWork.SetResult(AgentCompletion.ofSimpleText agentId "run-x" AgentRole.Inspector "first")
            let! result = runtime.Join()

            match result with
            | Ok completion -> Assert.Equal("first", AgentCompletion.text completion.Outcome)
            | Error err -> Assert.True(false, sprintf "Expected completion, got Error: %A" err)

            Assert.Equal(0, runtime.PendingCompletionCount)
        }

    [<Fact>]
    let ``ForkRuntime_join_ignores_completion_not_owned_by_runtime`` () =
        task {
            let runtime = ForkRuntime()

            let bloggerCompletion : RunCompletion =
                { RunId = "run-system-blogger"
                  AgentId = "system-blogger"
                  Role = AgentRole.Blogger
                  Outcome = AgentCompletion.ofSimpleText "system-blogger" "run-system-blogger" AgentRole.Blogger "blog"
                  CompletedAt = DateTimeOffset.UtcNow }

            runtime.PublishCompletion bloggerCompletion
            Assert.Equal(0, runtime.PendingCompletionCount)

            let! joined = runtime.Join()

            match joined with
            | Error ForkError.NothingToJoin -> ()
            | other -> Assert.True(false, sprintf "foreign completion resolved join: %A" other)
        }

    [<Fact>]
    let ``ForkRuntime_join_waits_for_pending_completion`` () =
        task {
            let pendingWork = TaskCompletionSource<AgentCompletionOutcome>()
            let completionSource = TaskCompletionSource<RunCompletion>()
            let runtime = ForkRuntime(listener = completionSource.SetResult)

            match runtime.Fork("agent-pending", AgentRole.Coder, runWork = (fun () -> pendingWork.Task)) with
            | ForkResult.Created _ -> ()
            | other -> Assert.True(false, sprintf "Expected Created result, got: %A" other)

            let joinResult = runtime.Join()
            pendingWork.SetResult(AgentCompletion.ofSimpleText "agent-pending" "run-p" AgentRole.Coder "pending")
            let! _ = completionSource.Task
            let! joined = joinResult
            Assert.True(joined.IsOk, sprintf "Join did not return completion: %A" joined)
        }

    [<Fact>]
    let ``ForkRuntime_list_returns_agents_and_ptys`` () =
        task {
            let agentId = "agent-list-test"
            let completionSource = TaskCompletionSource<RunCompletion>()
            let runtime = ForkRuntime(listener = completionSource.SetResult)

            let pty: PtyRecord =
                { PtyId = "pty-1"
                  AgentId = agentId
                  Command = "bash"
                  StartedAt = DateTimeOffset.UtcNow }

            runtime.RegisterPty(pty)

            let forkRes =
                runtime.Fork(agentId, AgentRole.Manager, runWork = (fun () -> task { return AgentCompletion.ofSimpleText agentId "run-m" AgentRole.Manager "ok" }))

            match forkRes with
            | ForkResult.Created id -> Assert.Equal(agentId, id)
            | other -> Assert.True(false, sprintf "Expected Created result, got: %A" other)

            let! _ = completionSource.Task

            let (agentList, ptyList) = runtime.List()
            Assert.Equal(1, agentList.Length)
            Assert.Equal(agentId, agentList.[0].AgentId)
            Assert.Equal(AgentRole.Manager, agentList.[0].Role)

            Assert.Equal(1, ptyList.Length)
            Assert.Equal("pty-1", ptyList.[0].PtyId)
        }

    [<Fact>]
    let ``ForkRuntime_fast_completion_leaves_agent_idle`` () =
        task {
            let agentId = "agent-fast-completion"
            let runtime = ForkRuntime()

            match runtime.Fork(agentId, AgentRole.Coder, runWork = (fun () -> task { return AgentCompletion.ofSimpleText agentId "run-d" AgentRole.Coder "done" })) with
            | ForkResult.Created id -> Assert.Equal(agentId, id)
            | other -> Assert.True(false, sprintf "Expected Created result, got: %A" other)

            let! joined = runtime.Join()
            Assert.True(joined.IsOk, sprintf "Expected completion, got: %A" joined)

            let (agentList, _) = runtime.List()
            let agent = agentList |> List.find (fun record' -> record'.AgentId = agentId)
            Assert.Equal(AgentStatus.Idle, agent.Status)
        }
