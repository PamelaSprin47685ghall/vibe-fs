namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Collections.Concurrent
open System.Threading.Channels
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module PtyTests =

    [<Fact>]
    let ``Pty_spawn_write_read_signal_resize_exit_preserves_command_ordering`` () =
        let commandLog = ConcurrentQueue<PtyId * PtyCommand>()
        let backendHandler id cmd = commandLog.Enqueue(id, cmd)

        let mailbox = Channel.CreateUnbounded<RunCompletion>()
        let port = PtyPort(mailbox = mailbox, handler = backendHandler)

        let ptyId = Pty.forkPty port "sh -c cat"
        Assert.False(String.IsNullOrEmpty(ptyId.Value))

        Pty.send port ptyId (PtyCommand.Write [| 65uy; 66uy; 67uy |])
        Pty.send port ptyId PtyCommand.Read
        Pty.send port ptyId (PtyCommand.Resize(120, 40))
        Pty.send port ptyId (PtyCommand.Signal PtySignal.Interrupt)
        Pty.close port ptyId

        let loggedCommands = commandLog.ToArray() |> Array.map snd

        Assert.Equal(6, loggedCommands.Length)

        match loggedCommands.[0] with
        | PtyCommand.Spawn cmd -> Assert.Equal("sh -c cat", cmd)
        | other -> Assert.True(false, sprintf "Expected Spawn, got %A" other)

        match loggedCommands.[1] with
        | PtyCommand.Write bytes -> Assert.Equal<byte[]>([| 65uy; 66uy; 67uy |], bytes)
        | other -> Assert.True(false, sprintf "Expected Write, got %A" other)

        match loggedCommands.[2] with
        | PtyCommand.Read -> ()
        | other -> Assert.True(false, sprintf "Expected Read, got %A" other)

        match loggedCommands.[3] with
        | PtyCommand.Resize(w, h) ->
            Assert.Equal(120, w)
            Assert.Equal(40, h)
        | other -> Assert.True(false, sprintf "Expected Resize, got %A" other)

        match loggedCommands.[4] with
        | PtyCommand.Signal PtySignal.Interrupt -> ()
        | other -> Assert.True(false, sprintf "Expected Signal Interrupt, got %A" other)

        match loggedCommands.[5] with
        | PtyCommand.Signal PtySignal.Terminate -> ()
        | other -> Assert.True(false, sprintf "Expected Signal Terminate on close, got %A" other)

        // Check completion delivered to mailbox
        let mutable completion = Unchecked.defaultof<RunCompletion>
        Assert.True(mailbox.Reader.TryRead(&completion))
        Assert.Equal(ptyId.Value, completion.RunId)
        Assert.Equal(Ok "closed", completion.Outcome)

    [<Fact>]
    let ``Pty_mixed_list_returns_agent_and_pty_snapshots`` () =
        let mockAgents () : AgentRecord list =
            [ { AgentId = "agent-alpha"
                Role = AgentRole.Coder
                Status = AgentStatus.Busy
                CurrentRunId = Some "run-1" } ]

        let port = PtyPort(agentProvider = mockAgents)
        let ptyId = port.Fork("top", agentId = "agent-alpha", role = AgentRole.Coder)

        let (agentSnapshots, ptySnapshots) = Pty.list port

        Assert.Single(agentSnapshots)
        Assert.Equal("agent-alpha", agentSnapshots.[0].AgentId)
        Assert.Equal(AgentStatus.Busy, agentSnapshots.[0].Status)

        Assert.Single(ptySnapshots)
        Assert.Equal(ptyId, ptySnapshots.[0].Id)
        Assert.Equal("top", ptySnapshots.[0].Command)
        Assert.Equal(Some "agent-alpha", ptySnapshots.[0].AgentId)
        Assert.Equal(Some AgentRole.Coder, ptySnapshots.[0].Role)

    [<Fact>]
    let ``Pty_completion_delivered_exactly_once_on_repeated_close_and_parent_cancellation`` () =
        let mailbox = Channel.CreateUnbounded<RunCompletion>()
        let port = PtyPort(mailbox = mailbox)

        let ptyId = Pty.forkPty port "tail -f log"

        // Perform multiple concurrent/sequential closes and parent cancellation
        Pty.close port ptyId
        Pty.close port ptyId
        Pty.send port ptyId (PtyCommand.Signal PtySignal.Kill)
        port.CloseAll()

        // Verify mailbox received exactly one completion item
        let completions = System.Collections.Generic.List<RunCompletion>()
        let mutable item = Unchecked.defaultof<RunCompletion>

        while mailbox.Reader.TryRead(&item) do
            completions.Add(item)

        Assert.Single(completions)
        Assert.Equal(ptyId.Value, completions.[0].RunId)

    [<Fact>]
    let ``Pty_typed_commands_no_magic_string_parsing`` () =
        let testCmd (cmd: PtyCommand) =
            match cmd with
            | PtyCommand.Spawn c -> sprintf "spawn:%s" c
            | PtyCommand.Write b -> sprintf "write:%d" b.Length
            | PtyCommand.Read -> "read"
            | PtyCommand.Signal s ->
                match s with
                | PtySignal.Terminate -> "signal:terminate"
                | PtySignal.Kill -> "signal:kill"
                | PtySignal.Interrupt -> "signal:interrupt"
            | PtyCommand.Resize(w, h) -> sprintf "resize:%dx%d" w h

        Assert.Equal("spawn:ls", testCmd (PtyCommand.Spawn "ls"))
        Assert.Equal("write:4", testCmd (PtyCommand.Write [| 1uy; 2uy; 3uy; 4uy |]))
        Assert.Equal("read", testCmd PtyCommand.Read)
        Assert.Equal("signal:interrupt", testCmd (PtyCommand.Signal PtySignal.Interrupt))
        Assert.Equal("resize:80x24", testCmd (PtyCommand.Resize(80, 24)))
