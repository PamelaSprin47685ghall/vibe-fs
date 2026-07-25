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
                runtime.Fork(agentId, AgentRole.Coder, runWork = (fun () -> task { return Ok "hello-fork" }))

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
                Assert.Equal(Ok "hello-fork", completion.Outcome)
            | Error err -> Assert.True(false, sprintf "Expected completion, got Error: %A" err)
        }

    [<Fact>]
    let ``ForkRuntime_nudge_on_busy_agent_does_not_replace_pending_run`` () =
        task {
            let firstWork = TaskCompletionSource<Result<string, string>>()
            let completionSource = TaskCompletionSource<RunCompletion>()
            let runtime = ForkRuntime(listener = completionSource.SetResult)
            let agentId = "agent-busy-nudge"

            let firstFork =
                runtime.Fork(agentId, AgentRole.Inspector, runWork = (fun () -> firstWork.Task))

            match firstFork with
            | ForkResult.Created id -> Assert.Equal(agentId, id)
            | other -> Assert.True(false, sprintf "Expected Created result, got: %A" other)

            // Nudge on a busy agent: no new pending run created,
            // existing join waits for original completion only.
            let secondFork =
                runtime.Fork(agentId, AgentRole.Inspector, runWork = (fun () -> task { return Ok "second" }))

            Assert.Equal(ForkResult.Nudged agentId, secondFork)

            // The second fork does not produce a second completion.
            // Completing the first work resolves the single join.
            firstWork.SetResult(Ok "first")
            let! _ = completionSource.Task
            let! joined = runtime.Join()

            match joined with
            | Ok completion ->
                Assert.Equal(agentId, completion.AgentId)
                Assert.Equal(AgentRole.Inspector, completion.Role)
                Assert.Equal(Ok "first", completion.Outcome)
            | Error err -> Assert.True(false, sprintf "Expected completion, got Error: %A" err)

            // Agent is now idle; a subsequent fork creates a new run.
            let thirdFork =
                runtime.Fork(agentId, AgentRole.Inspector, runWork = (fun () -> task { return Ok "third" }))

            match thirdFork with
            | ForkResult.Created id -> Assert.Equal(agentId, id)
            | other -> Assert.True(false, sprintf "Expected Created result for idle agent, got: %A" other)

            let! _ = runtime.Join()
        }

    [<Fact>]
    let ``ForkRuntime_join_waits_for_pending_completion`` () =
        task {
            let pendingWork = TaskCompletionSource<Result<string, string>>()
            let completionSource = TaskCompletionSource<RunCompletion>()
            let runtime = ForkRuntime(listener = completionSource.SetResult)

            match runtime.Fork("agent-pending", AgentRole.Coder, runWork = (fun () -> pendingWork.Task)) with
            | ForkResult.Created _ -> ()
            | other -> Assert.True(false, sprintf "Expected Created result, got: %A" other)

            let joinResult = runtime.Join()
            pendingWork.SetResult(Ok "pending")
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
                runtime.Fork(agentId, AgentRole.Manager, runWork = (fun () -> task { return Ok "ok" }))

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

            match runtime.Fork(agentId, AgentRole.Coder, runWork = (fun () -> task { return Ok "done" })) with
            | ForkResult.Created id -> Assert.Equal(agentId, id)
            | other -> Assert.True(false, sprintf "Expected Created result, got: %A" other)

            let! joined = runtime.Join()
            Assert.True(joined.IsOk, sprintf "Expected completion, got: %A" joined)

            let (agentList, _) = runtime.List()
            let agent = agentList |> List.find (fun record' -> record'.AgentId = agentId)
            Assert.Equal(AgentStatus.Idle, agent.Status)
        }
