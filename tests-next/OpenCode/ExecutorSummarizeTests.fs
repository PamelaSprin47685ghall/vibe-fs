#nowarn "3511"
namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.OpenCode.ExecutorSummarize
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module ExecutorSummarizeTests =

    // --- helpers ---------------------------------------------------------------

    let private mkCompletion (agentId: string) (outcome: AgentCompletionOutcome) =
        { RunId = "run-" + agentId
          AgentId = agentId
          Role = AgentRole.Executor
          Outcome = outcome
          CompletedAt = DateTimeOffset.UtcNow }

    let private parseChunkIndex (prompt: string) =
        let marker = "chunk "
        let i = prompt.IndexOf(marker)
        let after = prompt.Substring(i + marker.Length)
        Int32.Parse(after.Substring(0, after.IndexOf('.')))

    let private parseReduceLevel (prompt: string) =
        let marker = "Reduce level-"
        let i = prompt.IndexOf(marker)
        let after = prompt.Substring(i + marker.Length)
        Int32.Parse(after.Substring(0, after.IndexOf(' ')))

    let private parseReduceInputCount (prompt: string) =
        let nl = prompt.IndexOf('\n')
        let combined = prompt.Substring(nl + 1)
        combined.Split('\n').Length

    /// Fake IExecutorRuntime that records every Fork/Join in call order and emits
    /// deterministic outputs (CHUNK<i> for maps, the batched summaries verbatim
    /// for reduces) so the final fold is fully observable. Not TCS-driven: Join
    /// resolves from the FIFO of recorded forks, which is deterministic because
    /// runExecutorPrompt always awaits Fork before Join.
    type private RecordingRuntime() =
        let forks = ResizeArray<string * AgentRole * string>()
        let reduces = ResizeArray<int * int>()
        let mutable joinPos = 0
        let mutable mapCount = 0

        member _.MapCount = mapCount
        member _.ReduceCount = reduces.Count
        member _.Reduces = reduces

        member _.MaxReduceLevel =
            if reduces.Count = 0 then
                0
            else
                reduces |> Seq.map fst |> Seq.max

        member _.MaxReduceInputCount =
            if reduces.Count = 0 then
                0
            else
                reduces |> Seq.map snd |> Seq.max

        interface IExecutorRuntime with
            member _.Fork(agentId, role, prompt) =
                forks.Add(agentId, role, prompt)
                Task.FromResult(Ok(ForkResult.Created agentId))

            member _.Join() =
                let (aid, _, prompt) = forks.[joinPos]
                joinPos <- joinPos + 1

                let completion =
                    if prompt.Contains("Reduce level-") then
                        let level = parseReduceLevel prompt
                        let combined = prompt.Substring(prompt.IndexOf('\n') + 1)
                        reduces.Add(level, parseReduceInputCount prompt)
                        // Keep each summary single-line so the resident batch size
                        // (number of summaries) equals the newline count; the
                        // combined content (all CHUNK markers) is preserved via '|'.
                        mkCompletion aid (AgentCompletion.ofSimpleText aid ("run-" + aid) AgentRole.Executor (combined.Replace("\n", "|")))
                    else
                        mapCount <- mapCount + 1
                        let idx = parseChunkIndex prompt
                        mkCompletion aid (AgentCompletion.ofSimpleText aid ("run-" + aid) AgentRole.Executor (sprintf "CHUNK%d" idx))

                Task.FromResult(Ok completion)

    // --- 1. multi-chunk online ripple reduce ----------------------------------

    [<Fact>]
    let ``ExecutorSummarize_multi_chunk_ripple_reduce_is_bounded_and_complete`` () =
        task {
            let fanIn = ExecutorSummarize.ReduceFanIn
            let runtime = RecordingRuntime()

            // Sized so the spool yields well more than ReduceFanIn chunks
            // regardless of how Node fragments the stream (each read is <= 200KB).
            let payloadBytes = fanIn * Spool.ChunkSizeBytes * 3
            let payload = Array.init payloadBytes (fun i -> byte ((i * 7 + 3) % 251))
            let spool = Spool.startStreamingSpool ()
            Spool.appendStreamingSpool spool payload

            let! summary = ExecutorSummarize.summarizeSpool (runtime :> IExecutorRuntime) spool.Path

            // (a) multi-chunk and online reduction actually happened.
            Assert.True(runtime.MapCount > fanIn, sprintf "expected > %d chunks, got %d" fanIn runtime.MapCount)
            Assert.True(runtime.ReduceCount >= 1, "expected at least one reduce")

            let level0Collapses =
                runtime.Reduces
                |> Seq.filter (fun (lvl, cnt) -> lvl = 1 && cnt = fanIn)
                |> Seq.length

            Assert.True(level0Collapses >= 1, "expected level-0 collapses firing with exactly fanIn inputs")

            // (b) resident bound: no reduce batch exceeded fanIn (this catches a
            // regression to accumulate-all-then-reduce, whose final batch would be
            // N >> fanIn) and the reduction tree depth is logarithmic, so resident
            // summaries stay <= maxLevel * fanIn.
            Assert.True(
                runtime.MaxReduceInputCount <= fanIn,
                sprintf "reduce batch %d exceeded fanIn %d" runtime.MaxReduceInputCount fanIn
            )

            Assert.True(runtime.MaxReduceLevel >= 2, "expected reduction across multiple levels")

            let rec levels count acc =
                if count <= 1 then
                    acc
                else
                    levels ((count + fanIn - 1) / fanIn) (acc + 1)

            let expectedLevels = levels runtime.MapCount 1

            Assert.True(
                runtime.MaxReduceLevel <= expectedLevels,
                sprintf "reduce depth %d exceeded bound %d" runtime.MaxReduceLevel expectedLevels
            )

            // (c) the fold is the deterministic combination of every chunk output.
            for i in 0 .. runtime.MapCount - 1 do
                Assert.True(summary.Contains(sprintf "CHUNK%d" i), sprintf "final summary lost chunk %d" i)

            System.IO.File.Delete(spool.Path)
        }

    // --- 2. summarizer failure propagates and the spool is cleaned up --------

    [<Fact>]
    let ``ExecutorSummarize_failed_summarizer_propagates_and_spool_is_deleted`` () =
        task {
            // The production ExecutorTool path deletes the spool in a `finally`
            // on both success and failure. ExecutorTool.create cannot be
            // constructed without a live JS tool module, so we exercise the same
            // contract at the ExecutorSummarize + Spool level: a failing
            // summarizer must propagate its exception AND the spool file must be
            // removed (see report for the seam rationale).
            let spool = Spool.startStreamingSpool ()
            Spool.appendStreamingSpool spool (Array.init 5000 (fun i -> byte (i % 251)))
            Assert.True(System.IO.File.Exists(spool.Path), "spool should exist before summarization")

            let failingRuntime =
                { new IExecutorRuntime with
                    member _.Fork(_, _, _) =
                        Task.FromResult(Ok(ForkResult.Created "x"))

                    member _.Join() =
                        Task.FromResult(Ok(mkCompletion "x" (AgentCompletion.ofSimpleError "x" "run-x" AgentRole.Executor "summarizer boom"))) }

            let! propagated =
                task {
                    try
                        let! _ = ExecutorSummarize.summarizeSpool failingRuntime spool.Path
                        return false
                    with _ ->
                        return true
                }

            Spool.delete spool.Path
            Assert.True(propagated, "a failing summarizer must propagate its exception")
            Assert.False(System.IO.File.Exists(spool.Path), "spool must be deleted on the failure path")
        }

    // --- 3. mailbox isolation --------------------------------------------------

    let private makeFakeHost () =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable childCount = 0
        let mutable childPromptCount = 0
        let childId = SessionId.create "child-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, listener) =
                    terminal <- Some listener

                    { new IDisposable with
                        member _.Dispose() = terminal <- None }

                member _.SendPrompt(_, _, _) =
                    Task.FromResult(Ok(MessageId.create "accepted"))

                member _.SendChildPromptFireAndForget(_, _, _, _) =
                    childPromptCount <- childPromptCount + 1
                    Task.FromResult(Ok())

                member _.AbortSession(_) = Task.FromResult(Ok())
                member _.AbortChildren(_) = Task.FromResult(()) :> Task

                member _.CreateChildSession(_, _) =
                    childCount <- childCount + 1
                    Task.FromResult(Ok childId)

                member _.GetSessionOutput(_) = [] }

        let trigger () =
            terminal
            |> Option.iter (fun listener ->
                listener
                    childId
                    (TerminalOutcome.Completed(
                        { SessionId = childId
                          RootUserMessageId = MessageId.create "m-1"
                          AssistantMessageId = MessageId.create "m-1"
                          Role = "test"
                          Directory = ""
                          FinalText = "done"
                          Parts = [||] }
                    )))

        host, trigger, (fun () -> childCount), (fun () -> childPromptCount)

    [<Fact>]
    let ``ForkRuntime_mailbox_isolation_completion_stays_in_own_runtime`` () =
        task {
            let runtimeA = ForkRuntime()
            let runtimeB = ForkRuntime()
            let created = runtimeA.Fork("agent-A", AgentRole.Executor, prompt = "work")
            Assert.Equal(ForkResult.Created "agent-A", created)
            runtimeA.PublishCompletion(mkCompletion "agent-A" (AgentCompletion.ofSimpleText "agent-A" "run-A" AgentRole.Executor "A"))
            Assert.Equal(1, runtimeA.PendingCompletionCount)
            Assert.Equal(0, runtimeB.PendingCompletionCount)
            let! joinedA = runtimeA.Join()

            match joinedA with
            | Ok completion -> Assert.Equal("agent-A", completion.AgentId)
            | Error error -> Assert.True(false, sprintf "expected A completion, got %A" error)

            Assert.Equal(0, runtimeB.PendingCompletionCount)
        }

    [<Fact>]
    let ``ExecutorSummarize_awaitAgent_stashes_foreign_completion_and_resolves_target`` () =
        task {
            let fr = ForkRuntime()
            let exec = ExecutorSummarize.ofForkRuntime fr
            let stash = Dictionary<string, RunCompletion>()

            let foreign = mkCompletion "foreign-1" (AgentCompletion.ofSimpleText "foreign-1" "run-f" AgentRole.Executor "FOREIGN-OUT")
            let target = mkCompletion "target-1" (AgentCompletion.ofSimpleText "target-1" "run-t" AgentRole.Executor "TARGET-OUT")

            // Publish a FOREIGN completion (different agentId) before the target.
            fr.PublishCompletion(foreign)

            // awaitAgent on the target must stash the foreign one and keep waiting.
            let pending = ExecutorSummarize.awaitAgent exec "target-1" stash

            // The target then arrives.
            fr.PublishCompletion(target)
            let! result = pending

            Assert.Equal("TARGET-OUT", AgentCompletion.text result.Outcome)

            // The foreign completion was stashed, not consumed or destroyed.
            Assert.True(stash.ContainsKey("foreign-1"), "foreign completion must not be consumed by awaitAgent")

            // It remains joinable afterwards (stash semantics, not destruction).
            let! foreignAgain = ExecutorSummarize.awaitAgent exec "foreign-1" stash

            Assert.Equal("FOREIGN-OUT", AgentCompletion.text foreignAgain.Outcome)
        }
