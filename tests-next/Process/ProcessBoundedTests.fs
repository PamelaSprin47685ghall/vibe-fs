namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
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

            let! outcome = ProcessRunner.runWithLauncher launcher cmd estimate defaultCtx CancellationToken.None

            match outcome with
            | Ok(ProcessOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount)) ->
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
    let ``Spool_readChunks_streams_in_bounded_200KB_segments_preserving_total`` () =
        task {
            // Exercises the production path ExecutorSummarize.summarizeSpool consumes:
            // Spool.readChunks must never hand back a chunk larger than 200KB and must
            // preserve the entire spool byte-for-byte.
            let payload = Array.init 500000 (fun i -> byte (i % 251))
            let spool = Spool.startStreamingSpool ()
            Spool.appendStreamingSpool spool payload

            let mutable totalRead = 0
            let mutable maxChunk = 0
            let mutable position = 0

            do!
                Spool.readChunks spool.Path (fun chunk ->
                    task {
                        maxChunk <- max maxChunk chunk.Length

                        for offset in 0 .. chunk.Length - 1 do
                            if chunk.[offset] <> byte ((position + offset) % 251) then
                                failwithf "Spool byte mismatch at %d" (position + offset)

                        position <- position + chunk.Length
                        totalRead <- totalRead + chunk.Length
                        return ()
                    })

            if totalRead <> payload.Length then
                failwithf "Expected %d bytes read, got %d" payload.Length totalRead

            trueThat (maxChunk <= Spool.ChunkSizeBytes) "Spool reader exceeded 200KB bound"

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
            let running = ProcessRunner.runWithLauncher launcher cmd estimate defaultCtx cts.Token
            cts.Cancel()
            let! outcome = running

            match outcome with
            | Error(ProcessError.ProcessCancelled reason) ->
                trueThat (reason.Contains("Cancelled")) "Expected cancellation"
            | other -> failwithf "Expected ProcessCancelled, got %A" other
        }

    [<Fact>]
    let ``ExecutorSummarize_online_reduce_is_correct_and_bounded`` () =
        task {
            let fanIn = ExecutorSummarize.ReduceFanIn
            let mutable maxBatch = 0
            let mutable maxLevel = 0

            let reduce (level: int) (batch: string list) : Task<string> =
                task {
                    maxBatch <- max maxBatch batch.Length
                    maxLevel <- max maxLevel level
                    return String.concat "\n" batch
                }

            let chunkCount = 1000
            let summaries = [ for i in 0 .. chunkCount - 1 -> sprintf "C%d" i ]
            let! result = ExecutorSummarize.reduceOnline reduce summaries

            for i in 0 .. chunkCount - 1 do
                if not (result.Contains(sprintf "C%d" i)) then
                    failwithf "Online reduce lost chunk marker C%d" i

            // Each reduce batch stays bounded by fanIn; depth stays logarithmic.
            trueThat (maxBatch <= fanIn) (sprintf "Reduce batch size %d exceeded fanIn %d" maxBatch fanIn)
            trueThat (maxLevel <= 6) (sprintf "Reduce depth %d is not logarithmic" maxLevel)
        }

    [<Fact>]
    let ``Runner_huge_deadline_runs_to_completion_without_immediate_timeout`` () =
        task {
            // 100 days estimated runtime -> 300 day budget. The JS timer ceiling is
            // ~24.8 days, so the old int cast overflowed and timed out instantly.
            let estimate =
                { EstimatedRuntime = RuntimeSeconds(100.0 * 24.0 * 3600.0)
                  EstimatedOutput = OutputBytes 1024L
                  EstimatedMemory = EstimatedMemory.Medium }

            let cmd =
                { FileName = "sh"
                  Arguments = [ "-lc"; "printf huge-deadline-ok" ]
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let! outcome = ProcessRunner.run cmd estimate defaultCtx CancellationToken.None

            match outcome with
            | Ok(ProcessOutcome.Completed(exitCode, stdout, _, _)) ->
                trueThat (exitCode = 0) "Expected exit code 0"
                trueThat (stdout.Contains("huge-deadline-ok")) "Expected command output"
            | Error(ProcessError.TimeoutExceeded _) -> failwith "Huge estimate must not time out immediately"
            | Error err -> failwithf "Expected completion, got %A" err
            | _ -> failwith "Expected Completed outcome from huge-deadline command"
        }

    [<Fact>]
    let ``ExecutorSummarize_private_mailbox_does_not_steal_manager_completions`` () =
        task {
            let runner (agentId: string) (role: AgentRole) (prompt: string option) =
                Task.FromResult(
                    AgentCompletion.ofSimpleText
                        agentId
                        ("run-" + agentId)
                        role
                        (sprintf "out:%s" (defaultArg prompt ""))
                )

            // Manager's mailbox (must NOT be starved by the summarizer).
            let managerRuntime = ForkRuntime(runner = runner)
            // Summarizer's private mailbox.
            let execIface = ExecutorSummarize.ofForkRuntime (ForkRuntime(runner = runner))

            // A foreign (Coder) completion lands on the Manager mailbox.
            managerRuntime.Fork("coder-1", AgentRole.Coder, prompt = "coder-work") |> ignore

            let payload = Array.init 600000 (fun i -> byte (i % 251))
            let spool = Spool.startStreamingSpool ()
            Spool.appendStreamingSpool spool payload

            let! _ = ExecutorSummarize.summarizeSpool execIface spool.Path

            // Manager mailbox still holds exactly the Coder completion (not stolen).
            trueThat
                (managerRuntime.PendingCompletionCount = 1)
                "Manager mailbox was starved by the executor summarizer"

            let! completion = managerRuntime.Join()

            match completion with
            | Ok c -> trueThat (c.AgentId = "coder-1") "Manager mailbox returned an unexpected completion"
            | Error _ -> failwith "Manager Join failed after summarization"

            System.IO.File.Delete(spool.Path)
        }
