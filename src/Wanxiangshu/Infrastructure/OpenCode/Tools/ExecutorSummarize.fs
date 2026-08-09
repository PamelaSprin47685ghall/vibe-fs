namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Process
open Wanxiangshu.Session
open Wanxiangshu.Domain

/// Bounded hierarchical map/reduce for spooled Executor output.
module ExecutorSummarize =

    [<Literal>]
    let ReduceFanIn = 8

    type IExecutorRuntime = ExecutorSummarizeRuntime.IExecutorRuntime

    let asExecutorRuntime = ExecutorSummarizeRuntime.asExecutorRuntime
    let ofForkRuntime = ExecutorSummarizeRuntime.ofForkRuntime

    let summarizeChunkPrompt (index: int) : string =
        sprintf
            "Summarize command output chunk %d. Preserve errors, decisions, paths, and exact numbers; omit raw code."
            index

    let reduceBatchPrompt (level: int) : string =
        sprintf
            "Reduce level-%d command-output summaries into one dense report. Preserve failures and exact facts; do not include raw code."
            level

    let private agentId (processId: string) (level: int) (startChunk: int) (endChunk: int) =
        sprintf "exec-%s" (HostDigest.sha256Hex (sprintf "%s|%d|%d|%d" processId level startChunk endChunk))

    let private completionText (completion: RunCompletion) =
        match completion.Outcome with
        | AgentCompleted payload when not (String.IsNullOrWhiteSpace payload.WorkRecord) -> payload.WorkRecord
        | AgentCompleted _ -> raise (InvalidOperationException "completed with empty work record")
        | AgentFailed payload -> raise (InvalidOperationException payload.Message)
        | AgentAbandoned(_, reason) -> raise (InvalidOperationException reason)

    /// Per-chunk join budget. Prevents a single hung summarizer from blocking the
    /// whole map/reduce forever (Part 1: unblock; Part 2 owns richer partial text).
    [<Literal>]
    let AwaitAgentTimeoutMs = 600_000

    let private awaitJournalAdvanceOrDeadline (changed: Task<JournalChange>) (remainingMs: int) : Task<bool> =
        emitJsExpr
            (changed, PtyTiming.timerTask remainingMs)
            "Promise.race([$0.then(function () { return true; }), $1.then(function () { return false; })])"

    /// Permit-gated targeted await (Journal-authoritative via HostForkRuntime).
    /// FamilyWaiting → ForkError.TimedOut: wait for a journal advance within one deadline.
    /// FamilyBlocked / real join timeout → ForkError.NotFound: hard fail, no retry.
    let awaitAgentWithPermit (runtime: IExecutorRuntime) (agentId: string) =
        let deadline = DateTimeOffset.UtcNow.AddMilliseconds(float AwaitAgentTimeoutMs)
        let deadlineExpired (): RunCompletion =
            raise (
                InvalidOperationException(
                    sprintf "awaitAgent timed out for %s after %d ms" agentId AwaitAgentTimeoutMs
                )
            )

        let rec loop (): Task<RunCompletion> =
            task {
                let remainingMs = int (deadline - DateTimeOffset.UtcNow).TotalMilliseconds

                if remainingMs <= 0 then
                    return raise (
                        InvalidOperationException(
                            sprintf "awaitAgent timed out for %s after %d ms" agentId AwaitAgentTimeoutMs
                        )
                    )
                else
                    let fromRevision = runtime.CurrentJournalRevision()
                    let! joined = runtime.AwaitAgentWithPermit(agentId, Some remainingMs)

                    match joined with
                    | Ok completion -> return completion
                    | Error ForkError.TimedOut ->
                        let remainingMs = int (deadline - DateTimeOffset.UtcNow).TotalMilliseconds

                        if remainingMs <= 0 then
                            return raise (
                                InvalidOperationException(
                                    sprintf "awaitAgent timed out for %s after %d ms" agentId AwaitAgentTimeoutMs
                                )
                            )
                        else
                            let changed = runtime.AwaitJournalChangeFrom fromRevision
                            let! journalAdvanced = awaitJournalAdvanceOrDeadline changed remainingMs

                            if journalAdvanced then
                                return! loop ()
                            else
                                return raise (
                                    InvalidOperationException(
                                        sprintf "awaitAgent timed out for %s after %d ms" agentId AwaitAgentTimeoutMs
                                    )
                                )
                    | Error error -> return raise (InvalidOperationException(error.ToString()))
            }

        loop ()

    let runExecutorPrompt
        (runtime: IExecutorRuntime)
        (forkedIds: ResizeArray<string>)
        (processId: string)
        (level: int)
        (startChunk: int)
        (endChunk: int)
        (prompt: string)
        (payload: string option)
        =
        task {
            let id = agentId processId level startChunk endChunk
            // Track before fork so sibling cancel covers in-flight map/reduce agents.
            forkedIds.Add id
            let! fork = runtime.Fork(id, Role.Executor, prompt, payload)

            match fork with
            | Error error -> return raise (InvalidOperationException error)
            | Ok result ->
                let! completion = awaitAgentWithPermit runtime result.AgentId
                return completionText completion
        }

    let summarizeChunk
        (runtime: IExecutorRuntime)
        (forkedIds: ResizeArray<string>)
        (spoolPath: string)
        (chunk: byte[])
        (index: int)
        =
        let content = Encoding.UTF8.GetString chunk

        let prompt = summarizeChunkPrompt index

        let rootDigest = HostDigest.sha256Hex (sprintf "%s|%d" spoolPath index)

        runExecutorPrompt runtime forkedIds rootDigest 0 index index prompt (Some content)

    let reduceBatch (runtime: IExecutorRuntime) (forkedIds: ResizeArray<string>) (level: int) (batch: string list) =
        let combined = String.concat "\n" batch

        let prompt = reduceBatchPrompt level

        let batchDigest = HostDigest.sha256Hex (String.concat "\n" batch)

        runExecutorPrompt runtime forkedIds batchDigest level 0 (List.length batch - 1) prompt (Some combined)

    let private rippleInsert
        (reduceBatch: int -> string list -> Task<string>)
        (levels: ResizeArray<ResizeArray<string>>)
        (summary: string)
        =
        task {
            if levels.Count = 0 then
                levels.Add(ResizeArray())

            levels.[0].Add summary
            let mutable lvl = 0

            while levels.[lvl].Count >= ReduceFanIn do
                let batch = levels.[lvl] |> Seq.toList
                levels.[lvl].Clear()
                let! reduced = reduceBatch (lvl + 1) batch

                if levels.Count <= lvl + 1 then
                    levels.Add(ResizeArray())

                levels.[lvl + 1].Add reduced
                lvl <- lvl + 1
        }

    let private foldLevels
        (reduceBatch: int -> string list -> Task<string>)
        (levels: ResizeArray<ResizeArray<string>>)
        =
        task {
            if levels.Count = 0 then
                return ""
            else
                let mutable carry: string list = []

                for i in 0 .. levels.Count - 1 do
                    let batch = carry @ (levels.[i] |> Seq.toList)

                    match batch with
                    | [] -> ()
                    | [ single ] when i = levels.Count - 1 -> carry <- [ single ]
                    | _ ->
                        let! reduced = reduceBatch (i + 1) batch
                        carry <- [ reduced ]

                match carry with
                | [ single ] -> return single
                | _ -> return ""
        }

    let reduceOnline (reduceBatch: int -> string list -> Task<string>) (summaries: string list) : Task<string> =
        task {
            let levels = ResizeArray<ResizeArray<string>>()

            for summary in summaries do
                do! rippleInsert reduceBatch levels summary

            return! foldLevels reduceBatch levels
        }

    /// Maps bounded spool chunks concurrently (results sorted by chunk index),
    /// then reduces online. Map/reduce failures yield partial summary plus the
    /// last 200KB raw tail instead of dropping ProcessResult.
    /// Agent waits go through IExecutorRuntime.AwaitAgentWithPermit (fresh permit each wait).
    let summarizeSpool (runtime: IExecutorRuntime) (spoolPath: string) =
        task {
            let forkedIds = ResizeArray<string>()

            let cancelOwned () =
                for id in forkedIds do
                    runtime.CancelAgent id

            let chunks = ResizeArray<int * byte[]>()
            let mutable index = 0

            do!
                Spool.readChunks spoolPath (fun chunk ->
                    task {
                        chunks.Add(index, Array.copy chunk)
                        index <- index + 1
                    })

            // Start all map tasks first (concurrent), then await in index order.
            let mapTasks =
                [| for (chunkIndex, chunk) in chunks do
                       task {
                           try
                               let! summary = summarizeChunk runtime forkedIds spoolPath chunk chunkIndex
                               return Choice1Of2(chunkIndex, summary)
                           with ex ->
                               return Choice2Of2(chunkIndex, chunk, ex.Message)
                       } |]

            let mapped = Array.zeroCreate mapTasks.Length

            for i in 0 .. mapTasks.Length - 1 do
                let! result = mapTasks.[i]
                mapped.[i] <- result

                match result with
                | Choice2Of2 _ -> cancelOwned ()
                | Choice1Of2 _ -> ()

            let successes =
                mapped
                |> Array.choose (function
                    | Choice1Of2 value -> Some value
                    | Choice2Of2 _ -> None)
                |> Array.sortBy fst

            let failures =
                mapped
                |> Array.choose (function
                    | Choice2Of2 value -> Some value
                    | Choice1Of2 _ -> None)

            let levels = ResizeArray<ResizeArray<string>>()
            let reduce = reduceBatch runtime forkedIds

            try
                for _, summary in successes do
                    do! rippleInsert reduce levels summary

                let! reduced = foldLevels reduce levels

                if failures.Length = 0 then
                    return reduced
                else
                    cancelOwned ()

                    let lastChunk =
                        if chunks.Count = 0 then
                            [||]
                        else
                            snd chunks.[chunks.Count - 1]

                    let rawTail = Encoding.UTF8.GetString lastChunk

                    if String.IsNullOrWhiteSpace reduced then
                        return sprintf "partial summary unavailable\n--- raw tail ---\n%s" rawTail
                    else
                        return
                            sprintf "%s\n--- partial: map/reduce incomplete ---\n--- raw tail ---\n%s" reduced rawTail
            with ex ->
                cancelOwned ()

                let lastChunk =
                    if chunks.Count = 0 then
                        [||]
                    else
                        snd chunks.[chunks.Count - 1]

                let rawTail = Encoding.UTF8.GetString lastChunk

                return sprintf "partial summary (reduce failed: %s)\n--- raw tail ---\n%s" ex.Message rawTail
        }
