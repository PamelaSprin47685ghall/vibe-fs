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

/// DURABLE-CONVERGENCE-002/003/007/008/011.
/// Sync is deliberately physical: complete WriterId NDJSON files and payload
/// files in, Git blobs/tree out. It never interprets domain state.
[<RequireQualifiedAccess>]
module WriterStreamSync =

    let private blobMode = "100644"
    let private treeMode = "40000"

    [<Literal>]
    let private materializationCacheVersion = "v4"

    [<Literal>]
    let private writerManifestVersion = "v2"

    type private CachedFile =
        { StatIdentity: string
          Oid: GitObjectId
          LastActivityMs: float option }

    type private MaterializationCache =
        { Fingerprint: string
          Root: StoreSnapshot
          NextExpiryMs: float option
          Writers: Map<string, CachedFile>
          Payloads: Map<string, CachedFile> }

    type private MaterializedWriter =
        { Entry: TreeEntry
          StatIdentity: string
          LastActivityMs: float }

    type private WriterManifestEntry =
        { BlobOid: GitObjectId
          LastActivityMs: float }

    type private RemoteWriter =
        { WriterId: string
          Text: string
          LastActivityMs: float option }

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

    [<Emit("Date.now()")>]
    let private currentTimeMs () : float = jsNative

    [<Emit("Number($0)")>]
    let private numberOfString (value: string) : float = jsNative

    [<Emit("Number.isFinite($0)")>]
    let private isFiniteNumber (value: float) : bool = jsNative

    [<Emit("String($0)")>]
    let private numberText (value: float) : string = jsNative

    let retentionMilliseconds () =
        ProcessEventLog.writerRetentionMilliseconds ()

    let isWriterActiveAt nowMs lastActivityMs =
        ProcessEventLog.isWriterActiveAt nowMs lastActivityMs

    let private asStorage reason =
        ConvergeError.StorageInvalid(StorageInvalid.NonCanonical reason)

    let private materializationCachePath commonDir =
        joinPath (joinPath commonDir "wanxiang") "sync-materialization-cache"

    let private validHex length (value: string) =
        value.Length = length
        && value |> Seq.forall (fun ch -> Char.IsDigit ch || (ch >= 'a' && ch <= 'f'))

    let private tryParseFiniteFloat (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            let parsed = numberOfString value
            if isFiniteNumber parsed then Some parsed else None

    let private formatFloat (value: float) = numberText value

    let private parseOptionalExpiry value =
        if value = "-" then
            Some None
        else
            tryParseFiniteFloat value |> Option.map Some

    let private parseCacheLine (line: string) =
        match line.Split('\t') with
        | [| "w"; encodedName; statIdentity; oid; activity |] when validHex 40 oid ->
            tryParseFiniteFloat activity
            |> Option.map (fun lastActivity ->
                true,
                decodeFileName encodedName,
                { StatIdentity = statIdentity
                  Oid = GitObjectId.create oid
                  LastActivityMs = Some lastActivity })
        | [| "p"; encodedName; statIdentity; oid |] when validHex 40 oid ->
            Some(
                false,
                decodeFileName encodedName,
                { StatIdentity = statIdentity
                  Oid = GitObjectId.create oid
                  LastActivityMs = None }
            )
        | _ -> None

    let private parseCacheEntry state (line: string) =
        match state, parseCacheLine line with
        | Some(writers, payloads), Some(true, name, file) -> Some(Map.add name file writers, payloads)
        | Some(writers, payloads), Some(false, name, file) -> Some(writers, Map.add name file payloads)
        | _ -> None

    let private parseCacheHeader (line: string) =
        match line.Split('\t') with
        | [| version; fingerprint; root; nextExpiry |] when
            version = materializationCacheVersion
            && validHex 64 fingerprint
            && validHex 40 root
            ->
            parseOptionalExpiry nextExpiry
            |> Option.map (fun expiry -> fingerprint, root, expiry)
        | _ -> None

    let private cacheFromText (text: string) =
        let lines = text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)

        match Array.tryHead lines |> Option.bind parseCacheHeader with
        | Some(fingerprint, root, nextExpiry) ->
            lines
            |> Array.skip 1
            |> Array.fold parseCacheEntry (Some(Map.empty, Map.empty))
            |> Option.map (fun (writers, payloads) ->
                { Fingerprint = fingerprint
                  Root = { RootOid = RootOid.create (GitObjectId.create root) }
                  NextExpiryMs = nextExpiry
                  Writers = writers
                  Payloads = payloads })
        | None -> None

    let private tryReadTextFile (path: string) : string option =
        try
            Some(readTextFileSync path "utf8")
        with _ ->
            None

    let private readMaterializationCache commonDir =
        let path = materializationCachePath commonDir

        if existsSync path then
            tryReadTextFile path |> Option.bind cacheFromText
        else
            None

    let private cacheTimeValid (nowMs: float) (cache: MaterializationCache) =
        match cache.NextExpiryMs with
        | None -> true
        | Some expiry -> nowMs <= expiry

    let private tryCachedLocal commonDir nowMs =
        let fingerprint = ProcessEventLog.physicalFingerprint commonDir

        readMaterializationCache commonDir
        |> Option.filter (fun cache -> cache.Fingerprint = fingerprint && cacheTimeValid nowMs cache)

    let tryCachedLocalSnapshot (commonDir: string) : StoreSnapshot option =
        tryCachedLocal commonDir (currentTimeMs ())
        |> Option.map (fun cache -> cache.Root)

    let private sameRoot (left: StoreSnapshot) (right: StoreSnapshot) =
        RootOid.value left.RootOid = RootOid.value right.RootOid

    let private cacheFiles selector (cache: MaterializationCache option) =
        cache |> Option.map selector |> Option.defaultValue Map.empty

    let private exactCached (cacheFiles: Map<string, CachedFile>) name statIdentity =
        cacheFiles
        |> Map.tryFind name
        |> Option.filter (fun cached -> cached.StatIdentity = statIdentity)

    // Keep OID reuse as an explicit primitive: near-equal sync paths should be
    // visibly and mechanically tied to the validated stat cache.
    let private cachedOid (cacheFiles: Map<string, CachedFile>) name statIdentity =
        exactCached cacheFiles name statIdentity
        |> Option.map (fun cached -> cached.Oid)

    let private resolveBlobOid (raw: IGitRawStore) (readBytes: string -> byte[]) cacheFiles name statIdentity =
        match cachedOid cacheFiles name statIdentity with
        | Some oid -> Task.FromResult oid
        | None -> raw.WriteBlob(readBytes name)

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
                    let! oid = resolveBlobOid raw readBytes cacheFiles name statIdentity

                    return!
                        loop
                            tail
                            ({ Mode = blobMode
                               Name = name
                               Oid = oid }
                             :: acc)
            }

        loop files []

    let private resolveWriter
        (raw: IGitRawStore)
        commonDir
        (cacheFiles: Map<string, CachedFile>)
        (metadata: ProcessEventLog.WriterPhysicalMetadata)
        =
        let activityForWrittenBlob oid =
            Map.tryFind metadata.Name cacheFiles
            |> Option.filter (fun cached -> cached.Oid = oid)
            |> Option.bind _.LastActivityMs
            |> Option.defaultValue metadata.LastActivityMs

        task {
            match exactCached cacheFiles metadata.Name metadata.StatIdentity with
            | Some cached ->
                return
                    { Entry =
                        { Mode = blobMode
                          Name = metadata.Name
                          Oid = cached.Oid }
                      StatIdentity = metadata.StatIdentity
                      LastActivityMs = cached.LastActivityMs |> Option.defaultValue metadata.LastActivityMs }
            | None ->
                let! oid = raw.WriteBlob(ProcessEventLog.readWriterFileBytes commonDir metadata.Name)

                return
                    { Entry =
                        { Mode = blobMode
                          Name = metadata.Name
                          Oid = oid }
                      StatIdentity = metadata.StatIdentity
                      LastActivityMs = activityForWrittenBlob oid }
        }

    let private materializeWriters
        (raw: IGitRawStore)
        commonDir
        (cache: MaterializationCache option)
        (writerMetadata: ProcessEventLog.WriterPhysicalMetadata list)
        : Task<MaterializedWriter list> =
        let cached = cacheFiles (fun value -> value.Writers) cache

        let rec loop remaining acc =
            task {
                match remaining with
                | [] -> return List.rev acc
                | metadata :: tail ->
                    let! writer = resolveWriter raw commonDir cached metadata
                    return! loop tail (writer :: acc)
            }

        loop writerMetadata []

    let private writerManifestText (writers: MaterializedWriter list) =
        [ yield writerManifestVersion
          yield!
              writers
              |> List.map (fun writer ->
                  String.concat
                      "\t"
                      [ encodeFileName writer.Entry.Name
                        GitObjectId.value writer.Entry.Oid
                        formatFloat writer.LastActivityMs ]) ]
        |> String.concat "\n"
        |> fun text -> text + "\n"

    let private parseManifestLine (line: string) =
        match line.Split('\t') with
        | [| encodedName; oid; activity |] when validHex 40 oid ->
            tryParseFiniteFloat activity
            |> Option.map (fun lastActivity ->
                decodeFileName encodedName,
                { BlobOid = GitObjectId.create oid
                  LastActivityMs = lastActivity })
        | _ -> None

    let private addManifestLine state line =
        result {
            let! manifest = state

            let! name, entry =
                parseManifestLine line
                |> Result.requireSome "writer manifest contains a malformed entry"

            do!
                if Map.containsKey name manifest then
                    Error(sprintf "writer manifest contains duplicate entry: %s" name)
                else
                    Ok()

            return Map.add name entry manifest
        }

    let private parseManifestLines lines =
        lines |> Array.skip 1 |> Array.fold addManifestLine (Ok Map.empty)

    let private writerManifestFromText (text: string) =
        let lines = text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)

        if lines.Length = 0 || lines.[0] <> writerManifestVersion then
            Error "writer manifest has an unknown or missing version"
        else
            parseManifestLines lines

    let private nextExpiry (writers: MaterializedWriter list) =
        writers
        |> List.map (fun writer -> writer.LastActivityMs + retentionMilliseconds ())
        |> List.sort
        |> List.tryHead

    let private cacheWriterLine (writer: MaterializedWriter) =
        String.concat
            "\t"
            [ "w"
              encodeFileName writer.Entry.Name
              writer.StatIdentity
              GitObjectId.value writer.Entry.Oid
              formatFloat writer.LastActivityMs ]

    let private cachePayloadLine statByName (entry: TreeEntry) =
        let statIdentity = Map.find entry.Name statByName
        String.concat "\t" [ "p"; encodeFileName entry.Name; statIdentity; GitObjectId.value entry.Oid ]

    let private writeMaterializationCache
        commonDir
        (snapshot: StoreSnapshot)
        (writers: MaterializedWriter list)
        payloadStats
        payloadEntries
        =
        try
            let fingerprint = ProcessEventLog.physicalFingerprint commonDir
            let root = RootOid.value snapshot.RootOid |> GitObjectId.value
            let payloadStatByName = Map.ofList payloadStats

            let expiry = nextExpiry writers |> Option.map formatFloat |> Option.defaultValue "-"

            let body =
                [ yield String.concat "\t" [ materializationCacheVersion; fingerprint; root; expiry ]
                  yield! writers |> List.map cacheWriterLine
                  yield! payloadEntries |> List.map (cachePayloadLine payloadStatByName) ]
                |> String.concat "\n"
                |> fun value -> value + "\n"

            writeTextFileSync (materializationCachePath commonDir) body "utf8"
        with _ ->
            ()

    let private removeExpiredLocalWriters nowMs (writers: MaterializedWriter list) commonDir =
        let active, expired =
            writers
            |> List.partition (fun writer -> isWriterActiveAt nowMs writer.LastActivityMs)

        expired
        |> List.iter (fun writer -> ProcessEventLog.removeWriterFile commonDir writer.Entry.Name)

        active

    let materializeLocalAt (raw: IGitRawStore) (commonDir: string) (nowMs: float) : Task<StoreSnapshot> =
        task {
            let cache = readMaterializationCache commonDir
            let writerMetadata = ProcessEventLog.writerPhysicalMetadata commonDir
            let payloadStats = ProcessEventLog.payloadPhysicalStats commonDir

            let! resolvedWriters = materializeWriters raw commonDir cache writerMetadata
            let writers = removeExpiredLocalWriters nowMs resolvedWriters commonDir

            let! payloadEntries =
                materializeFileEntries
                    raw
                    (ProcessEventLog.readPayloadFileBytes commonDir)
                    (cacheFiles (fun value -> value.Payloads) cache)
                    payloadStats

            let! writerTree = raw.WriteTree(writers |> List.map (fun writer -> writer.Entry))
            let! payloadTree = raw.WriteTree payloadEntries
            let! manifestBlob = writerManifestText writers |> Encoding.UTF8.GetBytes |> raw.WriteBlob

            let! root =
                raw.WriteTree
                    [ { Mode = treeMode
                        Name = "writers"
                        Oid = writerTree }
                      { Mode = treeMode
                        Name = "payloads"
                        Oid = payloadTree }
                      { Mode = blobMode
                        Name = "writer-manifest"
                        Oid = manifestBlob } ]

            let snapshot = { RootOid = RootOid.create root }

            // Re-read stats after expiry deletion so the physical fingerprint and
            // cache entries describe exactly the materialized retained set.
            let retainedStats =
                ProcessEventLog.writerPhysicalMetadata commonDir
                |> List.map (fun item -> item.Name, item)
                |> Map.ofList

            let retainedWriters =
                writers
                |> List.choose (fun writer ->
                    retainedStats
                    |> Map.tryFind writer.Entry.Name
                    |> Option.map (fun stat ->
                        { writer with
                            StatIdentity = stat.StatIdentity }))

            writeMaterializationCache commonDir snapshot retainedWriters payloadStats payloadEntries
            return snapshot
        }

    let materializeLocal (raw: IGitRawStore) (commonDir: string) : Task<StoreSnapshot> =
        materializeLocalAt raw commonDir (currentTimeMs ())

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

    let private readOptionalManifest
        (raw: IGitRawStore)
        (entry: TreeEntry option)
        : Task<Result<Map<string, WriterManifestEntry> option, ConvergeError>> =
        let parseManifestBytes (bytes: byte[]) =
            let text = Encoding.UTF8.GetString bytes

            if text = "v1" || text.StartsWith("v1\n", StringComparison.Ordinal) then
                Ok None
            else
                text |> writerManifestFromText |> Result.map Some |> Result.mapError asStorage

        let readManifestEntry manifest =
            taskResult {
                do! requireBlobMode manifest
                let! bytesOpt = TaskResultCE.ofTask (raw.ReadObject manifest.Oid)
                let! bytes = bytesOpt |> Result.requireSome (asStorage "missing writer-manifest blob")
                return! parseManifestBytes bytes
            }

        taskResult {
            match entry with
            | None -> return None
            | Some manifest -> return! readManifestEntry manifest
        }

    let private manifestForEntry (manifest: Map<string, WriterManifestEntry>) (entry: TreeEntry) =
        manifest
        |> Map.tryFind entry.Name
        |> Option.filter (fun info -> info.BlobOid = entry.Oid)

    let private retainedRemoteEntries
        (nowMs: float)
        (manifest: Map<string, WriterManifestEntry>)
        (entries: TreeEntry list)
        =
        entries
        |> List.filter (fun entry ->
            match manifestForEntry manifest entry with
            | Some info -> isWriterActiveAt nowMs info.LastActivityMs
            | None -> false)

    let private validateManifestCoverage (manifest: Map<string, WriterManifestEntry>) (entries: TreeEntry list) =
        let missingOrMismatched =
            entries
            |> List.tryFind (fun entry -> manifestForEntry manifest entry |> Option.isNone)

        match missingOrMismatched with
        | Some entry -> Error(asStorage (sprintf "writer manifest does not bind writer blob: %s" entry.Name))
        | None when manifest.Count <> entries.Length ->
            Error(asStorage "writer manifest contains entries absent from writers tree")
        | None -> Ok()

    let private remoteEntryNeeded
        (cacheFiles: Map<string, CachedFile>)
        (currentStats: Map<string, string>)
        (manifest: Map<string, WriterManifestEntry>)
        (entry: TreeEntry)
        =
        match
            Map.tryFind entry.Name cacheFiles, Map.tryFind entry.Name currentStats, manifestForEntry manifest entry
        with
        | Some cached, Some currentStat, Some remoteInfo when
            entry.Mode = blobMode
            && cached.StatIdentity = currentStat
            && cached.Oid = entry.Oid
            ->
            cached.LastActivityMs
            |> Option.exists (fun localActivity -> remoteInfo.LastActivityMs <> localActivity)
        | _ -> true

    let private changedRemoteEntries
        (cacheFiles: Map<string, CachedFile>)
        (currentStats: Map<string, string>)
        (manifest: Map<string, WriterManifestEntry>)
        (entries: TreeEntry list)
        =
        entries |> List.filter (remoteEntryNeeded cacheFiles currentStats manifest)

    let private writerFromBlob
        (manifest: Map<string, WriterManifestEntry>)
        (entryByName: Map<string, TreeEntry>)
        (name: string, bytes: byte[])
        =
        if not (name.EndsWith(".ndjson", StringComparison.Ordinal)) then
            raise (InvalidOperationException(sprintf "invalid writer filename: %s" name))

        let writerId = name.Substring(0, name.Length - ".ndjson".Length)

        let activity =
            entryByName
            |> Map.tryFind name
            |> Option.bind (manifestForEntry manifest)
            |> Option.map (fun info -> info.LastActivityMs)

        { WriterId = writerId
          Text = Encoding.UTF8.GetString bytes
          LastActivityMs = activity }

    let private readRemoteTrees
        (raw: IGitRawStore)
        (cache: MaterializationCache option)
        (commonDir: string)
        nowMs
        (writerTree: TreeEntry)
        (payloadTree: TreeEntry)
        manifestEntry
        =
        taskResult {
            let! writerEntries = readRequiredTree raw writerTree.Oid "writers"
            let! manifestOpt = readOptionalManifest raw manifestEntry
            let manifest = manifestOpt |> Option.defaultValue Map.empty

            // A snapshot predating writer activity metadata cannot participate in
            // retention safely: importing it would assign fetch-time mtime and
            // resurrect arbitrary old process writers. Clean-break by ignoring
            // its writer tree; a new snapshot will publish only locally-known
            // writers with canonical activity metadata.
            let retainedEntries =
                match manifestOpt with
                | None -> []
                | Some _ -> retainedRemoteEntries nowMs manifest writerEntries

            do!
                match manifestOpt with
                | None -> Ok()
                | Some _ -> validateManifestCoverage manifest writerEntries

            let writerStats = ProcessEventLog.writerPhysicalStats commonDir |> Map.ofList

            let neededWriterEntries =
                changedRemoteEntries
                    (cacheFiles (fun value -> value.Writers) cache)
                    writerStats
                    manifest
                    retainedEntries

            let! writerBlobs = readBlobList raw neededWriterEntries

            let entryByName =
                retainedEntries |> List.map (fun entry -> entry.Name, entry) |> Map.ofList

            let remoteWriters = writerBlobs |> List.map (writerFromBlob manifest entryByName)

            let! payloadEntries = readRequiredTree raw payloadTree.Oid "payloads"
            let payloadStats = ProcessEventLog.payloadPhysicalStats commonDir |> Map.ofList

            let neededPayloadEntries =
                changedRemoteEntries
                    (cacheFiles (fun value -> value.Payloads) cache)
                    payloadStats
                    Map.empty
                    payloadEntries

            let! payloadBlobs = readBlobList raw neededPayloadEntries
            return remoteWriters, payloadBlobs
        }

    let private readRemote
        (raw: IGitRawStore)
        (cache: MaterializationCache option)
        (commonDir: string)
        nowMs
        (snapshot: StoreSnapshot)
        : Task<Result<RemoteWriter list * (string * byte[]) list, ConvergeError>> =
        taskResult {
            let! rootEntries = readRequiredTree raw (RootOid.value snapshot.RootOid) "root"

            let writers =
                rootEntries
                |> List.tryFind (fun entry -> entry.Name = "writers" && entry.Mode = treeMode)

            let payloads =
                rootEntries
                |> List.tryFind (fun entry -> entry.Name = "payloads" && entry.Mode = treeMode)

            let manifest =
                rootEntries |> List.tryFind (fun entry -> entry.Name = "writer-manifest")

            match writers, payloads with
            | Some writerTree, Some payloadTree ->
                return! readRemoteTrees raw cache commonDir nowMs writerTree payloadTree manifest
            | _ -> return! Error(asStorage "sync root must contain writers/ and payloads/")
        }

    let private decodeOneRemoteWriter (writer: RemoteWriter) =
        ProcessEventLog.decodeWriterText ("remote:" + writer.WriterId) writer.Text
        |> Result.mapError ConvergeError.StorageInvalid
        |> Result.map (fun events -> "remote:" + writer.WriterId, events)

    let private decodeRemoteWriters
        (writers: RemoteWriter list)
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
                EventKWayMerge.mergeRetained (taggedLocal @ remote)
                |> Result.mapError ConvergeError.StorageInvalid

            match missingPayloadRef commonDir ordered with
            | Some payloadRef -> return! Error(ConvergeError.StorageInvalid(StorageInvalid.MissingPayload payloadRef))
            | None -> return ()
        }

    let private validateUnion
        (commonDir: string)
        nowMs
        (remoteWriters: RemoteWriter list)
        : Result<unit, ConvergeError> =
        result {
            let! local =
                ProcessEventLog.readStreamsAt commonDir nowMs
                |> Result.mapError ConvergeError.StorageInvalid

            let! remote = decodeRemoteWriters remoteWriters
            return! validateMergedStreams commonDir local remote
        }

    let private mergeOnePayload commonDir (name, bytes) =
        ProcessEventLog.mergePayloadFile commonDir name bytes
        |> Result.mapError asStorage

    let private mergeOneWriter commonDir (writer: RemoteWriter) =
        ProcessEventLog.mergeWriterTextWithActivity commonDir writer.WriterId writer.Text writer.LastActivityMs
        |> Result.mapError asStorage

    let private importRemote
        (commonDir: string)
        nowMs
        (writers: RemoteWriter list)
        (payloads: (string * byte[]) list)
        : Result<unit, ConvergeError> =
        // Payloads are content-addressed and may be safely imported before facts.
        // Validate the retained combined k-way history/closure before changing writer truth.
        result {
            do!
                payloads
                |> List.traverseResultM (mergeOnePayload commonDir)
                |> Result.map ignore

            do! validateUnion commonDir nowMs writers
            do! writers |> List.traverseResultM (mergeOneWriter commonDir) |> Result.map ignore
            return ()
        }

    let private syncWithoutRemote (raw: IGitRawStore) (commonDir: string) nowMs =
        taskResult {
            // Materialization owns expiry deletion. Any unreachable objects created
            // before validation are harmless Git objects and no ref points at them.
            let! snapshot = materializeLocalAt raw commonDir nowMs |> TaskResultCE.ofTask
            do! validateUnion commonDir nowMs []
            return snapshot
        }

    let private syncWithRemote (raw: IGitRawStore) (commonDir: string) nowMs (snapshot: StoreSnapshot) =
        match tryCachedLocal commonDir nowMs with
        | Some cached when sameRoot cached.Root snapshot -> taskResult { return cached.Root }
        | _ ->
            taskResult {
                // First materialization applies local expiry and refreshes the cache
                // before remote change detection. The second one captures imports.
                let! _ = materializeLocalAt raw commonDir nowMs |> TaskResultCE.ofTask
                let cache = readMaterializationCache commonDir
                let! writers, payloads = readRemote raw cache commonDir nowMs snapshot
                do! importRemote commonDir nowMs writers payloads
                return! materializeLocalAt raw commonDir nowMs |> TaskResultCE.ofTask
            }

    let private syncRemoteChoice (raw: IGitRawStore) (commonDir: string) nowMs (remote: StoreSnapshot option) =
        match remote with
        | None -> syncWithoutRemote raw commonDir nowMs
        | Some snapshot -> syncWithRemote raw commonDir nowMs snapshot

    /// Deterministic synchronization entry used by tests and by the wall-clock
    /// wrapper below. All retention decisions in one convergence pass share the
    /// exact same `nowMs` cutoff.
    let syncWriterStreamsAt
        (raw: IGitRawStore)
        (commonDir: string)
        (remote: StoreSnapshot option)
        (nowMs: float)
        : Task<Result<StoreSnapshot, ConvergeError>> =
        taskResult {
            try
                return! syncRemoteChoice raw commonDir nowMs remote
            with ex ->
                return! Error(ConvergeError.Transport ex.Message)
        }

    /// Merge remote physical truth into local writer files, apply the fixed
    /// writer-retention window, then materialize the candidate remote snapshot.
    let syncWriterStreams
        (raw: IGitRawStore)
        (commonDir: string)
        (remote: StoreSnapshot option)
        : Task<Result<StoreSnapshot, ConvergeError>> =
        syncWriterStreamsAt raw commonDir remote (currentTimeMs ())
