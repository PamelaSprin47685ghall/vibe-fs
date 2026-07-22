namespace Wanxiangshu.Next.Journal

open System
open System.IO
open System.Text
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome

type JournalWriter private (stream: FileStream, writer: StreamWriter, runtimeId: RuntimeId, filePath: string) =
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
        if not (Directory.Exists(directory)) then
            Directory.CreateDirectory(directory) |> ignore

        let filename = sprintf "%s.ndjson" (RuntimeId.value runtimeId)
        let filePath = Path.Combine(directory, filename)

        let fileStream =
            new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)

        let streamWriter = new StreamWriter(fileStream, UTF8Encoding(false))

        let initEventId = EventId.create (Guid.NewGuid().ToString())

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

        (new JournalWriter(fileStream, streamWriter, runtimeId, filePath), initEnvelope)

    member private this.WriteAndFlush (env: Envelope) (eventId: EventId) =
        let line = Envelope.serialize env
        let mutable err = None

        try
            writer.WriteLine(line)
        with ex ->
            poisoned <- true
            err <- Some(WriteFailed ex.Message)

        match err with
        | Some e -> CommitUnknown(eventId, e)
        | None ->
            try
                writer.Flush()
            with ex ->
                poisoned <- true
                err <- Some(FlushFailed ex.Message)

            match err with
            | Some e -> CommitUnknown(eventId, e)
            | None ->
                try
                    stream.Flush(true)
                with ex ->
                    poisoned <- true
                    err <- Some(FlushFailed ex.Message)

                match err with
                | Some e -> CommitUnknown(eventId, e)
                | None ->
                    currentSeq <- currentSeq + 1L
                    Committed env

    member this.Append (streamKind: StreamId) (turnId: TurnId option) (fact: Fact) : CommitResult<Envelope> =
        lock gate (fun () ->
            let eventId = EventId.create (Guid.NewGuid().ToString())

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
        member _.Dispose() =
            let toDispose =
                lock gate (fun () ->
                    if not disposed then
                        disposed <- true
                        true
                    else
                        false)

            if toDispose then
                try
                    writer.Dispose()
                with _ ->
                    ()

                try
                    stream.Dispose()
                with _ ->
                    ()

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            let toDispose =
                lock gate (fun () ->
                    if not disposed then
                        disposed <- true
                        true
                    else
                        false)

            ValueTask(
                task {
                    if toDispose then
                        try
                            do! writer.DisposeAsync().AsTask()
                        with _ ->
                            ()

                        try
                            do! stream.DisposeAsync().AsTask()
                        with _ ->
                            ()
                }
            )
