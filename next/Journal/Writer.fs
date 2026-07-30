namespace Wanxiangshu.Next.Journal

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome

module private NodeFsWriter =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let mkdirSync (path: string, opts: obj) : unit = jsNative

    [<Import("openSync", "node:fs")>]
    let openSync (path: string, flags: string, mode: int) : int = jsNative

    [<Import("writeSync", "node:fs")>]
    let writeSync (fd: int, buffer: obj) : int = jsNative

    [<Import("fdatasyncSync", "node:fs")>]
    let fdatasyncSync (fd: int) : unit = jsNative

    [<Import("fsyncSync", "node:fs")>]
    let fsyncSync (fd: int) : unit = jsNative

    [<Import("closeSync", "node:fs")>]
    let closeSync (fd: int) : unit = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative

type JournalWriter private (runtimeId: RuntimeId, filePath: string, fd: int) =
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
        if not (NodeFsWriter.existsSync directory) then
            // PERSIST-006: 0700 on the runtime directory. `mode` must be passed at
            // creation rather than chmod'ed after — between mkdir and chmod the
            // directory is world-readable, and a journal line names sessions and
            // Git trees.
            NodeFsWriter.mkdirSync (directory, {| recursive = true; mode = 0o700 |})
            |> ignore

        let filename = sprintf "%s.ndjson" (RuntimeId.value runtimeId)
        let filePath = NodeFsWriter.pathJoin (directory, filename)

        // PERSIST-006: 0600 on the journal file. `wx` additionally makes a second
        // writer for one RuntimeId fail with EEXIST instead of reopening the file
        // and interleaving two LocalSeq sequences into it.
        let fd = NodeFsWriter.openSync (filePath, "wx", 0o600)

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
              ProviderRun = None
              Fact = initFact }

        let jsonLine = Envelope.serialize initEnvelope + "\n"
        let bytes = System.Text.Encoding.UTF8.GetBytes(jsonLine)
        NodeFsWriter.writeSync (fd, bytes) |> ignore

        try
            NodeFsWriter.fdatasyncSync fd
        with _ ->
            NodeFsWriter.fsyncSync fd

        (new JournalWriter(runtimeId, filePath, fd), initEnvelope)

    member private this.WriteAndFlush (env: Envelope) (eventId: EventId) =
        let line = Envelope.serialize env + "\n"
        let bytes = System.Text.Encoding.UTF8.GetBytes(line)

        try
            NodeFsWriter.writeSync (fd, bytes) |> ignore

            try
                NodeFsWriter.fdatasyncSync fd
            with _ ->
                NodeFsWriter.fsyncSync fd

            currentSeq <- currentSeq + 1L
            Committed env
        with ex ->
            poisoned <- true
            CommitUnknown(eventId, WriteFailed ex.Message)

    /// PERSIST-002: append yields Committed or CommitUnknown. There is no
    /// partial write, so there is no third result to return.
    ///
    /// `providerRun` is the run this fact was observed during, when there was
    /// one. Facts belonging to no run — runtime start, worktree creation, a
    /// Manager job's lifecycle — pass None.
    member this.Append
        (streamKind: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : CommitResult<Envelope> =
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
                      ProviderRun = providerRun
                      Fact = fact }

                this.WriteAndFlush env eventId)

    member private this.DisposeInternal() =
        lock gate (fun () ->
            if not disposed then
                disposed <- true

                try
                    NodeFsWriter.closeSync fd
                with _ ->
                    ())

    interface IDisposable with
        member this.Dispose() = this.DisposeInternal()

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            this.DisposeInternal()
            Fable.Core.JS.Constructors.Promise.resolve () |> unbox<ValueTask>
