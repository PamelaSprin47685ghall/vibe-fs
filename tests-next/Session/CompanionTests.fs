namespace Wanxiangshu.Next.Tests.Session

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module CompanionTests =

    [<Fact>]
    let ``jsonDelta_with_no_previous_checkpoint_returns_all_events`` () =
        let events =
            [ { Cursor = 1; Json = "{\"e\":1}" }
              { Cursor = 2; Json = "{\"e\":2}" }
              { Cursor = 3; Json = "{\"e\":3}" } ]

        let delta = Companion.jsonDelta None events
        Assert.Equal<TranscriptEvent list>(events, delta)

    [<Fact>]
    let ``jsonDelta_with_watermark_filters_and_returns_contiguous_tail`` () =
        let checkpoint = Some { Watermark = 2; Content = "B2" }

        let events =
            [ { Cursor = 1; Json = "{\"e\":1}" }
              { Cursor = 2; Json = "{\"e\":2}" }
              { Cursor = 3; Json = "{\"e\":3}" }
              { Cursor = 4; Json = "{\"e\":4}" } ]

        let delta = Companion.jsonDelta checkpoint events

        let expected =
            [ { Cursor = 3; Json = "{\"e\":3}" }; { Cursor = 4; Json = "{\"e\":4}" } ]

        Assert.Equal<TranscriptEvent list>(expected, delta)

    [<Fact>]
    let ``jsonDelta_stops_at_non_contiguous_gap`` () =
        let checkpoint = Some { Watermark = 2; Content = "B2" }

        let events =
            [ { Cursor = 3; Json = "{\"e\":3}" }; { Cursor = 5; Json = "{\"e\":5}" } ]

        let delta = Companion.jsonDelta checkpoint events
        let expected = [ { Cursor = 3; Json = "{\"e\":3}" } ]

        Assert.Equal<TranscriptEvent list>(expected, delta)

    [<Fact>]
    let ``jsonDelta_returns_empty_when_watermark_plus_one_missing`` () =
        let checkpoint = Some { Watermark = 2; Content = "B2" }

        let events =
            [ { Cursor = 4; Json = "{\"e\":4}" }; { Cursor = 5; Json = "{\"e\":5}" } ]

        let delta = Companion.jsonDelta checkpoint events
        Assert.Empty(delta)

    [<Fact>]
    let ``jsonDelta_with_empty_events_returns_empty`` () =
        let delta = Companion.jsonDelta None []
        Assert.Empty(delta)

    [<Fact>]
    let ``compressPrefix_delegates_to_replacePrefix_preserving_raw_tail`` () =
        let messages: HostMessage list =
            [ { Role = "user"
                Text = "u0"
                ToolCalls = None
                Metadata = None }
              { Role = "assistant"
                Text = "a1"
                ToolCalls = None
                Metadata = None }
              { Role = "user"
                Text = "u2"
                ToolCalls = None
                Metadata = None }
              { Role = "assistant"
                Text = "a3"
                ToolCalls = None
                Metadata = None } ]

        let checkpoint =
            Some
                { Watermark = 1
                  Content = "[SUMMARY_B]" }

        let result = Companion.compressPrefix messages checkpoint

        Assert.Equal(3, List.length result)
        Assert.Equal("system", result.[0].Role)
        Assert.Equal("[SUMMARY_B]", result.[0].Text)

        Assert.Equal("user", result.[1].Role)
        Assert.Equal("u2", result.[1].Text)
        Assert.Equal("assistant", result.[2].Role)
        Assert.Equal("a3", result.[2].Text)

    [<Fact>]
    let ``compressPrefix_returns_messages_unchanged_when_checkpoint_is_None`` () =
        let messages: HostMessage list =
            [ { Role = "user"
                Text = "u0"
                ToolCalls = None
                Metadata = None }
              { Role = "assistant"
                Text = "a1"
                ToolCalls = None
                Metadata = None } ]

        let result = Companion.compressPrefix messages None
        Assert.Equal<HostMessage list>(messages, result)

    [<Fact>]
    let ``Submit_starts_blog_function_when_idle_and_updates_checkpoint_atomically`` () =
        task {
            let companion = Companion()

            let events =
                [ { Cursor = 1; Json = "{\"e\":1}" }; { Cursor = 2; Json = "{\"e\":2}" } ]

            let outcome = companion.Submit(events, (fun _ -> Task.FromResult "Summary AB"))
            Assert.Equal(Submitted, outcome)

            do! companion.WaitInFlightAsync()

            match companion.Snapshot with
            | Some cp ->
                Assert.Equal(2, cp.Watermark)
                Assert.Equal("Summary AB", cp.Content)
            | None -> Assert.Fail("Expected checkpoint to be set")
        }

    [<Fact>]
    let ``Submit_returns_SkippedBusy_when_busy_without_implicit_queue`` () =
        task {
            let companion = Companion()
            let tcs = TaskCompletionSource<string>()
            let events = [ { Cursor = 1; Json = "{\"e\":1}" } ]

            let outcome1 = companion.Submit(events, (fun _ -> tcs.Task))
            Assert.Equal(Submitted, outcome1)
            Assert.True(companion.IsBusy)

            let outcome2 =
                companion.Submit(events, (fun _ -> Task.FromResult "Second Should Be Skipped"))

            Assert.Equal(SkippedBusy, outcome2)

            tcs.SetResult("First Blog Summary")
            do! companion.WaitInFlightAsync()

            Assert.False(companion.IsBusy)

            match companion.Snapshot with
            | Some cp ->
                Assert.Equal(1, cp.Watermark)
                Assert.Equal("First Blog Summary", cp.Content)
            | None -> Assert.Fail("Expected checkpoint to be set")
        }

    [<Fact>]
    let ``Failure_in_blog_function_is_non_blocking_leaving_main_caller_unaffected`` () =
        task {
            let companion = Companion()
            let events = [ { Cursor = 1; Json = "{\"e\":1}" } ]

            let outcome =
                companion.Submit(
                    events,
                    fun _ -> Task.FromException<string>(InvalidOperationException("Blogger crashed"))
                )

            Assert.Equal(Submitted, outcome)

            do! companion.WaitInFlightAsync()

            Assert.False(companion.IsBusy)
            Assert.True(companion.Snapshot.IsNone)

            // Subsequent Submit succeeds
            let outcome2 =
                companion.Submit(events, (fun _ -> Task.FromResult "Recovered Summary"))

            Assert.Equal(Submitted, outcome2)

            do! companion.WaitInFlightAsync()

            match companion.Snapshot with
            | Some cp -> Assert.Equal("Recovered Summary", cp.Content)
            | None -> Assert.Fail("Expected checkpoint after recovery")
        }

    [<Fact>]
    let ``TryRebase_updates_checkpoint_when_idle_and_fails_when_busy`` () =
        task {
            let companion = Companion()
            Assert.True(companion.Snapshot.IsNone)

            let rebased =
                companion.TryRebase(
                    { Watermark = 10
                      Content = "Manual Rebase" }
                )

            Assert.True(rebased)

            match companion.Snapshot with
            | Some cp ->
                Assert.Equal(10, cp.Watermark)
                Assert.Equal("Manual Rebase", cp.Content)
            | None -> Assert.Fail("Expected rebased checkpoint")

            let tcs = TaskCompletionSource<string>()
            let outcome = companion.Submit([], (fun _ -> tcs.Task))
            Assert.Equal(Submitted, outcome)

            let rebasedBusy =
                companion.TryRebase(
                    { Watermark = 20
                      Content = "Busy Rebase" }
                )

            Assert.False(rebasedBusy)

            Assert.Equal(10, companion.Snapshot.Value.Watermark)

            tcs.SetResult("Finished")
            do! companion.WaitInFlightAsync()
        }
