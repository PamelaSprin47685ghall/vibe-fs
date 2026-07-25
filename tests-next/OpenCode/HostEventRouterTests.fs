namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module HostEventRouterTests =

    let private recordingPort (prompts: ResizeArray<string * string>) =
        let activeSessions = HashSet<string>()

        { new ISessionHostPort with
            member _.SubscribeTerminal(sessionId, _) =
                let id = SessionId.value sessionId
                activeSessions.Add id |> ignore

                { new IDisposable with
                    member _.Dispose() = activeSessions.Remove id |> ignore }

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

    let private routerFor prompts sessionId =
        let roles = Dictionary<string, string>()
        roles.[sessionId] <- "manager"

        HostEventRouter(
            recordingPort prompts,
            Dictionary<string, string>(),
            roles,
            HashSet<string>(),
            HashSet<string>()
        )

    let private assistantUpdated sessionId messageId =
        createObj
            [ "type", box "message.updated"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sessionId
                        "info", box (createObj [ "id", box messageId; "role", box "assistant"; "finish", box "stop" ]) ]
              ) ]

    let private assistantTextPart sessionId messageId =
        createObj
            [ "type", box "message.part.updated"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sessionId
                        "part",
                        box (
                            createObj
                                [ "id", box "part-text"
                                  "messageID", box messageId
                                  "type", box "text"
                                  "text", box "Blogger record" ]
                        ) ]
              ) ]

    let private assistantTextPartLowerCamel sessionId messageId =
        createObj
            [ "type", box "message.part.updated"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sessionId
                        "part",
                        box (
                            createObj
                                [ "id", box "part-text-lower-camel"
                                  "messageId", box messageId
                                  "type", box "text"
                                  "text", box "Blogger record" ]
                        ) ]
              ) ]

    let private assistantEmptyTextPart sessionId messageId =
        createObj
            [ "type", box "message.part.updated"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sessionId
                        "part",
                        box (
                            createObj
                                [ "id", box "part-empty"
                                  "messageID", box messageId
                                  "type", box "text"
                                  "text", box "" ]
                        ) ]
              ) ]

    let private idle sessionId =
        createObj
            [ "type", box "session.idle"
              "properties", box (createObj [ "sessionID", box sessionId ]) ]

    let private abortError sessionId =
        createObj
            [ "type", box "session.error"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sessionId
                        "error", box (createObj [ "name", box "MessageAbortedError" ]) ]
              ) ]

    [<Fact>]
    let ``Assistant_text_part_prevents_zero_width_continuation`` () =
        let sessionId = "manager-session"
        let prompts = ResizeArray<string * string>()
        let router = routerFor prompts sessionId

        router.Observe(assistantUpdated sessionId "assistant-text", ignore)
        router.Observe(assistantTextPart sessionId "assistant-text", ignore)
        router.Observe(idle sessionId, ignore)

        Assert.Empty(prompts)

    [<Fact>]
    let ``Lower_camel_messageId_text_part_prevents_zero_width_continuation`` () =
        let sessionId = "manager-session"
        let prompts = ResizeArray<string * string>()
        let router = routerFor prompts sessionId

        router.Observe(assistantUpdated sessionId "assistant-lower-camel", ignore)
        router.Observe(assistantTextPartLowerCamel sessionId "assistant-lower-camel", ignore)
        router.Observe(idle sessionId, ignore)

        Assert.Empty(prompts)

    [<Fact>]
    let ``Terminal_empty_assistant_receives_one_zero_width_continuation`` () =
        let sessionId = "manager-session"
        let prompts = ResizeArray<string * string>()
        let router = routerFor prompts sessionId

        router.Observe(assistantUpdated sessionId "assistant-empty", ignore)
        router.Observe(idle sessionId, ignore)

        Assert.Single(prompts) |> ignore
        Assert.Equal("\u200B", snd prompts.[0])

    [<Fact>]
    let ``Terminal_empty_text_part_receives_one_zero_width_continuation`` () =
        let sessionId = "manager-session"
        let prompts = ResizeArray<string * string>()
        let router = routerFor prompts sessionId

        router.Observe(assistantUpdated sessionId "assistant-empty-text", ignore)
        router.Observe(assistantEmptyTextPart sessionId "assistant-empty-text", ignore)
        router.Observe(idle sessionId, ignore)

        Assert.Single(prompts) |> ignore
        Assert.Equal("\u200B", snd prompts.[0])

    [<Fact>]
    let ``Manager_terminal_without_current_review_receives_durable_guard_prompt`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "manager-guard-session"
                let prompts = ResizeArray<string * string>()
                let roles = Dictionary<string, string>()
                roles.[sessionId] <- "manager"

                use journal =
                    AgentJournal.create directory (RuntimeId.create "manager-guard-runtime") 1 DateTimeOffset.UtcNow

                let gitTreePort = { GetTreeHash = fun () -> "tree-without-review" }

                let router =
                    HostEventRouter(
                        recordingPort prompts,
                        Dictionary<string, string>(),
                        roles,
                        HashSet<string>(),
                        HashSet<string>(),
                        journal = journal,
                        gitTreePort = gitTreePort
                    )

                router.Observe(assistantUpdated sessionId "assistant-manager-guard", ignore)
                router.Observe(assistantTextPart sessionId "assistant-manager-guard", ignore)
                router.Observe(idle sessionId, ignore)

                Assert.Single(prompts) |> ignore
                Assert.Contains("Review is required before completion.", snd prompts.[0])
            })

    [<Fact>]
    let ``Aborted_manager_terminal_never_receives_review_or_continuation_nudge`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "aborted-manager-session"
                let prompts = ResizeArray<string * string>()
                let roles = Dictionary<string, string>()
                roles.[sessionId] <- "manager"

                use journal =
                    AgentJournal.create directory (RuntimeId.create "aborted-manager-runtime") 1 DateTimeOffset.UtcNow

                let router =
                    HostEventRouter(
                        recordingPort prompts,
                        Dictionary<string, string>(),
                        roles,
                        HashSet<string>(),
                        HashSet<string>(),
                        journal = journal,
                        gitTreePort = { GetTreeHash = fun () -> "tree-after-abort" }
                    )

                router.Observe(assistantUpdated sessionId "assistant-aborted", ignore)
                router.Observe(abortError sessionId, ignore)
                router.Observe(idle sessionId, ignore)

                Assert.Empty(prompts)
            })
