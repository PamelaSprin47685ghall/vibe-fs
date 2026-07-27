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
open Wanxiangshu.Next.Tests.EventDrivenHarness
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
        roles.[sessionId] <- "coder"

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
    let ``Missing_parts_is_unknown_not_empty`` () =
        // An assistant message whose parts were never observed (shutdown race,
        // SSE reorder, or already-consumed turn) is UNKNOWN, not empty: it must
        // produce no zero-width continuation and no durable fallback fact.
        let sessionId = "manager-session"
        let prompts = ResizeArray<string * string>()
        let router = routerFor prompts sessionId

        router.Observe(assistantUpdated sessionId "assistant-empty", ignore)
        router.Observe(idle sessionId, ignore)

        Assert.Empty(prompts)

    [<Fact>]
    let ``Idle_without_current_assistant_is_ignored`` () =
        // A stray/duplicate idle with no unconsumed assistant message performs
        // no message-level side effects at all.
        let sessionId = "manager-session"
        let prompts = ResizeArray<string * string>()
        let router = routerFor prompts sessionId

        router.Observe(idle sessionId, ignore)
        router.Observe(idle sessionId, ignore)

        Assert.Empty(prompts)

    [<Fact>]
    let ``Successful_message_followed_by_duplicate_idle_is_consumed_once`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "dup-idle-session"
                let prompts = ResizeArray<string * string>()
                let roles = Dictionary<string, string>()
                roles.[sessionId] <- "coder"

                use journal =
                    AgentJournal.create directory (RuntimeId.create "dup-idle-runtime") 1 DateTimeOffset.UtcNow

                let router =
                    HostEventRouter(
                        recordingPort prompts,
                        Dictionary<string, string>(),
                        roles,
                        HashSet<string>(),
                        HashSet<string>(),
                        journal = journal
                    )

                router.Observe(assistantUpdated sessionId "assistant-dup", ignore)
                router.Observe(assistantTextPart sessionId "assistant-dup", ignore)
                router.Observe(idle sessionId, ignore)
                // Restart/shutdown re-propagates idle; the turn was already consumed.
                router.Observe(idle sessionId, ignore)
                do! drainMicrotasks 8

                Assert.Empty(prompts)

                let failures =
                    match
                        (AgentJournal.snapshot journal)
                            .AgentProjections.Sessions.TryFind(SessionId.create sessionId)
                    with
                    | Some session ->
                        session.Fallback
                        |> Option.map (fun fb -> fb.TotalFailures)
                        |> Option.defaultValue 0
                    | None -> 0

                Assert.Equal(0, failures)
            })

    let private userUpdated sessionId messageId =
        createObj
            [ "type", box "message.updated"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sessionId
                        "info", box (createObj [ "id", box messageId; "role", box "user" ]) ]
              ) ]

    let private statusRetry sessionId attempt reason =
        createObj
            [ "type", box "session.status"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sessionId
                        "status",
                        box (
                            createObj
                                [ "type", box "retry"
                                  "attempt", box attempt
                                  "message", box reason ]
                        ) ]
              ) ]

    let private fallbackFailures (journal: AgentJournal) sessionId =
        match
            (AgentJournal.snapshot journal)
                .AgentProjections.Sessions.TryFind(SessionId.create sessionId)
        with
        | Some session ->
            session.Fallback
            |> Option.map (fun fb -> fb.TotalFailures)
            |> Option.defaultValue 0
        | None -> 0

    [<Fact>]
    let ``Same_user_replay_keeps_assistant_id_for_provider_retry`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "retry-identity-session"
                let prompts = ResizeArray<string * string>()
                let roles = Dictionary<string, string>()
                roles.[sessionId] <- "coder"

                use journal =
                    AgentJournal.create directory (RuntimeId.create "retry-identity-runtime") 1 DateTimeOffset.UtcNow

                let router =
                    HostEventRouter(
                        recordingPort prompts,
                        Dictionary<string, string>(),
                        roles,
                        HashSet<string>(),
                        HashSet<string>(),
                        journal = journal
                    )

                // OpenCode creates the assistant, then re-emits the same user
                // message.updated while the provider call is in flight. That
                // replay must not drop the assistant id before session.status=retry.
                router.Observe(userUpdated sessionId "user-1", ignore)
                router.Observe(assistantUpdated sessionId "assistant-inflight", ignore)
                router.Observe(userUpdated sessionId "user-1", ignore)
                router.Observe(statusRetry sessionId 1 "mock provider failure round1", ignore)

                Assert.Equal(1, fallbackFailures journal sessionId)

                // A genuinely new user turn still clears the stale assistant so a
                // later retry cannot attribute against the previous turn.
                router.Observe(userUpdated sessionId "user-2", ignore)
                router.Observe(statusRetry sessionId 2 "should-not-record-without-assistant", ignore)
                Assert.Equal(1, fallbackFailures journal sessionId)
            })
