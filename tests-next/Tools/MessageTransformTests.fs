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
            [ { Role = "user"
                Text = "  "
                ToolCalls = None
                Metadata = None }
              { Role = "user"
                Text = "hello world"
                ToolCalls = None
                Metadata = None } ]

        let result = MessageTransform.transform snapshot input

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

        let input =
            [ { Role = "user"
                Text = "test message"
                ToolCalls = None
                Metadata = None } ]

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
                Text = "Executing tools"
                ToolCalls = None
                Metadata = None }
              { Role = "tool"
                Text = "  "
                ToolCalls = None
                Metadata = None }
              { Role = "tool"
                Text = "tool result ok"
                ToolCalls = None
                Metadata = None }
              { Role = "user"
                Text = "next step"
                ToolCalls = None
                Metadata = None } ]

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
        let input =
            [ { Role = "user"
                Text = "original"
                ToolCalls = None
                Metadata = None } ]

        let snapshot =
            { Caps = []
              ReviewContext = None
              ParallelHint = None }

        let output = MessageTransform.transform snapshot input

        Assert.Equal(1, List.length input)
        Assert.Equal("original", input.[0].Text)
        Assert.Equal("original", output.[0].Text)

    [<Fact>]
    let ``MessageTransform_replacePrefix_tail_preservation`` () =
        let input =
            [ { Role = "user"
                Text = "m0"
                ToolCalls = None
                Metadata = None }
              { Role = "assistant"
                Text = "m1"
                ToolCalls = None
                Metadata = None }
              { Role = "user"
                Text = "m2"
                ToolCalls = None
                Metadata = None }
              { Role = "assistant"
                Text = "m3"
                ToolCalls = None
                Metadata = None }
              { Role = "user"
                Text = "m4"
                ToolCalls = None
                Metadata = None } ]

        let result = MessageTransform.replacePrefix input "[B_RECORD]" (Index 1)

        Assert.Equal(4, List.length result)
        Assert.Equal("system", result.[0].Role)
        Assert.Equal("[B_RECORD]", result.[0].Text)
        Assert.Equal<HostMessage list>(input |> List.skip 2, result |> List.skip 1)

    [<Fact>]
    let ``MessageTransform_replacePrefix_watermark_at_end`` () =
        let input =
            [ { Role = "user"
                Text = "m0"
                ToolCalls = None
                Metadata = None }
              { Role = "assistant"
                Text = "m1"
                ToolCalls = None
                Metadata = None }
              { Role = "user"
                Text = "m2"
                ToolCalls = None
                Metadata = None } ]

        let result = MessageTransform.replacePrefix input "[B_RECORD]" (Index 2)

        Assert.Equal<HostMessage list>(
            [ { Role = "system"
                Text = "[B_RECORD]"
                ToolCalls = None
                Metadata = None } ],
            result
        )

    [<Fact>]
    let ``MessageTransform_replacePrefix_watermark_not_found_noop`` () =
        let input =
            [ { Role = "user"
                Text = "m0"
                ToolCalls = None
                Metadata = None }
              { Role = "assistant"
                Text = "m1"
                ToolCalls = None
                Metadata = None } ]

        Assert.Equal<HostMessage list>(input, MessageTransform.replacePrefix input "[B_RECORD]" (Index 10))
        Assert.Equal<HostMessage list>(input, MessageTransform.replacePrefix input "[B_RECORD]" (Index -1))
