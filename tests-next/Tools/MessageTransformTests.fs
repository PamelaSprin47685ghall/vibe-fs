namespace Wanxiangshu.Next.Tests.Tools

open Xunit
open Wanxiangshu.Next.Tools

module MessageTransformTests =

    [<Fact>]
    let ``MessageTransform_order_markers_and_sanitization`` () =
        let snapshot =
            { Caps = [ "coder"; "browser" ]
              ReviewContext = Some "Reviewing PR #42"
              ParallelHint = Some "Run in parallel" }

        let input =
            [ { Role = "user"; Text = "  " }; { Role = "user"; Text = "hello world" } ]

        let result = MessageTransform.transform snapshot input

        // Sanitization removes empty/whitespace text, leaves "hello world"
        // addCaps adds system message at front: [CAPS: coder, browser]
        // addReviewContext adds system message: [REVIEW: Reviewing PR #42]
        // addParallelHint adds system message: [HINT: Run in parallel]
        Assert.Equal(4, List.length result)
        Assert.Equal("system", result.[0].Role)
        Assert.Equal("[CAPS: coder, browser]", result.[0].Text)
        Assert.Equal("system", result.[1].Role)
        Assert.Equal("[REVIEW: Reviewing PR #42]", result.[1].Text)
        Assert.Equal("system", result.[2].Role)
        Assert.Equal("[HINT: Run in parallel]", result.[2].Text)
        Assert.Equal("user", result.[3].Role)
        Assert.Equal("hello world", result.[3].Text)

    [<Fact>]
    let ``MessageTransform_idempotent`` () =
        let snapshot =
            { Caps = [ "coder" ]
              ReviewContext = Some "Context"
              ParallelHint = None }

        let input = [ { Role = "user"; Text = "test message" } ]

        let once = MessageTransform.transform snapshot input
        let twice = MessageTransform.transform snapshot once

        Assert.Equal<HostMessage list>(once, twice)

    [<Fact>]
    let ``MessageTransform_sanitize_drops_empty_tool_messages`` () =
        let snapshot =
            { Caps = []
              ReviewContext = None
              ParallelHint = None }

        let input =
            [ { Role = "assistant"
                Text = "Executing tools" }
              { Role = "tool"; Text = "  " }
              { Role = "tool"
                Text = "tool result ok" }
              { Role = "user"; Text = "next step" } ]

        let result = MessageTransform.transform snapshot input

        Assert.Equal(3, List.length result)
        Assert.Equal("assistant", result.[0].Role)
        Assert.Equal("Executing tools", result.[0].Text)
        Assert.Equal("tool", result.[1].Role)
        Assert.Equal("tool result ok", result.[1].Text)
        Assert.Equal("user", result.[2].Role)
        Assert.Equal("next step", result.[2].Text)

    [<Fact>]
    let ``MessageTransform_input_list_not_mutated`` () =
        let input = [ { Role = "user"; Text = "original" } ]

        let snapshot =
            { Caps = []
              ReviewContext = None
              ParallelHint = None }

        let output = MessageTransform.transform snapshot input

        Assert.Equal(1, List.length input)
        Assert.Equal("original", input.[0].Text)
        Assert.Equal("original", output.[0].Text)
