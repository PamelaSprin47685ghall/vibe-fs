namespace Wanxiangshu.Next.Tests.OpenCode

open Xunit
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

module EventsTests =

    let private eventWithProperties eventType properties =
        createObj [ "type", box eventType; "properties", properties ]

    [<Fact>]
    let ``HostEventPort_filters_user_and_zero_width_parts_and_flushes_reverse_order`` () =
        let eventPort = Events.HostEventPort()
        let sessionId = SessionId.create "event-output-session"

        eventPort.Observe(
            eventWithProperties
                "message.updated"
                (createObj
                    [ "sessionID", box "event-output-session"
                      "info", createObj [ "id", box "user-message"; "role", box "user" ] ])
        )

        eventPort.Observe(
            eventWithProperties
                "message.part.updated"
                (createObj
                    [ "sessionID", box "event-output-session"
                      "part",
                      createObj
                          [ "id", box "user-part"
                            "messageID", box "user-message"
                            "type", box "text"
                            "text", box "user prompt must not leak" ] ])
        )

        // Host streams can publish parts before the assistant message.updated
        // role envelope, and OpenCode may first emit a zero-width placeholder.
        eventPort.Observe(
            eventWithProperties
                "message.part.updated"
                (createObj
                    [ "sessionID", box "event-output-session"
                      "part",
                      createObj
                          [ "id", box "assistant-part"
                            "messageID", box "assistant-message"
                            "type", box "text"
                            "text", box "\u200B" ] ])
        )

        eventPort.Observe(
            eventWithProperties
                "message.part.updated"
                (createObj
                    [ "sessionID", box "event-output-session"
                      "part",
                      createObj
                          [ "id", box "assistant-part"
                            "messageID", box "assistant-message"
                            "type", box "text"
                            "text", box "assistant answer" ] ])
        )

        eventPort.Observe(
            eventWithProperties
                "message.updated"
                (createObj
                    [ "sessionID", box "event-output-session"
                      "info", createObj [ "id", box "assistant-message"; "role", box "assistant" ] ])
        )

        let observation = eventPort :> IEventObservationPort
        Assert.Equal([ "assistant answer" ], observation.GetSessionOutput sessionId)
        Assert.Equal(1, observation.GetSessionOutputWatermark sessionId)
        Assert.Empty(observation.GetSessionOutputSince(sessionId, 1))
        Assert.Equal([ "assistant answer" ], observation.GetSessionOutputSince(sessionId, 0))
