namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

module HostEventRouterTests =

    let private recordingPort (prompts: ResizeArray<string * string>) =
        { new ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(sessionId, text, _) =
                prompts.Add(SessionId.value sessionId, text)
                Task.FromResult(Ok(MessageId.create "accepted"))

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
