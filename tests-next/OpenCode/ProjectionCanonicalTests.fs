namespace Wanxiangshu.Next.Tests.OpenCode

open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.OpenCode

module ProjectionCanonicalTests =

    let private msg role text extraInfo =
        let info =
            createObj (
                [ "id", box "m1"
                  "role", box role ]
                @ extraInfo
            )

        createObj
            [ "info", box info
              "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]

    [<Fact>]
    let ``Non_model_metadata_does_not_change_canonical_bytes`` () =
        let a =
            msg
                "user"
                "hello"
                [ "sessionID", box "s1"
                  "time", box (createObj [ "created", box 1 ])
                  "cost", box 0.12
                  "usage", box (createObj [ "input", box 10 ]) ]

        let b =
            msg
                "user"
                "hello"
                [ "sessionID", box "s1"
                  "time", box (createObj [ "created", box 999 ])
                  "cost", box 9.99
                  "usage", box (createObj [ "input", box 999 ])
                  "directory", box "/tmp/other"
                  "status", box "busy" ]

        Assert.Equal(Projection.canonicalMessageJson a, Projection.canonicalMessageJson b)
        Assert.True(Projection.sameCanonicalMessage a b)

    [<Fact>]
    let ``Provider_visible_text_change_changes_canonical_bytes`` () =
        let a = msg "user" "hello" []
        let b = msg "user" "hello!" []
        Assert.True(Projection.canonicalMessageJson a <> Projection.canonicalMessageJson b)

    [<Fact>]
    let ``Reasoning_and_tool_result_are_provider_visible`` () =
        let withReasoning =
            createObj
                [ "info", box (createObj [ "id", box "a1"; "role", box "assistant" ])
                  "parts",
                  box
                      [| createObj [ "type", box "reasoning"; "text", box "think" ]
                         createObj
                             [ "type", box "tool-result"
                               "callID", box "c1"
                               "result", box (createObj [ "ok", box true ]) ] |] ]

        let withoutReasoning =
            createObj
                [ "info", box (createObj [ "id", box "a1"; "role", box "assistant" ])
                  "parts",
                  box
                      [| createObj
                             [ "type", box "tool-result"
                               "callID", box "c1"
                               "result", box (createObj [ "ok", box true ]) ] |] ]

        Assert.True(
            Projection.canonicalMessageJson withReasoning
            <> Projection.canonicalMessageJson withoutReasoning
        )

        let json = Projection.canonicalMessageJson withReasoning
        Assert.True(json.Contains("reasoning"))
        Assert.True(json.Contains("tool-result"))
        Assert.False(json.Contains("\"id\"")) // part/message ids excluded

    [<Fact>]
    let ``Unknown_host_parts_are_dropped_not_raw_embedded`` () =
        let raw =
            createObj
                [ "info", box (createObj [ "id", box "a1"; "role", box "assistant" ])
                  "parts",
                  box
                      [| createObj [ "type", box "text"; "text", box "hi" ]
                         createObj
                             [ "type", box "step-start"
                               "id", box "step1"
                               "raw", box (createObj [ "timestamp", box 123 ]) ] |] ]

        let json = Projection.canonicalMessageJson raw
        Assert.False(json.Contains("step-start"))
        Assert.False(json.Contains("timestamp"))
        Assert.True(json.Contains("hi"))
