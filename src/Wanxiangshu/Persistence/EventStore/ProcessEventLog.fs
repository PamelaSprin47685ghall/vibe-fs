namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open System
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// DURABLE-EVENTS-004/005/010/017.
/// Runtime truth is deliberately boring: one process owns one append-only NDJSON
/// file under the repository's git common-dir. Git object plumbing is not a
/// dependency of this module and therefore cannot leak into the local append path.
type ProcessEventLog =
    private
        { CommonDir: string
          WriterId: string
          FilePath: string }

type StoreFileGate internal (releaseFn: obj) =
    // DSL-MUTABLE: resource — physical one-shot release latch; not domain state.
    let mutable released = false

    member _.Release() =
        task {
            if not released then
                released <- true
                let release: unit -> Task<unit> = unbox releaseFn
                do! release ()
        }

[<RequireQualifiedAccess>]
module ProcessEventLog =

    [<Import("default", "proper-lockfile")>]
    let private lockfile: obj = jsNative

    [<Emit("$0($1, $2)")>]
    let private lockAsync (fn: obj) (path: string) (options: obj) : Task<obj> = jsNative

    [<Import("join", "node:path")>]
    let private join2 (a: string) (b: string) : string = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let private mkdirSync (path: string) (options: obj) : unit = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeTextFileSync (path: string) (content: string) (encoding: string) : unit = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeBytesFileSync (path: string) (content: byte[]) : unit = jsNative

    [<Import("appendFileSync", "node:fs")>]
    let private AppendAllText (path: string) (content: string) (encoding: string) : unit = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readTextFileSync (path: string) (encoding: string) : string = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readBytesFileSync (path: string) : byte[] = jsNative

    [<Import("readdirSync", "node:fs")>]
    let private readdirSync (path: string) : string[] = jsNative

    [<Import("statSync", "node:fs")>]
    let private statSync (path: string) : obj = jsNative

    [<Import("readSync", "node:fs")>]
    let private readSync (fd: int) (buffer: byte[]) (offset: int) (length: int) (position: int) : int = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("utimesSync", "node:fs")>]
    let private utimesSync (path: string) (atime: float) (mtime: float) : unit = jsNative

    [<Import("openSync", "node:fs")>]
    let private openSync (path: string) (flags: string) : int = jsNative

    [<Import("fsyncSync", "node:fs")>]
    let private fsyncSync (fd: int) : unit = jsNative

    [<Import("closeSync", "node:fs")>]
    let private closeSync (fd: int) : unit = jsNative

    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    [<Emit("$0.update($1)")>]
    let private hashUpdate (hash: obj) (content: byte[]) : obj = jsNative

    [<Emit("$0.update($1, 'utf8')")>]
    let private hashUpdateText (hash: obj) (content: string) : obj = jsNative

    [<Emit("[$0.dev, $0.ino, $0.mode, $0.size, $0.mtimeMs, $0.ctimeMs].join(':')")>]
    let private statIdentity (stat: obj) : string = jsNative

    [<Emit("$0.mtimeMs")>]
    let private statMtimeMs (stat: obj) : float = jsNative

    [<Emit("$0.size")>]
    let private statSize (stat: obj) : int = jsNative

    [<Emit("$0.digest('hex')")>]
    let private hashHex (hash: obj) : string = jsNative

    let private wanxiangDirectory commonDir = join2 commonDir "wanxiang"

    let private eventsDirectory commonDir =
        join2 (wanxiangDirectory commonDir) "events"

    let private payloadsDirectory commonDir =
        join2 (wanxiangDirectory commonDir) "payloads"

    let private ensureDirectory path =
        mkdirSync path (createObj [ "recursive" ==> true ])

    type WriterPhysicalMetadata =
        { Name: string
          StatIdentity: string
          LastActivityMs: float }

    /// Cross-process physical serialization shared by runtime append and the
    /// standalone Git-hook synchronizer. It protects bytes/snapshot boundaries
    /// only; it is not durable/domain state and contains no history meaning.
    let acquireStoreLock (commonDir: string) : Task<StoreFileGate> =
        task {
            let target = wanxiangDirectory commonDir
            ensureDirectory target

            let! release =
                lockAsync
                    lockfile
                    target
                    (createObj
                        [ "realpath" ==> false
                          "retries"
                          ==> createObj
                                  [ "forever" ==> true
                                    "minTimeout" ==> 20
                                    "maxTimeout" ==> 200
                                    "factor" ==> 1 ] ])

            return new StoreFileGate(release)
        }

    /// Resource CE for the physical cross-process gate. Completion of the
    /// returned Task includes completion of proper-lockfile release; no
    /// heartbeat/retry handle may escape the operation boundary.
    let withStoreLock (commonDir: string) (work: unit -> Task<'T>) : Task<'T> =
        task {
            let! gate = acquireStoreLock commonDir

            try
                let! result = work ()
                do! gate.Release()
                return result
            with ex ->
                do! gate.Release()
                return raise ex
        }

    let private safeWriterId (writerId: string) =
        if String.IsNullOrWhiteSpace writerId then
            invalidArg "writerId" "WriterId is required"
        elif writerId.IndexOfAny([| '/'; '\\' |]) >= 0 || writerId = "." || writerId = ".." then
            invalidArg "writerId" "WriterId must be one path segment"
        else
            writerId

    let create (commonDir: string) (writerId: string) : ProcessEventLog =
        if String.IsNullOrWhiteSpace commonDir then
            invalidArg "commonDir" "git common-dir is required"

        let writer = safeWriterId writerId
        let path = join2 (eventsDirectory commonDir) (writer + ".ndjson")

        { CommonDir = commonDir
          WriterId = writer
          FilePath = path }

    let writerId (log: ProcessEventLog) = log.WriterId

    let filePath (log: ProcessEventLog) = log.FilePath

    let private durabilityBarrier path =
        let fd = openSync path "r+"

        try
            fsyncSync fd
        finally
            closeSync fd

    /// One semantic append = one sequence of complete canonical JSON+LF lines.
    /// Existing bytes are never read or rewritten.
    let append (log: ProcessEventLog) (events: EventEnvelope list) : unit =
        let text =
            events
            |> List.map (EventEnvelope.normalize >> CanonicalEventCodec.encode)
            |> String.concat ""

        if text.Length > 0 then
            ensureDirectory (eventsDirectory log.CommonDir)
            AppendAllText log.FilePath text "utf8"
            durabilityBarrier log.FilePath

    let private decodeWriterLine (label: string) (line: string) : Result<EventEnvelope, StorageInvalid> =
        if String.IsNullOrEmpty line then
            Error(StorageInvalid.NonCanonical(sprintf "writer file contains empty interior line: %s" label))
        else
            CanonicalEventCodec.tryDecode (line + "\n")

    let private decodeWriterLines (label: string) (lines: string[]) : Result<EventEnvelope list, StorageInvalid> =
        // DSL-MUTABLE: algorithm-scratch — stack depth must not scale with writer history length.
        let mutable acc: EventEnvelope list = []
        let mutable failure: StorageInvalid option = None
        // DSL-MUTABLE: algorithm-scratch — writer-line cursor
        let mutable index = 0

        let advance () =
            match decodeWriterLine label lines.[index] with
            | Ok envelope ->
                acc <- envelope :: acc
                index <- index + 1
            | Error invalid -> failure <- Some invalid

        while index < lines.Length - 1 && failure.IsNone do
            advance ()

        match failure with
        | Some invalid -> Error invalid
        | None -> Ok(List.rev acc)

    let decodeWriterText (label: string) (text: string) : Result<EventEnvelope list, StorageInvalid> =
        if String.IsNullOrEmpty text then
            Ok []
        elif not (text.EndsWith("\n", StringComparison.Ordinal)) then
            Error(StorageInvalid.NonCanonical(sprintf "writer file has incomplete trailing line: %s" label))
        else
            decodeWriterLines label (text.Split('\n'))

    let private decodeFile (path: string) : Result<EventEnvelope list, StorageInvalid> =
        decodeWriterText path (readTextFileSync path "utf8")

    let private lastIndexOfLf (buffer: byte[]) count =
        // DSL-MUTABLE: algorithm-scratch — reverse byte cursor inside one pread block.
        let mutable index = count - 1
        // DSL-MUTABLE: algorithm-scratch — exact delimiter position, -1 when absent.
        let mutable found = -1

        while index >= 0 && found < 0 do
            if buffer.[index] = 10uy then
                found <- index
            else
                index <- index - 1

        found

    let private readExactAt fd position length =
        let bytes = Array.zeroCreate<byte> length
        // DSL-MUTABLE: algorithm-scratch — bytes already filled by positional reads.
        let mutable filled = 0

        while filled < length do
            let count = readSync fd bytes filled (length - filled) (position + filled)

            if count <= 0 then
                failwith "unexpected EOF while reading writer tail"

            filled <- filled + count

        bytes

    /// Exact O(last-line-bytes) NDJSON tail lookup. The scan searches raw LF bytes
    /// backwards with positional reads; UTF-8 boundaries do not matter because an
    /// unescaped LF cannot occur inside a valid JSON string.
    let readLastCompleteLine (path: string) : Result<string option, string> =
        if not (existsSync path) then
            Ok None
        else
            let size = statSync path |> statSize

            if size = 0 then
                Ok None
            else
                let fd = openSync path "r"

                try
                    let trailing = readExactAt fd (size - 1) 1

                    if trailing.[0] <> 10uy then
                        Error(sprintf "writer file has incomplete trailing line: %s" path)
                    else
                        let lineEnd = size - 1

                        if lineEnd = 0 then
                            Error(sprintf "writer file contains empty final line: %s" path)
                        else
                            let blockSize = 4096
                            // DSL-MUTABLE: algorithm-scratch — exclusive end of next reverse pread.
                            let mutable cursor = lineEnd
                            // DSL-MUTABLE: algorithm-scratch — exact line start once previous LF is found.
                            let mutable lineStart = -1

                            while cursor > 0 && lineStart < 0 do
                                let start = max 0 (cursor - blockSize)
                                let length = cursor - start
                                let buffer = readExactAt fd start length
                                let delimiter = lastIndexOfLf buffer length

                                if delimiter >= 0 then
                                    lineStart <- start + delimiter + 1
                                elif start = 0 then
                                    lineStart <- 0
                                else
                                    cursor <- start

                            let length = lineEnd - lineStart

                            if length <= 0 then
                                Error(sprintf "writer file contains empty final line: %s" path)
                            else
                                readExactAt fd lineStart length
                                |> Encoding.UTF8.GetString
                                |> Some
                                |> Ok
                with ex ->
                    Error ex.Message
                finally
                    closeSync fd

    let private writerFileNames commonDir =
        let directory = eventsDirectory commonDir

        if not (existsSync directory) then
            []
        else
            readdirSync directory
            |> Array.filter (fun name -> name.EndsWith(".ndjson", StringComparison.Ordinal))
            |> Array.sort
            |> Array.toList

    let private payloadFileNames commonDir =
        let directory = payloadsDirectory commonDir

        if not (existsSync directory) then
            []
        else
            readdirSync directory |> Array.sort |> Array.toList

    let private fingerprintFiles hash label directory names =
        names
        |> List.iter (fun name ->
            let identity = statSync (join2 directory name) |> statIdentity

            hashUpdateText hash (label + "\u0000" + name + "\u0000" + identity + "\n")
            |> ignore)

    let private physicalStats directory names =
        names
        |> List.map (fun name -> name, statSync (join2 directory name) |> statIdentity)

    let private writerMetadata directory names =
        names
        |> List.map (fun name ->
            let stat = statSync (join2 directory name)

            { Name = name
              StatIdentity = statIdentity stat
              LastActivityMs = statMtimeMs stat })

    /// Git-index-style physical cache key. It never replaces canonical validation:
    /// a cache miss falls back to reading bytes, while a hit only reuses a snapshot
    /// that has already been validated/materialized for the exact same file stats.
    let physicalFingerprint (commonDir: string) : string =
        let hash = createHash "sha256"
        fingerprintFiles hash "writers" (eventsDirectory commonDir) (writerFileNames commonDir)
        fingerprintFiles hash "payloads" (payloadsDirectory commonDir) (payloadFileNames commonDir)
        hashHex hash

    let writerPhysicalStats (commonDir: string) : (string * string) list =
        writerFileNames commonDir |> physicalStats (eventsDirectory commonDir)

    let writerPhysicalMetadata (commonDir: string) : WriterPhysicalMetadata list =
        writerFileNames commonDir |> writerMetadata (eventsDirectory commonDir)

    let payloadPhysicalStats (commonDir: string) : (string * string) list =
        payloadFileNames commonDir |> physicalStats (payloadsDirectory commonDir)

    let readWriterFileBytes (commonDir: string) (name: string) : byte[] =
        readBytesFileSync (join2 (eventsDirectory commonDir) name)

    let readPayloadFileBytes (commonDir: string) (name: string) : byte[] =
        readBytesFileSync (join2 (payloadsDirectory commonDir) name)

    /// Frozen writer files as exact UTF-8 text. Sync owns bytes, not event meaning.
    let readWriterTexts (commonDir: string) : (string * string) list =
        writerFileNames commonDir
        |> List.map (fun name ->
            let writer = name.Substring(0, name.Length - ".ndjson".Length)
            writer, readTextFileSync (join2 (eventsDirectory commonDir) name) "utf8")

    let private writeExtendedWriter (path: string) (existing: string) (incoming: string) =
        if incoming.Length > existing.Length then
            writeTextFileSync path incoming "utf8"
            durabilityBarrier path

    let private setWriterActivity path activityMs =
        let seconds = activityMs / 1000.0
        utimesSync path seconds seconds

    let private currentWriterActivity path = statSync path |> statMtimeMs

    let private reconcileEqualWriterActivity path incomingActivity =
        match incomingActivity with
        | None -> ()
        | Some remoteActivity ->
            // Equal bytes describe the same process output. Resolve legacy/fetch
            // metadata discrepancies monotonically toward the earlier observation;
            // ordinary snapshots already carry the identical value.
            setWriterActivity path (min (currentWriterActivity path) remoteActivity)

    /// Merge one whole writer while preserving the producer's activity time from
    /// the remote snapshot. A fetch/import must never make an old writer look new.
    let mergeWriterTextWithActivity
        (commonDir: string)
        (writerId: string)
        (incoming: string)
        (incomingActivityMs: float option)
        : Result<unit, string> =
        let writer = safeWriterId writerId
        let directory = eventsDirectory commonDir
        ensureDirectory directory
        let path = join2 directory (writer + ".ndjson")
        let exists = existsSync path
        let existing = if exists then readTextFileSync path "utf8" else ""

        if existing = incoming then
            if exists then
                reconcileEqualWriterActivity path incomingActivityMs

            Ok()
        elif incoming.StartsWith(existing, StringComparison.Ordinal) then
            writeExtendedWriter path existing incoming
            incomingActivityMs |> Option.iter (setWriterActivity path)
            Ok()
        elif existing.StartsWith(incoming, StringComparison.Ordinal) then
            Ok()
        else
            Error(sprintf "writer history diverged: %s" writer)

    /// Replace/import one writer file only when the incoming bytes extend the
    /// local complete-line prefix. Divergence is fail-closed physical corruption.
    let mergeWriterText (commonDir: string) (writerId: string) (incoming: string) : Result<unit, string> =
        mergeWriterTextWithActivity commonDir writerId incoming None

    let removeWriterFile (commonDir: string) (name: string) : unit =
        if name.EndsWith(".ndjson", StringComparison.Ordinal) then
            let path = join2 (eventsDirectory commonDir) name

            if existsSync path then
                unlinkSync path
        else
            invalidArg "name" "writer filename must end in .ndjson"

    /// Frozen writer streams, sorted only by WriterId for deterministic enumeration.
    /// The canonical Integrator owns cross-stream ordering and interpretation.
    let readStreams (commonDir: string) : Result<(string * EventEnvelope list) list, StorageInvalid> =
        let rec read remaining acc =
            result {
                match remaining with
                | [] -> return List.rev acc
                | name :: tail ->
                    let path = join2 (eventsDirectory commonDir) name
                    let! events = decodeFile path
                    let writer = name.Substring(0, name.Length - ".ndjson".Length)
                    return! read tail ((writer, events) :: acc)
            }

        read (writerFileNames commonDir) []

    let private payloadDigest (content: byte[]) =
        createHash "sha256" |> fun hash -> hashUpdate hash content |> hashHex

    let private ensurePayloadBytes path digest content =
        if not (existsSync path) then
            writeBytesFileSync path content
            durabilityBarrier path
        elif readBytesFileSync path <> content then
            failwith (sprintf "payload digest collision: %s" digest)

    let writePayload (commonDir: string) (content: byte[]) : PayloadRef =
        let directory = payloadsDirectory commonDir
        ensureDirectory directory
        let digest = payloadDigest content
        let path = join2 directory digest
        ensurePayloadBytes path digest content
        PayloadRef.create digest

    let readPayload (commonDir: string) (payloadRef: PayloadRef) : byte[] option =
        let path = join2 (payloadsDirectory commonDir) (PayloadRef.value payloadRef)

        if existsSync path then
            Some(readBytesFileSync path)
        else
            None

    let payloadExists commonDir payloadRef =
        existsSync (join2 (payloadsDirectory commonDir) (PayloadRef.value payloadRef))

    let readPayloadFiles (commonDir: string) : (string * byte[]) list =
        let directory = payloadsDirectory commonDir
        ensureDirectory directory

        payloadFileNames commonDir
        |> List.map (fun name -> name, readBytesFileSync (join2 directory name))

    let private writeOrReusePayload path name content =
        if not (existsSync path) then
            writeBytesFileSync path content
            durabilityBarrier path
            Ok()
        elif readBytesFileSync path = content then
            Ok()
        else
            Error(sprintf "payload identity collision: %s" name)

    let mergePayloadFile (commonDir: string) (name: string) (content: byte[]) : Result<unit, string> =
        result {
            do!
                if String.IsNullOrWhiteSpace name || name.IndexOfAny([| '/'; '\\' |]) >= 0 then
                    Error "invalid payload filename"
                else
                    Ok()

            let expected = payloadDigest content

            do!
                if expected <> name then
                    Error(sprintf "payload filename/digest mismatch: %s" name)
                else
                    Ok()

            let directory = payloadsDirectory commonDir
            ensureDirectory directory
            let path = join2 directory name
            return! writeOrReusePayload path name content
        }
