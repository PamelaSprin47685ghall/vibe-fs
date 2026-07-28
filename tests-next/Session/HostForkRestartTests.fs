namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Xunit

module HostForkRestartTests =

    let private msg id role finish text : SessionMessage =
        { Id = MessageId.create id
          Role = role
          Agent = None
          Finish = finish
          ErrorName = None
          Model = None
          Parts =
            [| box (
                   {| ``type`` = "text"
                      text = text |}
               ) |]
          Raw = null }

    let private snapshotOf (messages: Map<string, SessionMessage list>) =
        { new ISessionSnapshotPort with
            member _.GetMessages(sessionId) =
                task {
                    match messages.TryFind(SessionId.value sessionId) with
                    | Some ms -> return Ok ms
                    | None -> return Error "missing"
                } }

    [<Fact>]
    let ``Recover_completed_child_publishes_pending_join`` () =
        task {
            let runtime = ForkRuntime()
            let childId = SessionId.create "child-1"
            let agentId = "a1b2c3"

            let snapshot =
                snapshotOf (
                    Map.ofList
                        [ SessionId.value childId,
                          [ msg "u1" "user" None "do work"
                            msg "a1" "assistant" (Some "stop") "A version complete" ] ]
                )

            do! HostForkRestart.recoverChild runtime (Some snapshot) agentId childId AgentRole.Coder
            let! joined = runtime.Join()

            match joined with
            | Ok completion ->
                Assert.Equal(agentId, completion.AgentId)
                Assert.Equal("A version complete", AgentCompletion.text completion.Outcome)
            | Error e -> Assert.True(false, sprintf "expected completion, got %A" e)

            // Before join, list may still show pending; after join the mailbox is empty.
            Assert.equal(0, runtime.PendingCompletionCount)
        }

    [<Fact>]
    let ``Recover_nonterminal_child_is_interrupted`` () =
        task {
            let runtime = ForkRuntime()
            let childId = SessionId.create "child-2"
            let agentId = "d4e5f6"

            let snapshot =
                snapshotOf (
                    Map.ofList
                        [ SessionId.value childId,
                          [ msg "u1" "user" None "do work"
                            msg "a1" "assistant" (Some "tool-calls") "still working" ] ]
                )

            do! HostForkRestart.recoverChild runtime (Some snapshot) agentId childId AgentRole.Inspector
            let agents, _ = runtime.List()
            let record = agents |> List.find (fun a -> a.AgentId = agentId)
            Assert.equal(AgentStatus.Interrupted, record.Status)
            Assert.True(
                record.LastCompletionStatus
                |> Option.exists (fun s -> s.StartsWith("interrupted:")),
                sprintf "expected interrupted status, got %A" record.LastCompletionStatus
            )
            Assert.equal(0, runtime.PendingCompletionCount)
        }

    [<Fact>]
    let ``Recover_without_snapshot_is_interrupted`` () =
        task {
            let runtime = ForkRuntime()
            let childId = SessionId.create "child-3"
            let agentId = "aabbcc"

            do! HostForkRestart.recoverChild runtime None agentId childId AgentRole.Browser
            let agents, _ = runtime.List()
            let record = agents |> List.find (fun a -> a.AgentId = agentId)
            Assert.equal(AgentStatus.Interrupted, record.Status)
        }
