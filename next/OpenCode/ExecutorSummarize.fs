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

    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let private newAgentId () : string = jsNative

    let private completionText (completion: RunCompletion) =
        match completion.Outcome with
        | Ok text -> text
        | Error error -> raise (InvalidOperationException error)

    /// Join until the target Executor completes; stash foreign completions so a
    /// concurrent Coder/Reviewer/PTY cannot be mistaken for a map/reduce result.
    let awaitAgent (runtime: HostForkRuntime) (agentId: string) (stash: Dictionary<string, RunCompletion>) =
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

    let runExecutorPrompt (runtime: HostForkRuntime) (stash: Dictionary<string, RunCompletion>) (prompt: string) =
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
        (runtime: HostForkRuntime)
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
        (runtime: HostForkRuntime)
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

    /// Fixed fan-in hierarchical reduce: every level holds at most ReduceFanIn
    /// summaries so the reduce prompt stays bounded.
    let reduceHierarchical
        (runtime: HostForkRuntime)
        (stash: Dictionary<string, RunCompletion>)
        (summaries: ResizeArray<string>)
        =
        task {
            if summaries.Count = 0 then
                return ""
            elif summaries.Count = 1 then
                return summaries.[0]
            else
                let mutable level = 0
                let mutable current = summaries |> Seq.toList

                while current.Length > 1 do
                    level <- level + 1
                    let next = ResizeArray<string>()
                    let mutable offset = 0

                    while offset < current.Length do
                        let take = min ReduceFanIn (current.Length - offset)
                        let batch = current |> List.skip offset |> List.take take
                        offset <- offset + take

                        if batch.Length = 1 then
                            next.Add batch.Head
                        else
                            let! reduced = reduceBatch runtime stash level batch
                            next.Add reduced

                    current <- next |> Seq.toList

                return current.Head
        }

    /// Maps each bounded spool chunk sequentially, then hierarchically reduces.
    let summarizeSpool (runtime: HostForkRuntime) (spoolPath: string) =
        task {
            let stash = Dictionary<string, RunCompletion>()
            let summaries = ResizeArray<string>()
            let mutable index = 0

            do!
                Spool.readChunks spoolPath (fun chunk ->
                    task {
                        let current = index
                        index <- index + 1
                        let! summary = summarizeChunk runtime stash chunk current
                        summaries.Add summary
                        return ()
                    })

            return! reduceHierarchical runtime stash summaries
        }
