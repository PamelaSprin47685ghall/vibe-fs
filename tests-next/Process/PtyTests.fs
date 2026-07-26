namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Xunit
open Fable.Core.JsInterop
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Kernel.Identity

module PtyTests =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private trueThat condition message =
        if not condition then
            failwith message

    let private ok =
        function
        | Ok value -> value
        | Error error -> failwithf "Unexpected error: %A" error

    let private hostPort (childId: SessionId) =
        { new ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) =
                Task.FromResult(Ok(MessageId.create "accepted"))

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task
            member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
            member _.GetSessionOutput(_) = [] }

    [<Fact>]
    let ``HostForkRuntime_pty_dsl_writes_reads_signals_and_joins`` () =
        task {
            let log = ResizeArray<PtyCommand>()
            // Mutable so the mock backend can resolve a Read through the port.
            let mutable ptyPort = Unchecked.defaultof<PtyPort>

            let handler id command =
                match command with
                | PtyCommand.Read -> ptyPort.ReadResult(id, "buffered", false)
                | _ -> log.Add command

            let p = PtyPort(handler = handler)
            ptyPort <- p

            let bridge =
                HostForkRuntime(SessionId.create "pty-parent", hostPort (SessionId.create "pty-child"), ptyPort = p)

            let! created = bridge.ForkPty("cat", cwd = "/workspace")
            let id = ok created
            // Spawn must carry the cwd through to the backend.
            match log.[0] with
            | PtyCommand.Spawn(cmd, cwd) ->
                equal "cat" cmd
                equal "/workspace" cwd
            | other -> failwithf "Expected Spawn with cwd, got %A" other

            let! writeResult = bridge.SendPty(id, "abc", None)
            let write = ok writeResult
            equal id write.Id
            equal "" write.Output
            equal false write.Closed
            equal 2 log.Count

            match log.[1] with
            | PtyCommand.Write bytes -> equal [| 97uy; 98uy; 99uy |] bytes
            | other -> failwithf "Unexpected PTY write: %A" other

            // Read returns the buffered output immediately, without completing join.
            // The mock backend resolves the Read through the port (not logged).
            let! readResult = bridge.SendPty(id, "", None)
            let read = ok readResult
            equal id read.Id
            equal "buffered" read.Output
            equal false read.Closed
            equal 2 log.Count

            // Signal forwards to the backend but does NOT complete the join.
            let! signalled = bridge.ForkPty("cat", cwd = "/workspace")
            let signalledId = ok signalled

            match log.[2] with
            | PtyCommand.Spawn(cmd, cwd) ->
                equal "cat" cmd
                equal "/workspace" cwd
            | other -> failwithf "Expected Spawn with cwd, got %A" other

            let! sigResult = bridge.SendPty(signalledId, "", Some PtySignal.Kill)
            equal signalledId (ok sigResult).Id

            match log.[3] with
            | PtyCommand.Signal PtySignal.Kill -> ()
            | other -> failwithf "Unexpected PTY signal: %A" other

            // The only completion path is the backend exit (onExit -> Complete).
            p.Complete(signalledId, outcome = Ok PtyOutcome.Closed)
            let! signalCompletion = bridge.Join()
            let signalCompletion = ok signalCompletion
            equal signalledId.Value signalCompletion.RunId
            equal (Ok PtyOutcome.Closed) signalCompletion.Outcome
            equal 4 log.Count
        }

    [<Fact>]
    let ``HostForkRuntime_pty_exit_list_and_parent_abort_are_deterministic`` () =
        task {
            let log = ResizeArray<PtyCommand>()
            let port = PtyPort(handler = (fun _ command -> log.Add command))
            let parent = SessionId.create "pty-parent-lifecycle"
            let host = hostPort (SessionId.create "pty-child-lifecycle")
            let bridge = HostForkRuntime(parent, host, ptyPort = port)
            let! created = bridge.ForkPty("echo", cwd = "/workspace")
            let id = ok created
            port.Complete(id, outcome = Ok "output")
            let! completion = bridge.Join()
            equal (Ok "output") (ok completion).Outcome

            let! _ = bridge.ForkPty("stay", cwd = "/workspace")
            let! _ = bridge.Fork("agent-list", AgentRole.Coder, "work")
            let agents, ptys = bridge.List()
            equal 1 agents.Length
            equal 1 ptys.Length

            let router =
                HostEventRouter(
                    host,
                    Dictionary<string, string>(),
                    Dictionary<string, string>(),
                    HashSet<string>(),
                    HashSet<string>()
                )

            let aborted =
                createObj
                    [ "event",
                      box (
                          createObj
                              [ "type", box "session.error"
                                "properties",
                                box (
                                    createObj
                                        [ "sessionID", box (SessionId.value parent)
                                          "error", box (createObj [ "name", box "MessageAbortedError" ]) ]
                                ) ]
                      ) ]

            router.Observe(aborted, ignore)

            equal
                1
                (log
                 |> Seq.filter (function
                     | PtyCommand.Signal PtySignal.Terminate -> true
                     | _ -> false)
                 |> Seq.length)

            let _, remaining = bridge.List()
            equal 0 remaining.Length
        }

    [<Fact>]
    let ``Pty_spawn_write_read_signal_resize_exit_preserves_command_ordering`` () =
        let commandLog = ResizeArray<PtyId * PtyCommand>()
        let completions = ResizeArray<RunCompletion>()
        let backendHandler id cmd = commandLog.Add(id, cmd)

        let port =
            PtyPort(mailboxSender = (fun completion -> completions.Add completion), handler = backendHandler)

        let ptyId = Pty.forkPty port "sh -c cat"
        trueThat (not (String.IsNullOrEmpty(ptyId.Value))) "PTY fork must return an id"

        Pty.send port ptyId (PtyCommand.Write [| 65uy; 66uy; 67uy |])
        Pty.send port ptyId PtyCommand.Read
        Pty.send port ptyId (PtyCommand.Resize(120, 40))
        Pty.send port ptyId (PtyCommand.Signal PtySignal.Interrupt)
        Pty.close port ptyId

        let loggedCommands = commandLog |> Seq.map snd |> Seq.toArray

        equal 6 loggedCommands.Length

        match loggedCommands.[0] with
        | PtyCommand.Spawn(cmd, _) -> equal "sh -c cat" cmd
        | other -> failwithf "Expected Spawn, got %A" other

        match loggedCommands.[1] with
        | PtyCommand.Write bytes -> equal [| 65uy; 66uy; 67uy |] bytes
        | other -> failwithf "Expected Write, got %A" other

        match loggedCommands.[2] with
        | PtyCommand.Read -> ()
        | other -> failwithf "Expected Read, got %A" other

        match loggedCommands.[3] with
        | PtyCommand.Resize(w, h) ->
            equal 120 w
            equal 40 h
        | other -> failwithf "Expected Resize, got %A" other

        match loggedCommands.[4] with
        | PtyCommand.Signal PtySignal.Interrupt -> ()
        | other -> failwithf "Expected Signal Interrupt, got %A" other

        match loggedCommands.[5] with
        | PtyCommand.Signal PtySignal.Terminate -> ()
        | other -> failwithf "Expected Signal Terminate on close, got %A" other

        equal 1 completions.Count
        equal ptyId.Value completions.[0].RunId
        equal (Ok "closed") completions.[0].Outcome

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

        equal 1 agentSnapshots.Length
        equal "agent-alpha" agentSnapshots.[0].AgentId
        equal AgentStatus.Busy agentSnapshots.[0].Status

        equal 1 ptySnapshots.Length
        equal ptyId ptySnapshots.[0].Id
        equal "top" ptySnapshots.[0].Command
        equal (Some "agent-alpha") ptySnapshots.[0].AgentId
        equal (Some AgentRole.Coder) ptySnapshots.[0].Role

    [<Fact>]
    let ``Pty_completion_delivered_exactly_once_on_repeated_close_and_parent_cancellation`` () =
        let completions = ResizeArray<RunCompletion>()
        let port = PtyPort(mailboxSender = (fun completion -> completions.Add completion))

        let ptyId = Pty.forkPty port "tail -f log"

        // Perform multiple concurrent/sequential closes and parent cancellation
        Pty.close port ptyId
        Pty.close port ptyId
        Pty.send port ptyId (PtyCommand.Signal PtySignal.Kill)
        port.CloseAll()

        equal 1 completions.Count
        equal ptyId.Value completions.[0].RunId

    [<Fact>]
    let ``Pty_typed_commands_no_magic_string_parsing`` () =
        let testCmd (cmd: PtyCommand) =
            match cmd with
            | PtyCommand.Spawn(cmd, cwd) -> sprintf "spawn:%s" cmd
            | PtyCommand.Write b -> sprintf "write:%d" b.Length
            | PtyCommand.Read -> "read"
            | PtyCommand.Signal s ->
                match s with
                | PtySignal.Terminate -> "signal:terminate"
                | PtySignal.Kill -> "signal:kill"
                | PtySignal.Interrupt -> "signal:interrupt"
            | PtyCommand.Resize(w, h) -> sprintf "resize:%dx%d" w h

        equal "spawn:ls" (testCmd (PtyCommand.Spawn("ls", "")))
        equal "write:4" (testCmd (PtyCommand.Write [| 1uy; 2uy; 3uy; 4uy |]))
        equal "read" (testCmd PtyCommand.Read)
        equal "signal:interrupt" (testCmd (PtyCommand.Signal PtySignal.Interrupt))
        equal "resize:80x24" (testCmd (PtyCommand.Resize(80, 24)))
