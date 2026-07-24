namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Process

module ProcessRunnerTests =

    let private neverCompletes<'T> () : Task<'T> = emitJsExpr () "new Promise(() => {})"

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private trueThat condition message =
        if not condition then
            failwith message

    let private falseThat condition message =
        if condition then
            failwith message

    let private defaultCtx: ProcessContext =
        { WorkingDirectory = None
          DefaultTimeout = None }

    let ``Runner_calculateDeadline_is_exact_three_times_estimated_runtime`` () =
        let estRuntime = RuntimeSeconds 5.0
        let now = DateTimeOffset.UtcNow
        let deadline = Runner.calculateDeadline now estRuntime
        let remaining = Deadline.remaining (fun () -> now) deadline
        equal (TimeSpan.FromSeconds(15.0)) remaining

    let ``Spool_chunkBytes_splits_at_exactly_200KB_chunks`` () =
        let chunkSize = 204800 // 200 * 1024 bytes
        let totalSize = 500000
        let data = Array.zeroCreate<byte> totalSize

        for i in 0 .. totalSize - 1 do
            data.[i] <- byte (i % 256)

        let chunks = Spool.chunkBytes chunkSize data
        equal 3 chunks.Length
        equal 204800 chunks.[0].Length
        equal 204800 chunks.[1].Length
        equal 90400 chunks.[2].Length

        let reassembled = Array.concat chunks
        equal data reassembled

    let ``Runner_large_memory_gate_serializes_and_releases_on_completion_and_error`` () =
        task {
            equal 1 (Runner.getLargeGateCount ())

            use cts = new CancellationTokenSource()
            do! Runner.acquireLargeGate (cts.Token)

            try
                equal 0 (Runner.getLargeGateCount ())
            finally
                Runner.releaseLargeGate ()

            equal 1 (Runner.getLargeGateCount ())
        }

    let ``Runner_medium_memory_allows_concurrency_without_large_gate`` () =
        task {
            equal 1 (Runner.getLargeGateCount ())

            let estimate =
                { EstimatedRuntime = RuntimeSeconds 1.0
                  EstimatedOutput = OutputBytes 1024L
                  EstimatedMemory = EstimatedMemory.Medium }

            let mockLauncher =
                fun (cmd: Command) (_ct: CancellationToken) -> task { return (0, Encoding.UTF8.GetBytes("ok"), [||]) }

            let dummyCmd =
                { FileName = "echo"
                  Arguments = [ "hello" ]
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let task1 =
                Runner.executeWithLauncher mockLauncher dummyCmd estimate defaultCtx CancellationToken.None

            let task2 =
                Runner.executeWithLauncher mockLauncher dummyCmd estimate defaultCtx CancellationToken.None

            let! res1 = task1
            let! res2 = task2

            equal 1 (Runner.getLargeGateCount ())

            match res1, res2 with
            | Ok(RunnerOutcome.Completed(0, _, _, _)), Ok(RunnerOutcome.Completed(0, _, _, _)) -> ()
            | _ -> failwith "Expected both medium memory tasks to complete successfully"
        }

    let ``Runner_complete_output_preservation_spools_large_output_chunks`` () =
        task {
            let outputSize = 250000 // 250KB (> 200KB chunk size)
            let rawBytes = Array.zeroCreate<byte> outputSize

            for i in 0 .. outputSize - 1 do
                rawBytes.[i] <- byte (i % 256)

            let estimate =
                { EstimatedRuntime = RuntimeSeconds 5.0
                  EstimatedOutput = OutputBytes 500000L
                  EstimatedMemory = EstimatedMemory.Medium }

            let mockLauncher =
                fun (_cmd: Command) (_ct: CancellationToken) -> Task.FromResult(0, rawBytes, [||])

            let dummyCmd =
                { FileName = "echo"
                  Arguments = []
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! outcome = Runner.executeWithLauncher mockLauncher dummyCmd estimate defaultCtx CancellationToken.None

            match outcome with
            | Ok(RunnerOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount, chunks)) ->
                equal 0 exitCode
                equal (int64 outputSize) totalBytes
                equal 2 chunkCount
                trueThat (not (String.IsNullOrWhiteSpace spoolPath)) "Expected a spool path"
                equal outputSize (Array.concat chunks).Length
            | _ -> failwith "Expected outcome to be Spooled with 200KB chunks"
        }

    let ``Runner_timeout_kill_path_returns_TimeoutExceeded`` () =
        task {
            let estimate =
                { EstimatedRuntime = RuntimeSeconds 0.1
                  EstimatedOutput = OutputBytes 1024L
                  EstimatedMemory = EstimatedMemory.Medium }

            let hangingLauncher =
                fun (_cmd: Command) (_ct: CancellationToken) -> neverCompletes ()

            let dummyCmd =
                { FileName = "hang"
                  Arguments = []
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! outcome =
                Runner.executeWithLauncher hangingLauncher dummyCmd estimate defaultCtx CancellationToken.None

            match outcome with
            | Error(RunnerError.TimeoutExceeded span) -> equal (TimeSpan.FromSeconds(0.3)) span
            | _ -> failwith "Expected TimeoutExceeded outcome on deadline expiry"
        }

    let ``Runner_echo_executable_scenario_returns_Completed`` () =
        task {
            let cmd =
                { FileName = "echo"
                  Arguments = [ "hello world" ]
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let est =
                { EstimatedRuntime = RuntimeSeconds 2.0
                  EstimatedOutput = OutputBytes 1024L
                  EstimatedMemory = EstimatedMemory.Medium }

            let! outcome = Runner.execute cmd est defaultCtx CancellationToken.None

            match outcome with
            | Ok(RunnerOutcome.Completed(exitCode, stdout, _, _)) ->
                equal 0 exitCode
                trueThat (stdout.Contains("hello world")) "Expected echo output"
            | _ -> failwith "Expected Completed outcome from echo process execution"
        }

    let ``ExecutorSummarizer_summarizes_chunks_using_injected_port`` () =
        let port: SummarizerPort<byte[], int> =
            { MapChunk = fun bytes -> bytes.Length
              ReduceSummaries = fun list -> List.sum list }

        let chunk1 = Encoding.UTF8.GetBytes("hello ")
        let chunk2 = Encoding.UTF8.GetBytes("world")

        let res = ExecutorSummarizer.summarizeChunks port [ chunk1; chunk2 ]

        match res with
        | Ok(Some totalLen) -> equal 11 totalLen
        | _ -> failwith "Expected summarized length result"
