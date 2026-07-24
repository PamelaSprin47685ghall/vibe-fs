namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Collections.Generic
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module PtyTests =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private trueThat condition message =
        if not condition then
            failwith message

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
        | PtyCommand.Spawn cmd -> equal "sh -c cat" cmd
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

        equal "spawn:ls" (testCmd (PtyCommand.Spawn "ls"))
        equal "write:4" (testCmd (PtyCommand.Write [| 1uy; 2uy; 3uy; 4uy |]))
        equal "read" (testCmd PtyCommand.Read)
        equal "signal:interrupt" (testCmd (PtyCommand.Signal PtySignal.Interrupt))
        equal "resize:80x24" (testCmd (PtyCommand.Resize(80, 24)))
