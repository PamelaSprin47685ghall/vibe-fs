namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process

module ProcessTests =

    [<Fact>]
    let Process_execute_true_command_returns_exitCode_0 () =
        task {
            let isWindows = false
            let cmdName = if isWindows then "cmd.exe" else "true"
            let cmdArgs = if isWindows then [ "/c"; "exit 0" ] else []

            let cmd: Command =
                { FileName = cmdName
                  Arguments = cmdArgs
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let ctx: ProcessContext =
                { WorkingDirectory = None
                  DefaultTimeout = Some(TimeSpan.FromSeconds 5.0) }

            let! res = Flow.run ctx CancellationToken.None (ProcessFlows.execute cmd)

            match res with
            | Ok processResult ->
                Assert.Equal(0, processResult.ExitCode)
                Assert.False(processResult.StdoutTruncated)
                Assert.False(processResult.StderrTruncated)
            | Error err -> Assert.True(false, sprintf "Expected Ok processResult exit code 0, got Error: %A" err)
        }

    [<Fact>]
    let Process_execute_stdin_roundtrip () =
        task {
            let isWindows = false
            let payload = "hello-stdin-wanxiangshu"
            let cmdName = if isWindows then "cmd.exe" else "cat"
            let cmdArgs = if isWindows then [ "/c"; "findstr"; "/R"; ".*" ] else []

            let cmd: Command =
                { FileName = cmdName
                  Arguments = cmdArgs
                  WorkingDirectory = None
                  Environment = None
                  Stdin = Some(payload + "\n")
                  Deadline = None
                  PtyOptions = None }

            let ctx: ProcessContext =
                { WorkingDirectory = None
                  DefaultTimeout = Some(TimeSpan.FromSeconds 5.0) }

            let! res = Flow.run ctx CancellationToken.None (ProcessFlows.execute cmd)

            match res with
            | Ok processResult ->
                Assert.Equal(0, processResult.ExitCode)

                Assert.True(
                    processResult.Stdout.Contains(payload),
                    sprintf "Expected stdout to contain payload %s, got stdout: %s" payload processResult.Stdout
                )
            | Error err ->
                Assert.True(false, sprintf "Expected Ok processResult with stdin roundtrip, got Error: %A" err)
        }

    [<Fact>]
    let Process_spawn_nonexistent_binary_returns_SpawnFailed () =
        task {
            let nonExistentBinary =
                "non_existent_binary_wanxiangshu_xyz_" + Guid.NewGuid().ToString("N")

            let cmd: Command =
                { FileName = nonExistentBinary
                  Arguments = []
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! spawnRes = ProcessSpawn.spawn cmd None CancellationToken.None

            match spawnRes with
            | Error(ProcessError.SpawnFailed reason) -> Assert.False(String.IsNullOrWhiteSpace(reason))
            | other -> Assert.True(false, sprintf "Expected Error (SpawnFailed), got: %A" other)
        }

    [<Fact>]
    let Process_execute_cancel_long_running_returns_ProcessCancelled () =
        task {
            let isWindows = false
            let cmdName = if isWindows then "cmd.exe" else "cat"

            let cmdArgs = []

            let cmd: Command =
                { FileName = cmdName
                  Arguments = cmdArgs
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let ctx: ProcessContext =
                { WorkingDirectory = None
                  DefaultTimeout = Some(TimeSpan.FromSeconds 60.0) }

            use cts = new CancellationTokenSource()

            let flowTask = Flow.run ctx cts.Token (ProcessFlows.execute cmd)
            cts.Cancel()

            let! res = flowTask

            match res with
            | Error(ProcessError.ProcessCancelled reason) -> Assert.False(String.IsNullOrWhiteSpace(reason))
            | other -> Assert.True(false, sprintf "Expected Error (ProcessCancelled), got: %A" other)
        }

    [<Fact>]
    let Process_execute_short_deadline_returns_Timeout () =
        task {
            let isWindows = false
            let cmdName = if isWindows then "cmd.exe" else "cat"

            let cmdArgs = []

            let now = DateTimeOffset.UtcNow
            let deadline = Deadline.ofBudget (now.AddDays(-1.0)) (TimeSpan.FromMilliseconds 20.0)

            let cmd: Command =
                { FileName = cmdName
                  Arguments = cmdArgs
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = Some deadline
                  PtyOptions = None }

            let ctx: ProcessContext =
                { WorkingDirectory = None
                  DefaultTimeout = None }

            let! res = Flow.run ctx CancellationToken.None (ProcessFlows.execute cmd)

            match res with
            | Error(ProcessError.Timeout reason) -> Assert.False(String.IsNullOrWhiteSpace(reason))
            | other -> Assert.True(false, sprintf "Expected Error (Timeout), got: %A" other)
        }

    [<Fact>]
    let Process_kill_is_idempotent () =
        task {
            let isWindows = false
            let cmdName = if isWindows then "cmd.exe" else "cat"

            let cmdArgs = []

            let cmd: Command =
                { FileName = cmdName
                  Arguments = cmdArgs
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! spawnRes = ProcessSpawn.spawn cmd None CancellationToken.None

            match spawnRes with
            | Ok handle ->
                let h = handle

                try
                    do! h.Kill()
                    do! h.Kill()
                finally
                    h.Dispose()
            | Error err -> Assert.True(false, sprintf "Expected Ok handle, got: %A" err)
        }

    [<Fact>]
    let Process_runFlow_maps_OCE_to_ProcessCancelled () =
        task {
            use cts = new CancellationTokenSource()
            cts.Cancel()

            let ctx: ProcessContext =
                { WorkingDirectory = None
                  DefaultTimeout = None }

            let hangingFlow: ProcessFlows.ProcessFlow<Fact.ProcessResult> =
                Flow.create (fun _ ct ->
                    task {
                        ct.ThrowIfCancellationRequested()
                        return Ok Unchecked.defaultof<Fact.ProcessResult>
                    })

            let! res = ProcessFlows.runFlow ctx cts.Token hangingFlow

            match res with
            | Error(ProcessError.ProcessCancelled reason) -> Assert.False(String.IsNullOrWhiteSpace(reason))
            | other -> Assert.True(false, sprintf "Expected Error (ProcessCancelled), got: %A" other)
        }
