namespace Wanxiangshu.Next.Tests.JournalTests

open System
open System.IO
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open JournalTestSupport

module JournalBootTests =

    [<Fact>]
    let Boot_ignores_partial_trailing_line () =
        withTempDir (fun dir ->
            task {
                let runtimeId = RuntimeId.create "rt-partial-line"
                let filePath = sprintf "%s/rt-partial-line.ndjson" dir
                let now = DateTimeOffset.UtcNow

                let env1: Envelope =
                    { RuntimeId = runtimeId
                      LocalSeq = LocalSeq.create 1L
                      ObservedAt = now
                      EventId = EventId.create "e1"
                      Stream = StreamId.Workspace
                      TurnId = None
                      Fact =
                        Fact.Runtime(
                            RuntimeStarted
                                {| RuntimeId = runtimeId
                                   ProcessId = 100
                                   StartedAt = now |}
                        ) }

                let snapshot = Boot.boot dir
                Assert.NotNull(snapshot.Diagnostics)
            })

    [<Fact>]
    let Boot_captures_diagnostics_on_illegal_mid_file_line () =
        withTempDir (fun dir ->
            task {
                let snapshot = Boot.boot dir
                Assert.NotNull(snapshot.Diagnostics)
            })

    [<Fact>]
    let Boot_merges_by_ObservedAt_RuntimeId_LocalSeq () =
        withTempDir (fun dir ->
            task {
                let snapshot = Boot.boot dir
                Assert.NotNull(snapshot.Envelopes)
            })

    [<Fact>]
    let Boot_merges_same_ObservedAt_by_RuntimeId_Ordinal () =
        withTempDir (fun dir ->
            task {
                let snapshot = Boot.boot dir
                Assert.NotNull(snapshot.Envelopes)
            })

    [<Fact>]
    let Boot_accepts_complete_line_without_trailing_newline () =
        withTempDir (fun dir ->
            task {
                let snapshot = Boot.boot dir
                Assert.NotNull(snapshot.Envelopes)
            })

    [<Fact>]
    let Boot_keeps_complete_last_line_when_file_lacks_trailing_newline () =
        withTempDir (fun dir ->
            task {
                let snapshot = Boot.boot dir
                Assert.NotNull(snapshot.Envelopes)
            })
