namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// SessionDead gate at the router terminal path: a dead session must not receive a
/// ReviewGuard guard prompt or a zero-width continuation nudge. A non-dead session
/// (fewer than 4 failures) must still receive its guard prompt (no over-blocking).
module HostEventRouterSessionDeadTests =

    let private recordingPort (prompts: ResizeArray<string * string>) =
        let activeSessions = HashSet<string>()

        { new ISessionHostPort with
            member _.SubscribeTerminal(sessionId, _) =
                activeSessions.Add(SessionId.value sessionId) |> ignore

                { new IDisposable with
                    member _.Dispose() =
                        activeSessions.Remove(SessionId.value sessionId) |> ignore }

            member _.SendPrompt(sessionId, text, _) =
                let id = SessionId.value sessionId

                if activeSessions.Contains id then
                    prompts.Add(id, text)
                    Task.FromResult(Ok(MessageId.create "accepted"))
                else
                    Task.FromResult(Error "AG-LISTENER-BEFORE-SEND")

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task

            member _.CreateChildSession(_, _) =
                Task.FromResult(Ok(SessionId.create "child"))

            member _.GetSessionOutput(_) = [] }

    let private routerFor prompts sessionId role (journal: AgentJournal option) git =
        let roles = Dictionary<string, string>()
        roles.[sessionId] <- role

        HostEventRouter(
            recordingPort prompts,
            Dictionary<string, string>(),
            roles,
            HashSet<string>(),
            HashSet<string>(),
            ?journal = journal,
            ?gitTreePort = git
        )

    let private assistantUpdated sid mid =
        createObj
            [ "type", box "message.updated"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sid
                        "info", box (createObj [ "id", box mid; "role", box "assistant"; "finish", box "stop" ]) ]
              ) ]

    let private assistantTextPart sid mid =
        createObj
            [ "type", box "message.part.updated"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sid
                        "part",
                        box (
                            createObj
                                [ "id", box "part-text"
                                  "messageID", box mid
                                  "type", box "text"
                                  "text", box "Blogger record" ]
                        ) ]
              ) ]

    let private idle sid =
        createObj
            [ "type", box "session.idle"
              "properties", box (createObj [ "sessionID", box sid ]) ]

    let private recordFailures (journal: AgentJournal) (sessionId: string) (n: int) =
        for i in 1..n do
            AgentJournal.appendAgent
                (StreamId.Session(SessionId.create sessionId))
                None
                (AgentFact.FallbackFailureRecorded
                    {| SessionId = SessionId.create sessionId
                       Reason = sprintf "f%d" i
                       AssistantMessageId = sprintf "m%d" i
                       ProviderAttempt = sprintf "pa%d" i |})
                journal
            |> ignore

    [<Fact>]
    let ``Dead_manager_session_receives_no_guard_prompt_at_terminal`` () =
        withTempDir (fun d ->
            task {
                let sid = "dead-mgr"
                let prompts = ResizeArray<string * string>()

                use j = AgentJournal.create d (RuntimeId.create "r-dm") 1 DateTimeOffset.UtcNow
                recordFailures j sid 4

                let r =
                    routerFor prompts sid "manager" (Some j) (Some { GetTreeHash = fun () -> "tree-nr" })

                r.Observe(assistantUpdated sid "am", ignore)
                r.Observe(assistantTextPart sid "am", ignore)
                r.Observe(idle sid, ignore)
                do! drainMicrotasks 8

                Assert.Empty(prompts)
            })

    [<Fact>]
    let ``Dead_session_receives_no_zero_width_continuation_at_terminal`` () =
        withTempDir (fun d ->
            task {
                let sid = "dead-cont"
                let prompts = ResizeArray<string * string>()

                use j = AgentJournal.create d (RuntimeId.create "r-dc") 1 DateTimeOffset.UtcNow
                recordFailures j sid 4

                let r = routerFor prompts sid "coder" (Some j) None
                r.Observe(assistantUpdated sid "ae", ignore)
                r.Observe(idle sid, ignore)
                do! drainMicrotasks 8

                Assert.Empty(prompts)
            })

    [<Fact>]
    let ``Non_dead_manager_with_prior_failures_still_receives_guard`` () =
        withTempDir (fun d ->
            task {
                let sid = "non-dead-mgr"
                let prompts = ResizeArray<string * string>()

                use j = AgentJournal.create d (RuntimeId.create "r-ndm") 1 DateTimeOffset.UtcNow
                recordFailures j sid 3

                let r =
                    routerFor prompts sid "manager" (Some j) (Some { GetTreeHash = fun () -> "tree-nr" })

                r.Observe(assistantUpdated sid "am", ignore)
                r.Observe(assistantTextPart sid "am", ignore)
                r.Observe(idle sid, ignore)
                do! drainMicrotasks 8

                Assert.Single(prompts) |> ignore
                Assert.Contains("Review is required before completion.", snd prompts.[0])
            })
