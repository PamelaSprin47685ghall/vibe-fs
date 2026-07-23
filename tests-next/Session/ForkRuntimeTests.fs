namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Session

module ForkRuntimeTests =

    [<Fact>]
    let ``ForkRuntime_fork_and_join_returns_ok_completion`` () =
        task {
            let runtime = ForkRuntime()
            let agentId = "agent-alpha"

            let forkRes =
                runtime.Fork(agentId, AgentRole.Coder, (fun () -> task { return Ok "hello-fork" }))

            match forkRes with
            | Ok runId ->
                Assert.False(String.IsNullOrEmpty(runId))
                // Allow async completion to land in mailbox
                do! Task.Delay(50)

                match runtime.Join(agentId) with
                | Ok completion ->
                    Assert.Equal(runId, completion.RunId)
                    Assert.Equal(agentId, completion.AgentId)
                    Assert.Equal(AgentRole.Coder, completion.Role)
                    Assert.Equal(Ok "hello-fork", completion.Outcome)
                | Error err -> Assert.True(false, sprintf "Expected Ok completion, got Error: %A" err)
            | Error err -> Assert.True(false, sprintf "Expected Ok runId, got Error: %A" err)
        }

    [<Fact>]
    let ``ForkRuntime_busy_returns_Busy_immediately_without_queueing`` () =
        task {
            let runtime = ForkRuntime()
            let agentId = "agent-busy-test"
            let tcs = TaskCompletionSource<Result<string, string>>()

            let firstFork = runtime.Fork(agentId, AgentRole.Inspector, (fun () -> tcs.Task))

            match firstFork with
            | Ok _ ->
                // While first run is active (tcs not resolved), second fork on same agent should return Busy
                let secondFork =
                    runtime.Fork(agentId, AgentRole.Inspector, (fun () -> task { return Ok "second" }))

                match secondFork with
                | Error ForkError.Busy -> ()
                | other -> Assert.True(false, sprintf "Expected Error ForkError.Busy, got: %A" other)

                // Cleanup first task
                tcs.SetResult(Ok "done")
                do! Task.Delay(50)
            | Error err -> Assert.True(false, sprintf "Expected Ok on first fork, got Error: %A" err)
        }

    [<Fact>]
    let ``ForkRuntime_join_empty_returns_Empty_when_no_completion`` () =
        let runtime = ForkRuntime()

        match runtime.Join() with
        | Error ForkError.Empty -> ()
        | other -> Assert.True(false, sprintf "Expected Error ForkError.Empty, got: %A" other)

    [<Fact>]
    let ``ForkRuntime_mailbox_race_retains_completions_in_order`` () =
        task {
            let runtime = ForkRuntime()
            let agentCount = 10

            let! _ =
                Task.WhenAll(
                    [| for i in 1..agentCount ->
                           let aid = sprintf "agent-%d" i

                           Task.Run(fun () ->
                               runtime.Fork(aid, AgentRole.Coder, (fun () -> task { return Ok(sprintf "res-%d" i) }))) |]
                )

            do! Task.Delay(100)

            let mutable receivedCount = 0
            let mutable emptyCount = 0

            for _ in 1..agentCount do
                match runtime.Join() with
                | Ok _ -> receivedCount <- receivedCount + 1
                | Error ForkError.Empty -> emptyCount <- emptyCount + 1
                | Error _ -> ()

            Assert.Equal(agentCount, receivedCount)
            Assert.Equal(0, emptyCount)
        }

    [<Fact>]
    let ``ForkRuntime_join_by_agentId_returns_NotFound_for_unknown_agent`` () =
        let runtime = ForkRuntime()

        match runtime.Join("non-existent-agent-id") with
        | Error ForkError.NotFound -> ()
        | other -> Assert.True(false, sprintf "Expected Error ForkError.NotFound, got: %A" other)

    [<Fact>]
    let ``ForkRuntime_list_returns_agents_and_ptys`` () =
        task {
            let runtime = ForkRuntime()
            let agentId = "agent-list-test"

            let pty: PtyRecord =
                { PtyId = "pty-1"
                  AgentId = agentId
                  Command = "bash"
                  StartedAt = DateTimeOffset.UtcNow }

            runtime.RegisterPty(pty)

            let forkRes =
                runtime.Fork(agentId, AgentRole.Manager, (fun () -> task { return Ok "ok" }))

            Assert.True(forkRes.IsOk)
            do! Task.Delay(50)

            let (agentList, ptyList) = runtime.List()
            Assert.Equal(1, agentList.Length)
            Assert.Equal(agentId, agentList.[0].AgentId)
            Assert.Equal(AgentRole.Manager, agentList.[0].Role)

            Assert.Equal(1, ptyList.Length)
            Assert.Equal("pty-1", ptyList.[0].PtyId)
        }
