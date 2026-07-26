namespace Wanxiangshu.Next.Process

open System
open System.Text
open System.Threading.Tasks

type SummarizerPort<'Chunk, 'Summary> =
    { MapChunk: 'Chunk -> 'Summary
      ReduceSummaries: 'Summary list -> 'Summary }

module ExecutorSummarizer =

    [<Literal>]
    let private AgentPortUnavailable =
        "Executor Agent map/reduce port is not implemented"

    let textSummaryPort: SummarizerPort<byte[], string> =
        { MapChunk = fun (_: byte[]) -> raise (InvalidOperationException AgentPortUnavailable)
          ReduceSummaries = fun (_: string list) -> raise (InvalidOperationException AgentPortUnavailable) }

    let summarizeChunks
        (port: SummarizerPort<'Chunk, 'Summary>)
        (chunks: 'Chunk list)
        : Result<'Summary option, string> =
        try
            if List.isEmpty chunks then
                Ok None
            else
                let mapped = chunks |> List.map port.MapChunk
                Ok(Some(port.ReduceSummaries mapped))
        with ex ->
            Error ex.Message

    let private summarizeMapped
        (port: SummarizerPort<byte[], 'Summary>)
        (mapped: ResizeArray<'Summary>)
        : Result<'Summary option, string> =
        if mapped.Count = 0 then
            Ok None
        else
            Ok(Some(port.ReduceSummaries(mapped |> Seq.toList)))

    /// Reads the spool incrementally, maps one chunk at a time, then reduces summaries.
    let summarizeSpool
        (port: SummarizerPort<byte[], 'Summary>)
        (spoolPath: string)
        : Task<Result<'Summary option, string>> =
        task {
            try
                let mapped = ResizeArray<'Summary>()

                do!
                    Spool.readChunks spoolPath (fun chunk ->
                        task {
                            mapped.Add(port.MapChunk chunk)
                            return ()
                        })

                return summarizeMapped port mapped
            with ex ->
                return Error ex.Message
        }

    /// Async outcome entry point; spooled outcomes never materialize all chunks.
    let summarizeOutcomeAsync
        (port: SummarizerPort<byte[], 'Summary>)
        (outcome: RunnerOutcome)
        : Task<Result<'Summary option, string>> =
        task {
            match outcome with
            | RunnerOutcome.Completed(_, stdout, stderr, _) ->
                let combined = Encoding.UTF8.GetBytes(stdout + stderr)
                return summarizeChunks port [ combined ]
            | RunnerOutcome.Spooled(_, spoolPath, _, _) -> return! summarizeSpool port spoolPath
            | RunnerOutcome.OutputExceeded(_, _) ->
                return Error "Cannot summarize output after the output stream exceeded its limit"
        }

    /// Synchronous compatibility entry point using bounded file reads for spooled output.
    let summarizeOutcome
        (port: SummarizerPort<byte[], 'Summary>)
        (outcome: RunnerOutcome)
        : Result<'Summary option, string> =
        try
            match outcome with
            | RunnerOutcome.Completed(_, stdout, stderr, _) ->
                let combined = Encoding.UTF8.GetBytes(stdout + stderr)
                summarizeChunks port [ combined ]
            | RunnerOutcome.Spooled(_, spoolPath, _, _) ->
                let mapped = ResizeArray<'Summary>()
                Spool.readChunksSync spoolPath (fun chunk -> mapped.Add(port.MapChunk chunk))
                summarizeMapped port mapped
            | RunnerOutcome.OutputExceeded(_, _) ->
                Error "Cannot summarize output after the output stream exceeded its limit"
        with ex ->
            Error ex.Message

    let summarizeWithExecutorAgent (outcome: RunnerOutcome) : Result<string option, string> =
        summarizeOutcome textSummaryPort outcome
