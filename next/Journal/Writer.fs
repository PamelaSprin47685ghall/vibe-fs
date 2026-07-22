namespace Wanxiangshu.Next.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome

#if FABLE_COMPILER
open Fable.Core
open Fable.Core.JsInterop

module private NodeFsWriter =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let mkdirSync (path: string, opts: obj) : unit = jsNative

    [<Import("appendFileSync", "node:fs")>]
    let appendFileSync (path: string, content: string) : unit = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let writeFileSync (path: string, content: string) : unit = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative
#else
open System.IO
open System.Text
#endif

type JournalWriter private (runtimeId: RuntimeId, filePath: string) =
    let gate = obj ()
    let mutable currentSeq = 2L
    let mutable poisoned = false
    let mutable disposed = false

    member _.RuntimeId = runtimeId
    member _.FilePath = filePath
    member this.LocalSeq = lock gate (fun () -> currentSeq)
    member this.LastCommittedLocalSeq = lock gate (fun () -> currentSeq - 1L)
    member this.IsPoisoned = lock gate (fun () -> poisoned)

    static member create
        (directory: string)
        (runtimeId: RuntimeId)
        (processId: int)
        (startedAt: DateTimeOffset)
        : JournalWriter * Envelope =
#if FABLE_COMPILER
        if not (NodeFsWriter.existsSync directory) then
            NodeFsWriter.mkdirSync (directory, {| recursive = true |}) |> ignore

        let filename = sprintf "%s.ndjson" (RuntimeId.value runtimeId)
        let filePath = NodeFsWriter.pathJoin (directory, filename)

        let initEventId = EventId.create (Guid.NewGuid().ToString("N"))

        let initFact =
            Fact.Runtime(
                RuntimeStarted
                    {| RuntimeId = runtimeId
                       ProcessId = processId
                       StartedAt = startedAt |}
            )

        let initEnvelope: Envelope =
            { RuntimeId = runtimeId
              LocalSeq = LocalSeq.create 1L
              ObservedAt = startedAt
              EventId = initEventId
              Stream = StreamId.Workspace
              TurnId = None
              Fact = initFact }

        let jsonLine = Envelope.serialize initEnvelope + "\n"
        NodeFsWriter.writeFileSync (filePath, jsonLine)

        (new JournalWriter(runtimeId, filePath), initEnvelope)
#else
        if not (Directory.Exists(directory)) then
            Directory.CreateDirectory(directory) |> ignore

        let filename = sprintf "%s.ndjson" (RuntimeId.value runtimeId)
        let filePath = Path.Combine(directory, filename)

        let fileStream =
            new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)

        let streamWriter = new StreamWriter(fileStream, UTF8Encoding(false))

        let initEventId = EventId.create (Guid.NewGuid().ToString("N"))

        let initFact =
            Fact.Runtime(
                RuntimeStarted
                    {| RuntimeId = runtimeId
                       ProcessId = processId
                       StartedAt = startedAt |}
            )

        let initEnvelope: Envelope =
            { RuntimeId = runtimeId
              LocalSeq = LocalSeq.create 1L
              ObservedAt = startedAt
              EventId = initEventId
              Stream = StreamId.Workspace
              TurnId = None
              Fact = initFact }

        let jsonLine = Envelope.serialize initEnvelope
        streamWriter.WriteLine(jsonLine)
        streamWriter.Flush()
        fileStream.Flush(true)

        (new JournalWriter(runtimeId, filePath), initEnvelope)
#endif

    member private this.WriteAndFlush (env: Envelope) (eventId: EventId) =
        let line = Envelope.serialize env
#if FABLE_COMPILER
        try
            NodeFsWriter.appendFileSync (filePath, line + "\n")
            currentSeq <- currentSeq + 1L
            Committed env
        with ex ->
            poisoned <- true
            CommitUnknown(eventId, WriteFailed ex.Message)
#else
        try
            File.AppendAllText(filePath, line + "\n")
            currentSeq <- currentSeq + 1L
            Committed env
        with ex ->
            poisoned <- true
            CommitUnknown(eventId, WriteFailed ex.Message)
#endif

    member this.Append (streamKind: StreamId) (turnId: TurnId option) (fact: Fact) : CommitResult<Envelope> =
        lock gate (fun () ->
            let eventId = EventId.create (Guid.NewGuid().ToString("N"))

            if poisoned || disposed then
                CommitUnknown(eventId, WriteFailed "Writer is poisoned or disposed")
            else
                let env: Envelope =
                    { RuntimeId = runtimeId
                      LocalSeq = LocalSeq.create currentSeq
                      ObservedAt = DateTimeOffset.UtcNow
                      EventId = eventId
                      Stream = streamKind
                      TurnId = turnId
                      Fact = fact }

                this.WriteAndFlush env eventId)

    interface IDisposable with
        member _.Dispose() = lock gate (fun () -> disposed <- true)

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            unbox<ValueTask> (task { lock gate (fun () -> disposed <- true) })
