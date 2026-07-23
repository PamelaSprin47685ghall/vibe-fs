namespace Wanxiangshu.Next.Process

open System
open System.Text

type SummarizerPort<'Chunk, 'Summary> =
    { MapChunk: 'Chunk -> 'Summary
      ReduceSummaries: 'Summary list -> 'Summary }

module ExecutorSummarizer =

    /// Pure default text summarizer port combining string chunks.
    let textSummaryPort: SummarizerPort<byte[], string> =
        { MapChunk = fun (bytes: byte[]) -> Encoding.UTF8.GetString(bytes)
          ReduceSummaries = fun (summaries: string list) -> String.Concat(summaries) }

    /// Summarizes a sequence of byte chunks using an injected map/reduce summarizer port.
    let summarizeChunks
        (port: SummarizerPort<'Chunk, 'Summary>)
        (chunks: 'Chunk list)
        : Result<'Summary option, string> =
        try
            if List.isEmpty chunks then
                Ok None
            else
                let mapped = chunks |> List.map port.MapChunk
                let reduced = port.ReduceSummaries mapped
                Ok(Some reduced)
        with ex ->
            Error ex.Message

    /// Summarizes process output / spooled chunks using the injected map/reduce summarizer port.
    let summarizeOutcome
        (port: SummarizerPort<byte[], 'Summary>)
        (outcome: RunnerOutcome)
        : Result<'Summary option, string> =
        match outcome with
        | RunnerOutcome.Completed(_, stdout, stderr, _) ->
            let combined = Encoding.UTF8.GetBytes(stdout + stderr)
            summarizeChunks port [ combined ]
        | RunnerOutcome.Spooled(_, _, _, _, chunks) -> summarizeChunks port (Array.toList chunks)
        | RunnerOutcome.OutputExceeded(_, _) -> Ok None
