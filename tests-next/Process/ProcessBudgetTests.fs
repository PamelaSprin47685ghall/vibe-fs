namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process

module ProcessBudgetTests =

    [<Fact>]
    let ``ProcessBudget_calculateDeadline_is_three_times_estimated_runtime`` () =
        let estRuntime = RuntimeSeconds 5.0
        let now = DateTimeOffset.UtcNow
        let deadline = ProcessBudget.calculateDeadline now estRuntime

        let remaining = Deadline.remaining (fun () -> now) deadline

        Assert.True(
            Math.Abs(remaining.TotalSeconds - 15.0) < 0.5,
            sprintf "Expected remaining ~15.0s, got %f" remaining.TotalSeconds
        )

    [<Fact>]
    let ``ProcessBudget_large_gate_serializes_large_memory_commands`` () =
        task {
            Assert.Equal(1, ProcessBudget.getLargeGateCount ())

            use cts = new CancellationTokenSource()

            let est: ProcessEstimate =
                { EstimatedRuntime = RuntimeSeconds 10.0
                  EstimatedOutput = OutputBytes 1024L
                  EstimatedMemory = EstimatedMemory.Large }

            do! ProcessBudget.acquireLargeGate (cts.Token)

            try
                Assert.Equal(0, ProcessBudget.getLargeGateCount ())
            finally
                ProcessBudget.releaseLargeGate ()

            Assert.Equal(1, ProcessBudget.getLargeGateCount ())
        }

    [<Fact>]
    let ``ProcessBudget_normal_command_returns_Completed`` () =
        task {
            let isWindows = false
            let cmdName = if isWindows then "cmd.exe" else "echo"
            let cmdArgs = if isWindows then [ "/c"; "echo hi" ] else [ "hi" ]

            let cmd: Command =
                { FileName = cmdName
                  Arguments = cmdArgs
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let est: ProcessEstimate =
                { EstimatedRuntime = RuntimeSeconds 5.0
                  EstimatedOutput = OutputBytes 1024L
                  EstimatedMemory = EstimatedMemory.Medium }

            let ctx: ProcessContext =
                { WorkingDirectory = None
                  DefaultTimeout = None }

            let! outcome = ProcessBudget.executeWithBudget cmd est ctx CancellationToken.None

            match outcome with
            | BudgetOutcome.Completed(exitCode, stdout, stderr, spooled) ->
                Assert.Equal(0, exitCode)
                Assert.False(spooled)
                Assert.True(stdout.Contains("hi"))
            | other -> Assert.True(false, sprintf "Expected BudgetOutcome.Completed, got: %A" other)
        }

    [<Fact>]
    let ``ProcessBudget_spool_threshold_spools_large_output`` () =
        task {
            let isWindows = false
            let cmdName = if isWindows then "cmd.exe" else "python3"
            let pythonScript = "print('x' * 250000)"

            let cmdArgs =
                if isWindows then
                    [ "/c"; sprintf "python -c \"%s\"" pythonScript ]
                else
                    [ "-c"; pythonScript ]

            let cmd: Command =
                { FileName = cmdName
                  Arguments = cmdArgs
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let est: ProcessEstimate =
                { EstimatedRuntime = RuntimeSeconds 5.0
                  EstimatedOutput = OutputBytes 100000L
                  EstimatedMemory = EstimatedMemory.Medium }

            let ctx: ProcessContext =
                { WorkingDirectory = None
                  DefaultTimeout = None }

            let! outcome = ProcessBudget.executeWithBudget cmd est ctx CancellationToken.None

            match outcome with
            | BudgetOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount) ->
                Assert.Equal(0, exitCode)
                Assert.True(totalBytes > 200000L)
                Assert.True(chunkCount >= 1)
                Assert.True(System.IO.File.Exists(spoolPath))
                System.IO.File.Delete(spoolPath)
            | BudgetOutcome.OutputExceeded(totalBytes, spoolPathOpt) ->
                Assert.True(totalBytes > 200000L)

                match spoolPathOpt with
                | Some p ->
                    Assert.True(System.IO.File.Exists(p))
                    System.IO.File.Delete(p)
                | None -> ()
            | other -> Assert.True(false, sprintf "Expected Spooled or OutputExceeded, got: %A" other)
        }
