namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System.Collections.Generic
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.OpenCode.ExecutorSummarize
open Wanxiangshu.Next.Session

module ExecutorMailboxOwnershipTests =

    [<Fact>]
    let ``ExecutorSummarize_awaitAgent_stashes_owned_sibling_completion_and_resolves_target`` () =
        task {
            let completion agentId text =
                { RunId = "run-" + agentId
                  AgentId = agentId
                  AgentName = "fast-executor"
                  Role = AgentRole.Executor
                  Outcome = AgentCompletion.ofSimpleText agentId ("run-" + agentId) AgentRole.Executor text
                  CompletedAt = System.DateTimeOffset.UtcNow }

            let completions = Queue<RunCompletion>()
            completions.Enqueue(completion "sibling-1" "SIBLING-OUT")
            completions.Enqueue(completion "target-1" "TARGET-OUT")

            // Executor's private runtime contains only its own forked children.
            // Model that owned mailbox directly so awaitAgent's stash behavior is
            // deterministic without injecting a foreign ForkRuntime completion.
            let runtime =
                { new IExecutorRuntime with
                    member _.Fork(_, _, _) = Task.FromResult(Error "not used")

                    member _.Join() =
                        if completions.Count = 0 then
                            Task.FromResult(Error ForkError.NothingToJoin)
                        else
                            Task.FromResult(Ok(completions.Dequeue())) }

            let stash = Dictionary<string, RunCompletion>()
            let! result = ExecutorSummarize.awaitAgent runtime "target-1" stash

            Assert.Equal("TARGET-OUT", AgentCompletion.text result.Outcome)
            Assert.True(stash.ContainsKey("sibling-1"), "owned sibling completion must be stashed")

            let! sibling = ExecutorSummarize.awaitAgent runtime "sibling-1" stash
            Assert.Equal("SIBLING-OUT", AgentCompletion.text sibling.Outcome)
        }
