namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Process
open Xunit

module ProcessBoundedTests =
    let private trueThat condition message =
        if not condition then
            failwith message

    let private defaultCtx: ProcessContext =
        { WorkingDirectory = None
          DefaultTimeout = None }

    [<Fact>]
    let ``Runner_spooled_outcome_has_only_metadata_and_complete_bounded_file`` () =
        task {
            let outputSize = 250000
            let rawBytes = Array.init outputSize (fun i -> byte (i % 256))

            let estimate =
                { EstimatedRuntime = RuntimeSeconds 5.0
                  EstimatedOutput = OutputBytes 50000L
                  EstimatedMemory = EstimatedMemory.Medium }

            let launcher =
                fun (_cmd: Command) (_ct: CancellationToken) -> Task.FromResult(0, rawBytes, [||])

            let cmd =
                { FileName = "echo"
                  Arguments = []
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! outcome = Runner.executeWithLauncher launcher cmd estimate defaultCtx CancellationToken.None

            match outcome with
            | Ok(RunnerOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount)) ->
                if exitCode <> 0 then
                    failwithf "Expected exit code 0, got %d" exitCode

                if totalBytes <> int64 outputSize then
                    failwithf "Expected %d bytes, got %d" outputSize totalBytes

                if chunkCount <> 2 then
                    failwithf "Expected 2 chunks, got %d" chunkCount

                trueThat (not (String.IsNullOrWhiteSpace spoolPath)) "Expected a spool path"
                let mutable bytesRead = 0
                let mutable maxChunk = 0
                let mutable position = 0

                Spool.readChunksSync spoolPath (fun chunk ->
                    maxChunk <- max maxChunk chunk.Length

                    for offset in 0 .. chunk.Length - 1 do
                        if chunk.[offset] <> byte ((position + offset) % 256) then
                            failwithf "Spool byte mismatch at %d" (position + offset)

                    position <- position + chunk.Length
                    bytesRead <- bytesRead + chunk.Length)

                if bytesRead <> outputSize then
                    failwithf "Expected %d bytes read, got %d" outputSize bytesRead

                if maxChunk > Spool.ChunkSizeBytes then
                    failwith "Spool reader exceeded 200KB"

                System.IO.File.Delete(spoolPath)
            | _ -> failwith "Expected bounded Spooled outcome"
        }

    [<Fact>]
    let ``ExecutorSummarizer_streams_spool_chunks_with_bounded_reads`` () =
        task {
            let payload = Array.init 500000 (fun i -> byte (i % 251))
            let spool = Spool.startStreamingSpool ()
            Spool.appendStreamingSpool spool payload
            let mutable maxChunk = 0

            let port: SummarizerPort<byte[], int> =
                { MapChunk =
                    fun bytes ->
                        maxChunk <- max maxChunk bytes.Length
                        bytes.Length
                  ReduceSummaries = fun list -> List.sum list }

            let! result = ExecutorSummarizer.summarizeSpool port spool.Path

            match result with
            | Ok(Some total) ->
                if total <> payload.Length then
                    failwithf "Expected %d mapped bytes, got %d" payload.Length total

                trueThat (maxChunk <= Spool.ChunkSizeBytes) "Spool reader exceeded 200KB bound"
            | _ -> failwith "Expected streamed spool summary"

            System.IO.File.Delete(spool.Path)
        }

    [<Fact>]
    let ``Runner_launcher_cancellation_propagates_without_second_timeout`` () =
        task {
            let estimate =
                { EstimatedRuntime = RuntimeSeconds 10.0
                  EstimatedOutput = OutputBytes 1024L
                  EstimatedMemory = EstimatedMemory.Medium }

            let launcher =
                fun (_cmd: Command) (token: CancellationToken) ->
                    let completion = TaskCompletionSource<int * byte[] * byte[]>()
                    token.Register(fun () -> completion.SetResult(0, [||], [||])) |> ignore
                    completion.Task

            let cmd =
                { FileName = "stdin-hang"
                  Arguments = []
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            use cts = new CancellationTokenSource()
            let running = Runner.executeWithLauncher launcher cmd estimate defaultCtx cts.Token
            cts.Cancel()
            let! outcome = running

            match outcome with
            | Error(RunnerError.ProcessCancelled reason) ->
                trueThat (reason.Contains("Cancelled")) "Expected cancellation"
            | other -> failwithf "Expected ProcessCancelled, got %A" other
        }
