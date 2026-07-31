namespace Wanxiangshu.Next.Journal

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Host
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

    [<Import("readFileSync", "node:fs")>]
    let readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative

type BlobWriteReceipt =
    { BlobRef: BlobRef
      BlobDigest: BlobDigest }

type IBlobWriter =
    abstract Write: string -> Result<BlobWriteReceipt, string>
    abstract Read: BlobRef -> Result<string, string>

type BlobWriter private (directory: string) =
    let writeNew (path: string) (bytes: byte array) =
        let fd = NodeFsWriter.openSync (path, "wx", 0o600)

        try
            let written = NodeFsWriter.writeSync (fd, bytes)

            if written <> bytes.Length then
                raise (InvalidOperationException "blob write was partial")

            try
                NodeFsWriter.fdatasyncSync fd
            with _ ->
                NodeFsWriter.fsyncSync fd
        finally
            NodeFsWriter.closeSync fd

    member _.Write(content: string) : Result<BlobWriteReceipt, string> =
        let digest = BlobDigest.create (HostDigest.sha256Hex content)
        let name = BlobDigest.value digest
        let blobRef = BlobRef.create (NodeFsWriter.pathJoin ("blobs", name))
        let path = NodeFsWriter.pathJoin (directory, name)
        let bytes = System.Text.Encoding.UTF8.GetBytes content

        try
            writeNew path bytes

            Ok
                { BlobRef = blobRef
                  BlobDigest = digest }
        with ex ->
            if ex.Message.Contains("EEXIST") then
                try
                    if NodeFsWriter.readFileSync (path, "utf8") = content then
                        Ok
                            { BlobRef = blobRef
                              BlobDigest = digest }
                    else
                        Error(sprintf "blob path exists with different content: %s" path)
                with readEx ->
                    Error(sprintf "existing blob unreadable: %s" readEx.Message)
            else
                Error(sprintf "blob write failed: %s" ex.Message)

    member _.Read(blobRef: BlobRef) : Result<string, string> =
        let relative = BlobRef.value blobRef
        let prefix = "blobs/"

        if not (relative.StartsWith(prefix, StringComparison.Ordinal)) then
            Error(sprintf "invalid blob reference: %s" relative)
        else
            let name = relative.Substring(prefix.Length)

            if String.IsNullOrWhiteSpace name || name.Contains "/" then
                Error(sprintf "invalid blob reference: %s" relative)
            else
                try
                    Ok(NodeFsWriter.readFileSync (NodeFsWriter.pathJoin (directory, name), "utf8"))
                with ex ->
                    Error(sprintf "blob read failed: %s" ex.Message)

    interface IBlobWriter with
        member this.Write(content) = this.Write content
        member this.Read(blobRef) = this.Read blobRef

    static member Create(parentDirectory: string) : IBlobWriter =
        let directory = NodeFsWriter.pathJoin (parentDirectory, "blobs")

        if not (NodeFsWriter.existsSync directory) then
            NodeFsWriter.mkdirSync (directory, {| recursive = true; mode = 0o700 |})

        BlobWriter(directory) :> IBlobWriter

type JournalWriter private (runtimeId: RuntimeId, blobWriter: IBlobWriter, filePath: string, fd: int) =
    let gate = obj ()
    let mutable currentSeq = 2L
    let mutable poisoned = false
    let mutable disposed = false

    member _.RuntimeId = runtimeId
    member _.BlobWriter = blobWriter
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

        let blobWriter = BlobWriter.Create directory

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

        (new JournalWriter(runtimeId, blobWriter, filePath, fd), initEnvelope)

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
