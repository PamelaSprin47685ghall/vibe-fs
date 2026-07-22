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
            let runtimeId = RuntimeId.create "rt-partial-line"
            let filePath = Path.Combine(dir, "rt-partial-line.ndjson")
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

            let line1 = Envelope.serialize env1

            File.WriteAllText(
                filePath,
                line1
                + "\n"
                + "{\"RuntimeId\":\"rt-partial-line\",\"LocalSeq\":2,\"ObservedAt\":\"2025-01-01T00:00:00Z\",\"EventId\":\"e2\","
            )

            let snapshot = Boot.boot dir
            Assert.NotNull(snapshot.Diagnostics)
            Assert.Single(snapshot.Envelopes) |> ignore
            Assert.Equal(EventId.create "e1", snapshot.Envelopes.[0].EventId))

    [<Fact>]
    let Boot_captures_diagnostics_on_illegal_mid_file_line () =
        withTempDir (fun dir ->
            let runtimeId = RuntimeId.create "rt-illegal-mid"
            let filePath = Path.Combine(dir, "rt-illegal-mid.ndjson")
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

            let line1 = Envelope.serialize env1
            File.WriteAllText(filePath, line1 + "\n" + "{\"invalid_json_line\": true}\n")
            let snapshot = Boot.boot dir
            Assert.Single(snapshot.Envelopes) |> ignore
            Assert.NotEmpty(snapshot.Diagnostics)
            Assert.Contains("Failed to parse line", snapshot.Diagnostics.[0]))

    [<Fact>]
    let Boot_merges_by_ObservedAt_RuntimeId_LocalSeq () =
        withTempDir (fun dir ->
            let rtA, rtB = RuntimeId.create "rt-A", RuntimeId.create "rt-B"

            let t1, t2 =
                DateTimeOffset.Parse("2025-01-01T10:00:00Z"), DateTimeOffset.Parse("2025-01-01T11:00:00Z")

            let envB1: Envelope =
                { RuntimeId = rtB
                  LocalSeq = LocalSeq.create 1L
                  ObservedAt = t1
                  EventId = EventId.create "b1"
                  Stream = StreamId.Workspace
                  TurnId = None
                  Fact =
                    Fact.Runtime(
                        RuntimeStarted
                            {| RuntimeId = rtB
                               ProcessId = 2
                               StartedAt = t1 |}
                    ) }

            let envA1: Envelope =
                { RuntimeId = rtA
                  LocalSeq = LocalSeq.create 1L
                  ObservedAt = t2
                  EventId = EventId.create "a1"
                  Stream = StreamId.Workspace
                  TurnId = None
                  Fact =
                    Fact.Runtime(
                        RuntimeStarted
                            {| RuntimeId = rtA
                               ProcessId = 1
                               StartedAt = t2 |}
                    ) }

            File.WriteAllText(Path.Combine(dir, "rt-B.ndjson"), Envelope.serialize envB1 + "\n")
            File.WriteAllText(Path.Combine(dir, "rt-A.ndjson"), Envelope.serialize envA1 + "\n")
            let snapshot = Boot.boot dir
            Assert.Equal(2, snapshot.Envelopes.Length)
            Assert.Equal(rtB, snapshot.Envelopes.[0].RuntimeId)
            Assert.Equal(rtA, snapshot.Envelopes.[1].RuntimeId))

    [<Fact>]
    let Boot_merges_same_ObservedAt_by_RuntimeId_Ordinal () =
        withTempDir (fun dir ->
            let rtA, rtB = RuntimeId.create "rt-A", RuntimeId.create "rt-B"
            let t = DateTimeOffset.Parse("2025-01-01T10:00:00Z")

            let envB1: Envelope =
                { RuntimeId = rtB
                  LocalSeq = LocalSeq.create 1L
                  ObservedAt = t
                  EventId = EventId.create "b1"
                  Stream = StreamId.Workspace
                  TurnId = None
                  Fact =
                    Fact.Runtime(
                        RuntimeStarted
                            {| RuntimeId = rtB
                               ProcessId = 2
                               StartedAt = t |}
                    ) }

            let envA1: Envelope =
                { RuntimeId = rtA
                  LocalSeq = LocalSeq.create 1L
                  ObservedAt = t
                  EventId = EventId.create "a1"
                  Stream = StreamId.Workspace
                  TurnId = None
                  Fact =
                    Fact.Runtime(
                        RuntimeStarted
                            {| RuntimeId = rtA
                               ProcessId = 1
                               StartedAt = t |}
                    ) }

            File.WriteAllText(Path.Combine(dir, "rt-B.ndjson"), Envelope.serialize envB1 + "\n")
            File.WriteAllText(Path.Combine(dir, "rt-A.ndjson"), Envelope.serialize envA1 + "\n")
            let snapshot = Boot.boot dir
            Assert.Equal(2, snapshot.Envelopes.Length)
            Assert.Equal(rtA, snapshot.Envelopes.[0].RuntimeId)
            Assert.Equal(rtB, snapshot.Envelopes.[1].RuntimeId))

    [<Fact>]
    let Boot_accepts_complete_line_without_trailing_newline () =
        withTempDir (fun dir ->
            let runtimeId = RuntimeId.create "rt-no-newline"
            let filePath = Path.Combine(dir, "rt-no-newline.ndjson")
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

            File.WriteAllText(filePath, Envelope.serialize env1)
            let snapshot = Boot.boot dir
            Assert.Empty(snapshot.Diagnostics)
            Assert.Single(snapshot.Envelopes) |> ignore
            Assert.Equal(EventId.create "e1", snapshot.Envelopes.[0].EventId))

    [<Fact>]
    let Boot_keeps_complete_last_line_when_file_lacks_trailing_newline () =
        withTempDir (fun dir ->
            let runtimeId = RuntimeId.create "rt-multiline-no-newline"
            let filePath = Path.Combine(dir, "rt-multiline-no-newline.ndjson")
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

            let env2: Envelope =
                { RuntimeId = runtimeId
                  LocalSeq = LocalSeq.create 2L
                  ObservedAt = now.AddSeconds(1.0)
                  EventId = EventId.create "e2"
                  Stream = StreamId.Workspace
                  TurnId = None
                  Fact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ "task2" ] } |}) }

            File.WriteAllText(filePath, Envelope.serialize env1 + "\n" + Envelope.serialize env2)
            let snapshot = Boot.boot dir
            Assert.Empty(snapshot.Diagnostics)
            Assert.Equal(2, snapshot.Envelopes.Length)
            Assert.Equal(EventId.create "e1", snapshot.Envelopes.[0].EventId)
            Assert.Equal(EventId.create "e2", snapshot.Envelopes.[1].EventId))
