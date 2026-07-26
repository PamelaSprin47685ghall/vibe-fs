namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// Bounded hierarchical map/reduce for spooled Executor output.
module ExecutorSummarize =

    [<Literal>]
    let ReduceFanIn = 8

    /// Minimal runtime surface the summarizer needs: fork an Executor agent and
    /// await one completion. Decoupled from HostForkRuntime so the summarizer can
    /// run on a PRIVATE mailbox (never the Manager's Join) — bug #1.
    type IExecutorRuntime =
        abstract Fork: string * AgentRole * string -> Task<Result<ForkResult, string>>
        abstract Join: unit -> Task<Result<RunCompletion, ForkError>>

    /// Wrap a HostForkRuntime as the summarizer's private mailbox (production path).
    let asExecutorRuntime (runtime: HostForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt) = runtime.Fork(agentId, role, prompt)
            member _.Join() = runtime.Join() }

    /// Wrap a bare ForkRuntime as the summarizer mailbox (tests / fakes).
    let ofForkRuntime (runtime: ForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt) =
                task { return Ok(runtime.Fork(agentId, role, prompt = prompt)) }

            member _.Join() = runtime.Join() }

    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let private newAgentId () : string = jsNative

    let private completionText (completion: RunCompletion) =
        match completion.Outcome with
        | Ok text -> text
        | Error error -> raise (InvalidOperationException error)

    /// Join until the target Executor completes; stash foreign (other Executor)
    /// completions so a concurrent reduce cannot be mistaken for a map result.
    /// Only sees completions on THIS private mailbox, so the Manager mailbox is
    /// never starved.
    let awaitAgent (runtime: IExecutorRuntime) (agentId: string) (stash: Dictionary<string, RunCompletion>) =
        task {
            match stash.TryGetValue agentId with
            | true, completion ->
                stash.Remove agentId |> ignore
                return completion
            | false, _ ->
                let mutable result: RunCompletion = Unchecked.defaultof<_>
                let mutable done' = false

                while not done' do
                    let! joined = runtime.Join()

                    match joined with
                    | Error error ->
                        result <- raise (InvalidOperationException(error.ToString()))
                        done' <- true
                    | Ok completion when completion.AgentId = agentId ->
                        result <- completion
                        done' <- true
                    | Ok completion -> stash.[completion.AgentId] <- completion

                return result
        }

    let runExecutorPrompt (runtime: IExecutorRuntime) (stash: Dictionary<string, RunCompletion>) (prompt: string) =
        task {
            let agentId = newAgentId ()
            let! fork = runtime.Fork(agentId, AgentRole.Executor, prompt)

            match fork with
            | Error error -> return raise (InvalidOperationException error)
            | Ok result ->
                let! completion = awaitAgent runtime result.AgentId stash
                return completionText completion
        }

    let summarizeChunk
        (runtime: IExecutorRuntime)
        (stash: Dictionary<string, RunCompletion>)
        (chunk: byte[])
        (index: int)
        =
        let content = Encoding.UTF8.GetString chunk

        let prompt =
            sprintf
                "Summarize command output chunk %d. Preserve errors, decisions, paths, and exact numbers; omit raw code.\n%s"
                index
                content

        runExecutorPrompt runtime stash prompt

    let reduceBatch
        (runtime: IExecutorRuntime)
        (stash: Dictionary<string, RunCompletion>)
        (level: int)
        (batch: string list)
        =
        let combined = String.concat "\n" batch

        let prompt =
            sprintf
                "Reduce level-%d command-output summaries into one dense report. Preserve failures and exact facts; do not include raw code.\n%s"
                level
                combined

        runExecutorPrompt runtime stash prompt

    /// Online carry/ripple reduce: summaries fill level 0 up to ReduceFanIn-1; at
    /// fanIn they collapse immediately into one summary pushed to level 1, and so
    /// on. Memory stays O(fanIn * log chunks) — never holds all summaries at once.
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

    /// Fold remaining level arrays bottom-up into a single summary.
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

    /// Testable core: reduce an already-materialized summary list online.
    let reduceOnline (reduceBatch: int -> string list -> Task<string>) (summaries: string list) : Task<string> =
        task {
            let levels = ResizeArray<ResizeArray<string>>()

            for summary in summaries do
                do! rippleInsert reduceBatch levels summary

            return! foldLevels reduceBatch levels
        }

    /// Maps each bounded spool chunk sequentially, reducing online so memory stays
    /// bounded; never holds more than ReduceFanIn * log(chunks) summaries.
    let summarizeSpool (runtime: IExecutorRuntime) (spoolPath: string) =
        task {
            let stash = Dictionary<string, RunCompletion>()
            let levels = ResizeArray<ResizeArray<string>>()
            let mutable index = 0

            do!
                Spool.readChunks spoolPath (fun chunk ->
                    task {
                        let current = index
                        index <- index + 1
                        let! summary = summarizeChunk runtime stash chunk current
                        do! rippleInsert (reduceBatch runtime stash) levels summary
                        return ()
                    })

            return! foldLevels (reduceBatch runtime stash) levels
        }
