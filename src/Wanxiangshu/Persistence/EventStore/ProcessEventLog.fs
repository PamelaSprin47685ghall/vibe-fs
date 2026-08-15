namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open System
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

    [<Emit("$0.digest('hex')")>]
    let private hashHex (hash: obj) : string = jsNative

    let private wanxiangDirectory commonDir = join2 commonDir "wanxiang"

    let private eventsDirectory commonDir =
        join2 (wanxiangDirectory commonDir) "events"

    let private payloadsDirectory commonDir =
        join2 (wanxiangDirectory commonDir) "payloads"

    let private ensureDirectory path =
        mkdirSync path (createObj [ "recursive" ==> true ])

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

    let private writerFileNames commonDir =
        let directory = eventsDirectory commonDir

        if not (existsSync directory) then
            []
        else
            readdirSync directory
            |> Array.filter (fun name -> name.EndsWith(".ndjson", StringComparison.Ordinal))
            |> Array.sort
            |> Array.toList

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

    /// Replace/import one writer file only when the incoming bytes extend the
    /// local complete-line prefix. Divergence is fail-closed physical corruption.
    let mergeWriterText (commonDir: string) (writerId: string) (incoming: string) : Result<unit, string> =
        let writer = safeWriterId writerId
        let directory = eventsDirectory commonDir
        ensureDirectory directory
        let path = join2 directory (writer + ".ndjson")
        let existing = if existsSync path then readTextFileSync path "utf8" else ""

        if existing = incoming || incoming.StartsWith(existing, StringComparison.Ordinal) then
            writeExtendedWriter path existing incoming
            Ok()
        elif existing.StartsWith(incoming, StringComparison.Ordinal) then
            Ok()
        else
            Error(sprintf "writer history diverged: %s" writer)

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
        readPayload commonDir payloadRef |> Option.isSome

    let readPayloadFiles (commonDir: string) : (string * byte[]) list =
        let directory = payloadsDirectory commonDir
        ensureDirectory directory

        readdirSync directory
        |> Array.sort
        |> Array.toList
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
