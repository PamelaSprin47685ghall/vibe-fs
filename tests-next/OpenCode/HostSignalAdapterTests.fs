namespace Wanxiangshu.Next.Tests.OpenCode

open System.Collections.Generic
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

module HostSignalAdapterTests =

    let private owned = fun (sessionId: SessionId) -> SessionId.value sessionId = "s1"

    [<Fact>]
    let ``Drops_message_and_part_events`` () =
        let raw =
            createObj
                [ "type", box "message.part.updated"
                  "properties", box (createObj [ "sessionID", box "s1" ]) ]

        Assert.True(HostSignalAdapter.tryAdapt owned raw |> Option.isNone)

        let updated =
            createObj
                [ "type", box "message.updated"
                  "properties", box (createObj [ "sessionID", box "s1" ]) ]

        Assert.True(HostSignalAdapter.tryAdapt owned raw |> Option.isNone)
        Assert.True(HostSignalAdapter.tryAdapt owned updated |> Option.isNone)

    [<Fact>]
    let ``Idle_and_retry_and_deleted_are_signals`` () =
        let idle =
            createObj
                [ "type", box "session.status"
                  "properties",
                  box (
                      createObj
                          [ "sessionID", box "s1"
                            "status", box (createObj [ "type", box "idle" ]) ]
                  ) ]

        match HostSignalAdapter.tryAdapt owned idle with
        | Some(SessionIdle sid) -> Assert.Equal("s1", SessionId.value sid)
        | other -> Assert.True(false, sprintf "unexpected %A" other)

        let retry =
            createObj
                [ "type", box "session.status"
                  "properties",
                  box (
                      createObj
                          [ "sessionID", box "s1"
                            "messageID", box "m1"
                            "status",
                            box (
                                createObj
                                    [ "type", box "retry"
                                      "attempt", box 2
                                      "message", box "provider blew up" ]
                            ) ]
                  ) ]

        match HostSignalAdapter.tryAdapt owned retry with
        | Some(ProviderRetry signal) ->
            Assert.Equal("2", signal.Attempt)
            Assert.Equal("provider blew up", signal.Reason)
            Assert.Equal("m1", signal.MessageId |> Option.map MessageId.value |> Option.defaultValue "")
        | other -> Assert.True(false, sprintf "unexpected %A" other)

        let deleted =
            createObj
                [ "type", box "session.deleted"
                  "properties", box (createObj [ "sessionID", box "s1" ]) ]

        match HostSignalAdapter.tryAdapt owned deleted with
        | Some(SessionDeleted sid) -> Assert.Equal("s1", SessionId.value sid)
        | other -> Assert.True(false, sprintf "unexpected %A" other)

    [<Fact>]
    let ``Unowned_session_is_ignored`` () =
        let idle =
            createObj
                [ "type", box "session.idle"
                  "properties", box (createObj [ "sessionID", box "other" ]) ]

        Assert.True(HostSignalAdapter.tryAdapt owned idle |> Option.isNone)
