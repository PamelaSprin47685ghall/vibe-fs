namespace Wanxiangshu.Next.Process

open System
open System.Text

type SummarizerPort<'Chunk, 'Summary> =
    { MapChunk: 'Chunk -> 'Summary
      ReduceSummaries: 'Summary list -> 'Summary }

module ExecutorSummarizer =

    [<Literal>]
    let private AgentPortUnavailable =
        "Executor Agent map/reduce port is not implemented"

    /// Port reserved for the real Executor Agent integration.
    /// It deliberately fails instead of presenting concatenated bytes as a summary.
    let textSummaryPort: SummarizerPort<byte[], string> =
        { MapChunk = fun (_: byte[]) -> raise (InvalidOperationException AgentPortUnavailable)
          ReduceSummaries = fun (_: string list) -> raise (InvalidOperationException AgentPortUnavailable) }

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
        | RunnerOutcome.OutputExceeded(_, _) ->
            Error "Cannot summarize output after the output stream exceeded its limit"

    /// Default entry point until an Executor Agent is wired into this layer.
    let summarizeWithExecutorAgent (outcome: RunnerOutcome) : Result<string option, string> =
        summarizeOutcome textSummaryPort outcome
