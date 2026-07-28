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

        Assert.True(HostSignalAdapter.tryAdapt owned updated |> Option.isNone)

    [<Fact>]
    let ``Drops_legacy_session_idle_and_abort_session_error`` () =
        let idle =
            createObj
                [ "type", box "session.idle"
                  "properties", box (createObj [ "sessionID", box "s1" ]) ]

        Assert.True(HostSignalAdapter.tryAdapt owned idle |> Option.isNone)

        let abortErr =
            createObj
                [ "type", box "session.error"
                  "properties",
                  box (
                      createObj
                          [ "sessionID", box "s1"
                            "error", box (createObj [ "name", box "MessageAbortedError" ]) ]
                  ) ]

        Assert.True(HostSignalAdapter.tryAdapt owned abortErr |> Option.isNone)

    [<Fact>]
    let ``Non_retryable_session_error_is_provider_error_signal`` () =
        let err =
            createObj
                [ "type", box "session.error"
                  "properties",
                  box (
                      createObj
                          [ "sessionID", box "s1"
                            "error",
                            box (
                                createObj
                                    [ "name", box "APIError"
                                      "data",
                                      box (
                                          createObj
                                              [ "message", box "bad request"
                                                "statusCode", box 400
                                                "isRetryable", box false ]
                                      ) ]
                            ) ]
                  ) ]

        match HostSignalAdapter.tryAdapt owned err with
        | Some(ProviderError signal) ->
            Assert.equal ("s1", SessionId.value signal.SessionId)
            Assert.equal ("bad request", signal.Reason)
            Assert.equal (Some 400, signal.StatusCode)
            Assert.equal (Some false, signal.IsRetryable)
        | other -> Assert.True(false, sprintf "unexpected %A" other)

    [<Fact>]
    let ``Devin_stream_error_without_statusCode_is_provider_error_signal`` () =
        let err =
            createObj
                [ "type", box "session.error"
                  "properties",
                  box (
                      createObj
                          [ "sessionID", box "s1"
                            "error",
                            box (
                                createObj
                                    [ "name", box "APIError"
                                      "message",
                                      box
                                          "Devin stream error invalid_argument: an internal error occurred (trace ID: 85c5a621ac1dcff4667eb684fa3e95b1)" ]
                            ) ]
                  ) ]

        match HostSignalAdapter.tryAdapt owned err with
        | Some(ProviderError signal) ->
            Assert.equal ("s1", SessionId.value signal.SessionId)

            Assert.equal (
                "Devin stream error invalid_argument: an internal error occurred (trace ID: 85c5a621ac1dcff4667eb684fa3e95b1)",
                signal.Reason
            )

            Assert.equal (None, signal.StatusCode)
            Assert.equal (None, signal.IsRetryable)
        | other -> Assert.True(false, sprintf "unexpected %A" other)

    [<Fact>]
    let ``Idle_and_retry_and_deleted_are_signals`` () =
        let idle =
            createObj
                [ "type", box "session.status"
                  "properties",
                  box (createObj [ "sessionID", box "s1"; "status", box (createObj [ "type", box "idle" ]) ]) ]

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
                            box (createObj [ "type", box "retry"; "attempt", box 2; "message", box "provider blew up" ]) ]
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
                [ "type", box "session.status"
                  "properties",
                  box (createObj [ "sessionID", box "other"; "status", box (createObj [ "type", box "idle" ]) ]) ]

        Assert.True(HostSignalAdapter.tryAdapt owned idle |> Option.isNone)

    [<Fact>]
    let ``Empty_owned_registry_is_fail_closed`` () =
        let signals = ResizeArray<HostSignal>()
        let owned = HashSet<string>()
        let router = HostSignalRouter(owned, (fun s -> signals.Add s))

        let idle =
            createObj
                [ "type", box "session.status"
                  "properties",
                  box (createObj [ "sessionID", box "s1"; "status", box (createObj [ "type", box "idle" ]) ]) ]

        router.Observe idle
        Assert.Equal(0, signals.Count)

        router.RegisterOwned(SessionId.create "s1")
        router.Observe idle
        Assert.Equal(1, signals.Count)
