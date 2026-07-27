namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Kernel.Identity

module PtyLifecycleTests =
    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

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

    type private FakePtyBackend(?termIgnored: bool, ?throwOnWrite: bool, ?killFails: bool) =
        let termIgnored = defaultArg termIgnored false
        let throwOnWrite = defaultArg throwOnWrite false
        let killFails = defaultArg killFails false

        let live =
            Dictionary<string, StringBuilder * ref<bool> * TaskCompletionSource<unit>>()

        let signals = ResizeArray<string * PtySignal>()
        let portRef = ref Unchecked.defaultof<PtyPort>
        let mutable pendingRead: PtyId option = None
        let spawnReady = TaskCompletionSource<unit>()

        member private this.ExitPty(id: PtyId) =
            match live.TryGetValue id.Value with
            | true, (buf, closed, exitTcs) ->
                closed.Value <- true
                let residual = buf.ToString()
                live.Remove id.Value |> ignore
                exitTcs.SetResult(())

                let outcome =
                    if String.IsNullOrEmpty residual then
                        Ok PtyOutcome.Closed
                    else
                        Ok residual

                portRef.Value.Complete(id, outcome) |> ignore
            | _ -> ()

        member this.Handler: PtyBackendHandler =
            fun (id: PtyId) (command: PtyCommand) ->
                task {
                    match command with
                    | PtyCommand.Spawn(_, _) ->
                        let exitTcs = TaskCompletionSource<unit>()
                        let buf = StringBuilder()
                        let closed = ref false
                        live.[id.Value] <- (buf, closed, exitTcs)
                        portRef.Value.RegisterExitTask(id, exitTcs.Task)
                        spawnReady.SetResult(())
                        return Ok()
                    | PtyCommand.Write bytes ->
                        match live.TryGetValue id.Value with
                        | true, (buf, closed, _) when not closed.Value ->
                            if throwOnWrite then
                                return Error "boom"
                            else
                                buf.Append(Encoding.UTF8.GetString bytes) |> ignore
                                return Ok()
                        | true, (_, closed, _) when closed.Value -> return Error "PTY closed"
                        | true, _ -> return Error "PTY closed"
                        | false, _ -> return Error "Unknown PTY id"
                    | PtyCommand.Read ->
                        pendingRead <- Some id
                        return Ok()
                    | PtyCommand.Signal signal ->
                        signals.Add(id.Value, signal)

                        match signal with
                        | PtySignal.Kill ->
                            if killFails then
                                return Error "kill failed: ESRCH"
                            else
                                this.ExitPty id
                                return Ok()
                        | PtySignal.Terminate ->
                            if not termIgnored then
                                this.ExitPty id

                            return Ok()
                        | PtySignal.Interrupt
                        | PtySignal.Hangup
                        | PtySignal.Quit
                        | PtySignal.User1
                        | PtySignal.User2 -> return Ok()
                    | PtyCommand.Resize _ -> return Ok()
                }

        member this.MakePort(?mailboxSender) =
            let p = PtyPort(?mailboxSender = mailboxSender, handler = this.Handler)
            portRef.Value <- p
            p

        member this.Port = this.MakePort()
        member _.WaitForSpawn() = spawnReady.Task
        member _.Signals = signals |> Seq.toList
        member _.LiveCount = live.Count

        member _.AppendOutput(id: PtyId, text: string) =
            match live.TryGetValue id.Value with
            | true, (buf, _, _) -> buf.Append(text) |> ignore
            | _ -> ()

        member this.ForceExit(id: PtyId) = this.ExitPty id

        member _.ResolveRead(id: PtyId) =
            match live.TryGetValue id.Value with
            | true, (buf, closed, _) ->
                let text = buf.ToString()
                buf.Clear() |> ignore
                pendingRead <- None
                portRef.Value.ReadResult(id, text, closed.Value)
            | false, _ -> ()

        member _.TriggerSpawnFailure(id: PtyId) =
            let msg = "PTY spawn failed: boom"
            portRef.Value.FailRead(id, msg)
            live.Remove id.Value |> ignore
            portRef.Value.Complete(id, Error msg) |> ignore

    [<Fact>]
    let ``Pty_second_concurrent_read_returns_error_first_still_resolves`` () =
        task {
            let b = FakePtyBackend()
            let p = b.Port
            let id = p.Fork("cat")
            do! b.WaitForSpawn()
            let first = p.Read(id)
            let! second = p.Read(id)
            equal (Error "PTY read already in progress") second
            let! _ = b.Handler id PtyCommand.Read
            b.ResolveRead(id)
            let! firstResult = first
            equal (Ok("", false)) firstResult
        }

    [<Fact>]
    let ``Pty_spawn_failure_flushes_parked_read_and_completes_with_error`` () =
        task {
            let completions = ResizeArray<RunCompletion>()
            let b = FakePtyBackend()
            let p = b.MakePort(mailboxSender = (fun c -> completions.Add c))
            let id = p.Fork("boom")
            do! b.WaitForSpawn()
            let readTask = p.Read(id)
            b.TriggerSpawnFailure(id)
            let! read = readTask
            equal (Error "PTY spawn failed: boom") read
            equal 1 completions.Count
            equal (Error "PTY spawn failed: boom") completions.[0].Outcome
        }

    [<Fact>]
    let ``Pty_close_while_read_parked_flushes_read_and_completes_once`` () =
        task {
            let completions = ResizeArray<RunCompletion>()
            let b = FakePtyBackend()
            let p = b.MakePort(mailboxSender = (fun c -> completions.Add c))
            let id = p.Fork("cat")
            do! b.WaitForSpawn()
            let readTask = p.Read(id)
            p.Close(id)
            let! read = readTask
            equal (Error "PTY closed before read completed") read
            equal 1 completions.Count
        }

    [<Fact>]
    let ``Pty_CloseAll_on_term_ignoring_process_sends_term_then_kill_and_reaps`` () =
        task {
            let completions = ResizeArray<RunCompletion>()
            let b = FakePtyBackend(termIgnored = true)
            let p = b.MakePort(mailboxSender = (fun c -> completions.Add c))
            let id = p.Fork("loop")
            do! b.WaitForSpawn()
            do! p.CloseAll(graceMs = 0)
            equal [ PtySignal.Terminate; PtySignal.Kill ] (b.Signals |> List.map snd)
            equal 1 completions.Count
            equal 0 b.LiveCount
        }

    [<Fact>]
    let ``Pty_on_exit_drains_residual_buffer_into_join_and_post_exit_read_is_closed`` () =
        task {
            let completions = ResizeArray<RunCompletion>()
            let b = FakePtyBackend()
            let p = b.MakePort(mailboxSender = (fun c -> completions.Add c))
            let id = p.Fork("cat")
            do! b.WaitForSpawn()
            b.AppendOutput(id, "hello world")
            b.ForceExit(id)
            equal 1 completions.Count
            equal (Ok "hello world") completions.[0].Outcome
            let! read = p.Read(id)
            equal (Ok("", true)) read
        }

    [<Fact>]
    let ``Pty_backend_state_is_per_port_isolated`` () =
        task {
            let b1 = FakePtyBackend()
            let p1 = b1.Port
            let b2 = FakePtyBackend()
            let p2 = b2.Port
            let id1 = p1.Fork("a")
            let id2 = p2.Fork("b")
            do! b1.WaitForSpawn()
            do! b2.WaitForSpawn()
            do! p1.CloseAll(graceMs = 0)
            equal false (p1.Exists id1)
            equal true (p2.Exists id2)
            equal 1 b2.LiveCount
        }

    [<Fact>]
    let ``Pty_write_to_closed_and_backend_throw_surface_as_errors`` () =
        task {
            let b = FakePtyBackend()
            let p = b.Port
            let id = p.Fork("cat")
            do! b.WaitForSpawn()
            p.Close(id)
            let! closed = p.Send(id, PtyCommand.Write(Pty.bytes "x"))

            match closed with
            | Error _ -> ()
            | Ok _ -> failwith "closed write should fail"

            let b2 = FakePtyBackend(throwOnWrite = true)
            let p2 = b2.Port
            let id2 = p2.Fork("cat")
            do! b2.WaitForSpawn()
            let! failed = p2.Send(id2, PtyCommand.Write(Pty.bytes "x"))

            match failed with
            | Error _ -> ()
            | Ok _ -> failwith "backend write should fail"
        }

    /// TERM barrier: Close sends TERM, but when the process ignores TERM,
    /// no completion is published before onExit fires. This proves completion
    /// is published ONLY by onExit → Complete, not by Close/TERM.
    [<Fact>]
    let ``Pty_Close_no_completion_until_onExit_after_term_barrier`` () =
        task {
            let completions = ResizeArray<RunCompletion>()
            let b = FakePtyBackend(termIgnored = true)
            let p = b.MakePort(mailboxSender = (fun c -> completions.Add c))
            let id = p.Fork("stubborn")
            do! b.WaitForSpawn()
            // Send TERM via Close — TERM is ignored, no exit, no completion.
            p.Close(id)
            equal 1 b.Signals.Length
            equal PtySignal.Terminate (b.Signals |> List.head |> snd)
            equal 0 completions.Count
            // Now force exit (simulates backend onExit firing).
            b.ForceExit(id)
            equal 1 completions.Count
            equal id.Value completions.[0].RunId
        }

    /// KILL error propagation via the FakePtyBackend: when KILL fails,
    /// CloseAll must surface the error, not hang forever waiting for onExit.
    [<Fact>]
    let ``Pty_CloseAll_surfaces_kill_error_when_kill_fails`` () =
        task {
            let b = FakePtyBackend(termIgnored = true, killFails = true)
            let p = b.Port
            let id = p.Fork("unkillable")
            do! b.WaitForSpawn()
            let! captured = Record.ExceptionAsync(fun () -> p.CloseAll(graceMs = 0))
            equal true captured.IsSome
        }
