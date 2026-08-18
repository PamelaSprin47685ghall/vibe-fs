namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Enforcer
open Wanxiangshu.Foundation
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.Persistence

open System
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling

/// DURABLE-CONVERGENCE-002/003/007/008.
/// Sync is deliberately physical: complete WriterId NDJSON files and payload
/// files in, Git blobs/tree out. It never interprets domain state.
[<RequireQualifiedAccess>]
module WriterStreamSync =

    let private blobMode = "100644"
    let private treeMode = "40000"

    [<Literal>]
    let private materializationCacheVersion = "v2"

    type private CachedFile =
        { StatIdentity: string
          Oid: GitObjectId }

    type private MaterializationCache =
        { Fingerprint: string
          Root: StoreSnapshot
          Writers: Map<string, CachedFile>
          Payloads: Map<string, CachedFile> }

    [<Import("join", "node:path")>]
    let private joinPath (left: string) (right: string) : string = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readTextFileSync (path: string) (encoding: string) : string = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeTextFileSync (path: string) (content: string) (encoding: string) : unit = jsNative

    [<Emit("Buffer.from($0, 'utf8').toString('base64url')")>]
    let private encodeFileName (name: string) : string = jsNative

    [<Emit("Buffer.from($0, 'base64url').toString('utf8')")>]
    let private decodeFileName (name: string) : string = jsNative

    let private asStorage reason =
        ConvergeError.StorageInvalid(StorageInvalid.NonCanonical reason)

    let private materializationCachePath commonDir =
        joinPath (joinPath commonDir "wanxiang") "sync-materialization-cache"

    let private validHex length (value: string) =
        value.Length = length
        && value |> Seq.forall (fun ch -> Char.IsDigit ch || (ch >= 'a' && ch <= 'f'))

    let private parseCacheEntry state (line: string) =
        match state with
        | None -> None
        | Some(writers, payloads) ->
            match line.Split('\t') with
            | [| kind; encodedName; statIdentity; oid |] when validHex 40 oid ->
                let file =
                    { StatIdentity = statIdentity
                      Oid = GitObjectId.create oid }

                match kind with
                | "w" -> Some(Map.add (decodeFileName encodedName) file writers, payloads)
                | "p" -> Some(writers, Map.add (decodeFileName encodedName) file payloads)
                | _ -> None
            | _ -> None

    let private cacheFromText (text: string) =
        let lines = text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)

        if lines.Length = 0 then
            None
        else
            match lines.[0].Split('\t') with
            | [| version; fingerprint; root |]
                when version = materializationCacheVersion && validHex 64 fingerprint && validHex 40 root ->
                lines
                |> Array.skip 1
                |> Array.fold parseCacheEntry (Some(Map.empty, Map.empty))
                |> Option.map (fun (writers, payloads) ->
                    { Fingerprint = fingerprint
                      Root = { RootOid = RootOid.create (GitObjectId.create root) }
                      Writers = writers
                      Payloads = payloads })
            | _ -> None

    let private readMaterializationCache commonDir =
        try
            let path = materializationCachePath commonDir

            if existsSync path then
                readTextFileSync path "utf8" |> cacheFromText
            else
                None
        with _ ->
            None

    let private tryCachedLocal commonDir =
        let fingerprint = ProcessEventLog.physicalFingerprint commonDir

        readMaterializationCache commonDir
        |> Option.filter (fun cache -> cache.Fingerprint = fingerprint)

    let private cacheFileLine kind statByName (entry: TreeEntry) =
        let statIdentity = Map.find entry.Name statByName

        String.concat
            "\t"
            [ kind
              encodeFileName entry.Name
              statIdentity
              GitObjectId.value entry.Oid ]

    let private writeMaterializationCache
        commonDir
        (snapshot: StoreSnapshot)
        writerStats
        writerEntries
        payloadStats
        payloadEntries
        =
        try
            let fingerprint = ProcessEventLog.physicalFingerprint commonDir
            let root = RootOid.value snapshot.RootOid |> GitObjectId.value
            let writerStatByName = Map.ofList writerStats
            let payloadStatByName = Map.ofList payloadStats

            let body =
                [ yield String.concat "\t" [ materializationCacheVersion; fingerprint; root ]
                  yield! writerEntries |> List.map (cacheFileLine "w" writerStatByName)
                  yield! payloadEntries |> List.map (cacheFileLine "p" payloadStatByName) ]
                |> String.concat "\n"
                |> fun value -> value + "\n"

            writeTextFileSync (materializationCachePath commonDir) body "utf8"
        with _ ->
            ()

    let private sameRoot (left: StoreSnapshot) (right: StoreSnapshot) =
        RootOid.value left.RootOid = RootOid.value right.RootOid

    let private cachedOid cacheFiles name statIdentity =
        cacheFiles
        |> Map.tryFind name
        |> Option.bind (fun cached ->
            if cached.StatIdentity = statIdentity then
                Some cached.Oid
            else
                None)

    let private materializeFileEntries
        (raw: IGitRawStore)
        (readBytes: string -> byte[])
        (cacheFiles: Map<string, CachedFile>)
        (files: (string * string) list)
        : Task<TreeEntry list> =
        let rec loop remaining acc =
            task {
                match remaining with
                | [] -> return List.rev acc
                | (name, statIdentity) :: tail ->
                    let! oid =
                        match cachedOid cacheFiles name statIdentity with
                        | Some oid -> Task.FromResult oid
                        | None -> raw.WriteBlob(readBytes name)

                    return!
                        loop
                            tail
                            ({ Mode = blobMode
                               Name = name
                               Oid = oid }
                             :: acc)
            }

        loop files []

    let private cacheFiles selector cache =
        cache |> Option.map selector |> Option.defaultValue Map.empty

    let materializeLocal (raw: IGitRawStore) (commonDir: string) : Task<StoreSnapshot> =
        task {
            let cache = readMaterializationCache commonDir
            let writerStats = ProcessEventLog.writerPhysicalStats commonDir
            let payloadStats = ProcessEventLog.payloadPhysicalStats commonDir

            let! writerEntries =
                materializeFileEntries
                    raw
                    (ProcessEventLog.readWriterFileBytes commonDir)
                    (cacheFiles (fun value -> value.Writers) cache)
                    writerStats

            let! payloadEntries =
                materializeFileEntries
                    raw
                    (ProcessEventLog.readPayloadFileBytes commonDir)
                    (cacheFiles (fun value -> value.Payloads) cache)
                    payloadStats

            let! writers = raw.WriteTree writerEntries
            let! payloads = raw.WriteTree payloadEntries

            let! root =
                raw.WriteTree
                    [ { Mode = treeMode
                        Name = "writers"
                        Oid = writers }
                      { Mode = treeMode
                        Name = "payloads"
                        Oid = payloads } ]

            let snapshot = { RootOid = RootOid.create root }
            writeMaterializationCache commonDir snapshot writerStats writerEntries payloadStats payloadEntries
            return snapshot
        }

    let private materializeAndCache raw commonDir =
        materializeLocal raw commonDir

    let private readRequiredTree (raw: IGitRawStore) (oid: GitObjectId) (label: string) =
        task {
            match! raw.ReadTree oid with
            | Some entries -> return Ok entries
            | None -> return Error(asStorage (sprintf "missing %s tree" label))
        }

    let private requireBlobMode (entry: TreeEntry) =
        if entry.Mode = blobMode then
            Ok()
        else
            Error(asStorage (sprintf "sync leaf is not a blob: %s" entry.Name))

    let private readBlobEntry (raw: IGitRawStore) (entry: TreeEntry) =
        taskResult {
            do! requireBlobMode entry
            let! bytesOpt = TaskResultCE.ofTask (raw.ReadObject entry.Oid)

            match bytesOpt with
            | None -> return! Error(asStorage (sprintf "missing sync blob: %s" entry.Name))
            | Some bytes -> return entry.Name, bytes
        }

    let private readBlobList
        (raw: IGitRawStore)
        (entries: TreeEntry list)
        : Task<Result<(string * byte[]) list, ConvergeError>> =
        entries |> TaskResultList.traverseM (readBlobEntry raw)

    let private writerTextFromBlob (name: string, bytes: byte[]) =
        if not (name.EndsWith(".ndjson", StringComparison.Ordinal)) then
            raise (InvalidOperationException(sprintf "invalid writer filename: %s" name))

        let writerId = name.Substring(0, name.Length - ".ndjson".Length)
        writerId, Encoding.UTF8.GetString bytes

    let private remoteEntryNeeded cacheFiles currentStats (entry: TreeEntry) =
        match Map.tryFind entry.Name cacheFiles, Map.tryFind entry.Name currentStats with
        | Some cached, Some currentStat
            when entry.Mode = blobMode
                 && cached.StatIdentity = currentStat
                 && cached.Oid = entry.Oid ->
            false
        | _ -> true

    let private changedRemoteEntries cacheFiles currentStats entries =
        entries |> List.filter (remoteEntryNeeded cacheFiles currentStats)

    let private readRemoteTrees
        (raw: IGitRawStore)
        (cache: MaterializationCache option)
        (commonDir: string)
        (writerTree: TreeEntry)
        (payloadTree: TreeEntry)
        =
        taskResult {
            let! writerEntries = readRequiredTree raw writerTree.Oid "writers"
            let writerStats = ProcessEventLog.writerPhysicalStats commonDir |> Map.ofList

            let neededWriterEntries =
                changedRemoteEntries
                    (cacheFiles (fun value -> value.Writers) cache)
                    writerStats
                    writerEntries

            let! writerBlobs = readBlobList raw neededWriterEntries
            let! payloadEntries = readRequiredTree raw payloadTree.Oid "payloads"
            let payloadStats = ProcessEventLog.payloadPhysicalStats commonDir |> Map.ofList

            let neededPayloadEntries =
                changedRemoteEntries
                    (cacheFiles (fun value -> value.Payloads) cache)
                    payloadStats
                    payloadEntries

            let! payloadBlobs = readBlobList raw neededPayloadEntries
            return List.map writerTextFromBlob writerBlobs, payloadBlobs
        }

    let private readRemote
        (raw: IGitRawStore)
        (cache: MaterializationCache option)
        (commonDir: string)
        (snapshot: StoreSnapshot)
        : Task<Result<(string * string) list * (string * byte[]) list, ConvergeError>> =
        taskResult {
            let! rootEntries = readRequiredTree raw (RootOid.value snapshot.RootOid) "root"

            let writers =
                rootEntries
                |> List.tryFind (fun entry -> entry.Name = "writers" && entry.Mode = treeMode)

            let payloads =
                rootEntries
                |> List.tryFind (fun entry -> entry.Name = "payloads" && entry.Mode = treeMode)

            match writers, payloads with
            | Some writerTree, Some payloadTree ->
                return! readRemoteTrees raw cache commonDir writerTree payloadTree
            | _ -> return! Error(asStorage "sync root must contain writers/ and payloads/")
        }

    let private decodeOneRemoteWriter (writerId: string, text: string) =
        ProcessEventLog.decodeWriterText ("remote:" + writerId) text
        |> Result.mapError ConvergeError.StorageInvalid
        |> Result.map (fun events -> "remote:" + writerId, events)

    let private decodeRemoteWriters
        (writers: (string * string) list)
        : Result<(string * Wanxiangshu.Persistence.EventStore.EventEnvelope list) list, ConvergeError> =
        writers |> List.traverseResultM decodeOneRemoteWriter

    let private missingPayloadRef (commonDir: string) (ordered: EventEnvelope list) =
        ordered
        |> List.collect (fun event -> event.PayloadRefs)
        |> List.tryFind (ProcessEventLog.payloadExists commonDir >> not)

    let private validateMergedStreams
        (commonDir: string)
        (local: (string * EventEnvelope list) list)
        (remote: (string * EventEnvelope list) list)
        =
        let taggedLocal =
            local |> List.map (fun (writerId, events) -> "local:" + writerId, events)

        result {
            let! ordered =
                EventKWayMerge.merge (taggedLocal @ remote)
                |> Result.mapError ConvergeError.StorageInvalid

            match missingPayloadRef commonDir ordered with
            | Some payloadRef -> return! Error(ConvergeError.StorageInvalid(StorageInvalid.MissingPayload payloadRef))
            | None -> return ()
        }

    let private validateUnion
        (commonDir: string)
        (remoteWriters: (string * string) list)
        : Result<unit, ConvergeError> =
        result {
            let! local =
                ProcessEventLog.readStreams commonDir
                |> Result.mapError ConvergeError.StorageInvalid

            let! remote = decodeRemoteWriters remoteWriters
            return! validateMergedStreams commonDir local remote
        }

    let private mergeOnePayload commonDir (name, bytes) =
        ProcessEventLog.mergePayloadFile commonDir name bytes
        |> Result.mapError asStorage

    let private mergeOneWriter commonDir (writerId, text) =
        ProcessEventLog.mergeWriterText commonDir writerId text
        |> Result.mapError asStorage

    let private importRemote
        (commonDir: string)
        (writers: (string * string) list)
        (payloads: (string * byte[]) list)
        : Result<unit, ConvergeError> =
        // Payloads are content-addressed and may be safely imported before facts.
        // Validate the combined k-way history/closure before changing writer truth.
        result {
            do!
                payloads
                |> List.traverseResultM (mergeOnePayload commonDir)
                |> Result.map ignore

            do! validateUnion commonDir writers
            do! writers |> List.traverseResultM (mergeOneWriter commonDir) |> Result.map ignore
            return ()
        }

    let private syncWithoutRemote (raw: IGitRawStore) (commonDir: string) =
        taskResult {
            do! validateUnion commonDir []
            return! materializeAndCache raw commonDir |> TaskResultCE.ofTask
        }

    let private syncWithRemote (raw: IGitRawStore) (commonDir: string) (snapshot: StoreSnapshot) =
        match tryCachedLocal commonDir with
        | Some cached when sameRoot cached.Root snapshot -> taskResult { return cached.Root }
        | _ ->
            taskResult {
                let cache = readMaterializationCache commonDir
                let! writers, payloads = readRemote raw cache commonDir snapshot
                do! importRemote commonDir writers payloads
                return! materializeAndCache raw commonDir |> TaskResultCE.ofTask
            }

    let private syncRemoteChoice (raw: IGitRawStore) (commonDir: string) (remote: StoreSnapshot option) =
        match remote with
        | None -> syncWithoutRemote raw commonDir
        | Some snapshot -> syncWithRemote raw commonDir snapshot

    /// Merge remote physical truth into local writer files, then materialize the
    /// whole local truth as the candidate remote snapshot. Business integration
    /// happens only after this returns, through CanonicalIntegrator.
    let syncWriterStreams
        (raw: IGitRawStore)
        (commonDir: string)
        (remote: StoreSnapshot option)
        : Task<Result<StoreSnapshot, ConvergeError>> =
        taskResult {
            try
                return! syncRemoteChoice raw commonDir remote
            with ex ->
                return! Error(ConvergeError.Transport ex.Message)
        }
