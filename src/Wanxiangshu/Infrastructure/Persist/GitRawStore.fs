namespace Wanxiangshu.Infrastructure.Persist

open System.Collections.Generic
open System.Threading.Tasks
open System.Text
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// Capability port over content-addressed Git objects + ref CAS (§2.3 / §9).
/// No CreateRef: first publication is CompareAndSwapRef(expectedOld=None).
/// Members return Task so EventStore write/CAS can yield the Node event loop.
type IGitRawStore =
    abstract WriteBlob: content: byte[] -> Task<GitObjectId>
    abstract WriteTree: entries: TreeEntry list -> Task<GitObjectId>
    abstract ReadObject: oid: GitObjectId -> Task<byte[] option>
    abstract ReadTree: oid: GitObjectId -> Task<TreeEntry list option>
    abstract ReadRef: refName: string -> Task<GitObjectId option>
    abstract CompareAndSwapRef: refName: string * expectedOld: GitObjectId option * newOid: GitObjectId -> Task<bool>

/// EventId path sharding: events/<2-hex>/<EventId>.jsonl (§6).
[<RequireQualifiedAccess>]
module EventIdShard =
    [<Literal>]
    let PrefixLength = 2

    [<Literal>]
    let Extension = ".jsonl"

    let prefix (eventId: EventId) : string =
        let id = EventId.value eventId

        if id.Length < PrefixLength then
            invalidArg "eventId" "EventId shorter than shard prefix"

        id.Substring(0, PrefixLength)

    let fileName (eventId: EventId) : string = EventId.value eventId + Extension

    /// Relative authority path under the store root.
    let relativePath (eventId: EventId) : string =
        sprintf "events/%s/%s" (prefix eventId) (fileName eventId)

    /// Parse EventId from a root-relative path, if it matches the shard layout.
    let tryParseEventId (relativePath: string) : EventId option =
        let parts = relativePath.Replace("\\", "/").Split('/')

        match parts with
        | [| "events"; shard; file |] when file.EndsWith(Extension) && shard.Length = PrefixLength ->
            let id = file.Substring(0, file.Length - Extension.Length)

            if id.Length >= PrefixLength && id.Substring(0, PrefixLength) = shard then
                Some(EventId.create id)
            else
                None
        | _ -> None

[<RequireQualifiedAccess>]
module StoreTree =
    [<Literal>]
    let EventsDir = "events"

    [<Literal>]
    let PayloadsDir = "payloads"

    [<Literal>]
    let BlobMode = "100644"

    /// Git tree object mode for directories (stored as 40000, not 040000).
    [<Literal>]
    let TreeMode = "40000"

    let normalizeMode (mode: string) : string =
        if mode = "040000" || mode = "40000" then TreeMode else mode

    let isTreeMode (mode: string) : bool =
        let m = normalizeMode mode
        m = TreeMode

    /// Git's canonical tree-entry order, which is part of the object format rather than a
    /// presentation choice: entries sort by name, and a directory sorts as `name + "/"`, so
    /// `events` precedes `events.meta`. Get this wrong and the tree oid differs from the one
    /// `git mktree` would produce for the same content.
    let canonicalOrder (entries: TreeEntry list) : TreeEntry list =
        entries
        |> List.map (fun entry ->
            { entry with
                Mode = normalizeMode entry.Mode })
        |> List.sortWith (fun a b ->
            let key (entry: TreeEntry) =
                if isTreeMode entry.Mode then
                    entry.Name + "/"
                else
                    entry.Name

            compare (key a) (key b))

/// §7.1: committed payloads/ == ⋃ events.payload_refs (no dangling, no extras).
[<RequireQualifiedAccess>]
module PayloadClosure =
    let ofEvents (events: EventEnvelope list) : GitObjectId list =
        events
        |> List.collect (fun envelope ->
            envelope.PayloadRefs
            |> List.map (fun payloadRef -> GitObjectId.create (PayloadRef.value payloadRef)))
        |> List.distinct
        |> List.sortWith GitObjectId.compare

    /// Fail closed when any payload_ref is missing from the object database.
    let validatePresent (store: IGitRawStore) (refs: GitObjectId list) : Task<Result<unit, StorageInvalid>> =
        let rec loop remaining =
            task {
                match remaining with
                | [] -> return Ok()
                | head :: tail ->
                    match! store.ReadObject head with
                    | None ->
                        return Error(StorageInvalid.MissingPayload(PayloadRef.create (GitObjectId.value head)))
                    | Some _ -> return! loop tail
            }

        loop refs

    /// Fail closed when payloads/ names ≠ closure(events).
    let validateMatches (payloadNames: string list) (events: EventEnvelope list) : Result<unit, StorageInvalid> =
        let expected = ofEvents events |> List.map GitObjectId.value |> Set.ofList
        let actual = payloadNames |> Set.ofList

        if expected = actual then
            Ok()
        else
            let missing = Set.difference expected actual

            if not (Set.isEmpty missing) then
                let first = missing |> Set.toList |> List.sort |> List.head
                Error(StorageInvalid.MissingPayload(PayloadRef.create first))
            else
                Error(StorageInvalid.NonCanonical "payloads/ contains unreferenced payload objects")

module private GitObjectCodec =
    [<Import("createHash", "node:crypto")>]
    let private createHash: string -> obj = jsNative

    let private sha1Hex (data: byte[]) : string =
        let hash = createHash "sha1"
        hash?update (box data) |> ignore
        unbox<string> (hash?digest (box "hex"))

    let hashBlob (content: byte[]) : GitObjectId * byte[] =
        let header = Encoding.UTF8.GetBytes(sprintf "blob %d\u0000" content.Length)

        let framed: byte[] =
            emitJsExpr (header, content) "Buffer.concat([Buffer.from($0), Buffer.from($1)])"

        GitObjectId.create (sha1Hex framed), content

    /// Sort like git: tree names compare as name+"/".
    let sortEntries (entries: TreeEntry list) : TreeEntry list =
        entries
        |> List.map (fun entry ->
            { entry with
                Mode = StoreTree.normalizeMode entry.Mode })
        |> List.sortWith (fun a b ->
            let key (entry: TreeEntry) =
                if StoreTree.isTreeMode entry.Mode then
                    entry.Name + "/"
                else
                    entry.Name

            compare (key a) (key b))

    let encodeTreeBody (entries: TreeEntry list) : byte[] =
        let sorted = sortEntries entries

        let parts: byte[][] =
            sorted
            |> List.toArray
            |> Array.collect (fun entry ->
                let modeName = Encoding.UTF8.GetBytes(sprintf "%s %s\u0000" entry.Mode entry.Name)

                let oidBytes: byte[] =
                    emitJsExpr (GitObjectId.value entry.Oid) "Buffer.from($0, 'hex')"

                [| modeName; oidBytes |])

        emitJsExpr parts "Buffer.concat($0.map(function (p) { return Buffer.from(p); }))"

    let hashTree (entries: TreeEntry list) : GitObjectId * byte[] * TreeEntry list =
        let sorted = sortEntries entries
        let body = encodeTreeBody sorted
        let header = Encoding.UTF8.GetBytes(sprintf "tree %d\u0000" body.Length)

        let framed: byte[] =
            emitJsExpr (header, body) "Buffer.concat([Buffer.from($0), Buffer.from($1)])"

        GitObjectId.create (sha1Hex framed), body, sorted

    let parseTreeBody (body: byte[]) : TreeEntry list option =
        let parsed: obj option =
            emitJsExpr
                body
                """
                (function (body) {
                    const buf = Buffer.from(body);
                    const entries = [];
                    let i = 0;
                    while (i < buf.length) {
                        const space = buf.indexOf(0x20, i);
                        if (space < 0) return null;
                        const nul = buf.indexOf(0x00, space + 1);
                        if (nul < 0 || nul + 21 > buf.length) return null;
                        const mode = buf.slice(i, space).toString('utf8');
                        const name = buf.slice(space + 1, nul).toString('utf8');
                        const oid = buf.slice(nul + 1, nul + 21).toString('hex');
                        entries.push([mode, name, oid]);
                        i = nul + 21;
                    }
                    return entries;
                })($0)
                """

        match parsed with
        | None -> None
        | Some rows ->
            let items: (string * string * string)[] = unbox rows

            items
            |> Array.map (fun (mode, name, oid) ->
                { Mode = StoreTree.normalizeMode mode
                  Name = name
                  Oid = GitObjectId.create oid })
            |> Array.toList
            |> Some

type private StoredObject =
    | Blob of byte[]
    | Tree of body: byte[] * entries: TreeEntry list

/// Process-local content-addressed store for unit tests / pure merge algebra.
type InMemoryGitRawStore() =
    let gate = obj ()
    let objects = Dictionary<string, StoredObject>()
    let refs = Dictionary<string, string>()

    let writeBlobUnlocked (content: byte[]) =
        let oid, _ = GitObjectCodec.hashBlob content
        let key = GitObjectId.value oid

        if not (objects.ContainsKey key) then
            objects.[key] <- Blob content

        oid

    let writeTreeUnlocked (entries: TreeEntry list) =
        let oid, body, sorted = GitObjectCodec.hashTree entries
        let key = GitObjectId.value oid

        if not (objects.ContainsKey key) then
            objects.[key] <- Tree(body, sorted)

        oid

    interface IGitRawStore with
        member _.WriteBlob(content) =
            Task.FromResult(lock gate (fun () -> writeBlobUnlocked content))

        member _.WriteTree(entries) =
            Task.FromResult(lock gate (fun () -> writeTreeUnlocked entries))

        member _.ReadObject(oid) =
            Task.FromResult(
                lock gate (fun () ->
                    match objects.TryGetValue(GitObjectId.value oid) with
                    | true, Blob bytes -> Some bytes
                    | true, Tree(body, _) -> Some body
                    | _ -> None)
            )

        member _.ReadTree(oid) =
            Task.FromResult(
                lock gate (fun () ->
                    match objects.TryGetValue(GitObjectId.value oid) with
                    | true, Tree(_, entries) -> Some entries
                    | true, Blob body -> GitObjectCodec.parseTreeBody body
                    | _ -> None)
            )

        member _.ReadRef(refName) =
            Task.FromResult(
                lock gate (fun () ->
                    match refs.TryGetValue refName with
                    | true, value -> Some(GitObjectId.create value)
                    | _ -> None)
            )

        member _.CompareAndSwapRef(refName, expectedOld, newOid) =
            Task.FromResult(
                lock gate (fun () ->
                    let current =
                        match refs.TryGetValue refName with
                        | true, value -> Some value
                        | _ -> None

                    let expected = expectedOld |> Option.map GitObjectId.value
                    let next = GitObjectId.value newOid

                    match expected, current with
                    | None, None ->
                        refs.[refName] <- next
                        true
                    | Some exp, Some cur when exp = cur ->
                        refs.[refName] <- next
                        true
                    | _ -> false)
            )

[<RequireQualifiedAccess>]
module GitRawStore =

    /// Persist-side payload candidate for EventStore.Publish. Computes the same
    /// immutable Git blob identity as WriteBlob without publishing bytes first,
    /// so feature adapters can submit payload closure + events atomically through
    /// the unified store publication path.
    let preparePayload (content: byte[]) : GitObjectId * byte[] = GitObjectCodec.hashBlob content
    let createInMemory () : IGitRawStore = InMemoryGitRawStore() :> IGitRawStore

    let private buildEventsTree (store: IGitRawStore) (events: EventEnvelope list) : Task<GitObjectId> =
        task {
            let written = ResizeArray<string * string * GitObjectId>()

            for envelope in events do
                let normalized = EventEnvelope.normalize envelope
                let! oid = store.WriteBlob(CanonicalEventCodec.encodeUtf8 normalized)
                written.Add((EventIdShard.prefix normalized.EventId, EventIdShard.fileName normalized.EventId, oid))

            let byPrefix =
                written
                |> Seq.toList
                |> List.groupBy (fun (shard, _, _) -> shard)

            let prefixEntries = ResizeArray<TreeEntry>()

            for shard, items in byPrefix do
                let fileEntries =
                    items
                    |> List.map (fun (_, name, oid) ->
                        { Mode = StoreTree.BlobMode
                          Name = name
                          Oid = oid })

                let! leaf = store.WriteTree fileEntries

                prefixEntries.Add(
                    { Mode = StoreTree.TreeMode
                      Name = shard
                      Oid = leaf }
                )

            return! store.WriteTree (Seq.toList prefixEntries)
        }

    let private buildPayloadsTree (store: IGitRawStore) (refs: GitObjectId list) : Task<GitObjectId> =
        let entries =
            refs
            |> List.map (fun oid ->
                { Mode = StoreTree.BlobMode
                  Name = GitObjectId.value oid
                  Oid = oid })

        store.WriteTree entries

    /// Write a canonical root for the event set. payloads/ = §7.1 closure.
    /// Does not CAS refs/wanxiang/store — caller decides publication.
    let materializeSnapshot
        (store: IGitRawStore)
        (events: EventEnvelope list)
        : Task<Result<StoreSnapshot, StorageInvalid>> =
        task {
            match CanonicalEventCodec.mergeByIdentity events with
            | Error err -> return Error err
            | Ok normalized ->
                let closure = PayloadClosure.ofEvents normalized

                match! PayloadClosure.validatePresent store closure with
                | Error err -> return Error err
                | Ok() ->
                    let! eventsOid = buildEventsTree store normalized
                    let! payloadsOid = buildPayloadsTree store closure

                    let! rootOid =
                        store.WriteTree
                            [ { Mode = StoreTree.TreeMode
                                Name = StoreTree.EventsDir
                                Oid = eventsOid }
                              { Mode = StoreTree.TreeMode
                                Name = StoreTree.PayloadsDir
                                Oid = payloadsOid } ]

                    return Ok { RootOid = RootOid.create rootOid }
        }

    /// Walk events/ recursively → (relativePath * blobOid) pairs.
    ///
    /// Linear in blob count. The previous `list @ nested` fold copied the
    /// accumulating prefix at every file and directory, so listing a snapshot
    /// was O(|events|²). Boot, converge validate, and feature loaders all
    /// walk this tree; keep the walk O(|events|).
    let listEventBlobs
        (store: IGitRawStore)
        (root: RootOid)
        : Task<Result<(string * GitObjectId) list, StorageInvalid>> =
        let rec walk
            (dirOid: GitObjectId)
            (prefix: string)
            (acc: ResizeArray<string * GitObjectId>)
            : Task<Result<unit, StorageInvalid>> =
            task {
                match! store.ReadTree dirOid with
                | None -> return Error(StorageInvalid.MalformedEnvelope(sprintf "missing tree at %s" prefix))
                | Some entries ->
                    let rec loop (remaining: TreeEntry list) =
                        task {
                            match remaining with
                            | [] -> return Ok()
                            | entry :: rest ->
                                let path =
                                    if prefix = "" then
                                        entry.Name
                                    else
                                        prefix + "/" + entry.Name

                                if StoreTree.isTreeMode entry.Mode then
                                    match! walk entry.Oid path acc with
                                    | Error e -> return Error e
                                    | Ok() -> return! loop rest
                                else
                                    acc.Add((path, entry.Oid))
                                    return! loop rest
                        }

                    return! loop entries
            }

        task {
            match! store.ReadTree(RootOid.value root) with
            | None -> return Error(StorageInvalid.MalformedEnvelope "missing store root tree")
            | Some rootEntries ->
                match
                    rootEntries
                    |> List.tryFind (fun (e: TreeEntry) -> e.Name = StoreTree.EventsDir && StoreTree.isTreeMode e.Mode)
                with
                | None -> return Ok []
                | Some eventsEntry ->
                    let acc = ResizeArray<string * GitObjectId>()

                    match! walk eventsEntry.Oid StoreTree.EventsDir acc with
                    | Error e -> return Error e
                    | Ok() -> return Ok(Seq.toList acc)
        }

    /// Decode every event blob under a snapshot root.
    ///
    /// Production loaders (journal boot, feature adapters, converge validate)
    /// must use this walk — O(|events|) tree + blob reads — not
    /// EventStoreMergeSpec, which is the contract-test set-union oracle.
    let loadEventEnvelopes
        (store: IGitRawStore)
        (root: RootOid)
        : Task<Result<EventEnvelope list, StorageInvalid>> =
        task {
            match! listEventBlobs store root with
            | Error err -> return Error err
            | Ok blobs ->
                let rec decode remaining acc =
                    task {
                        match remaining with
                        | [] -> return Ok(List.rev acc)
                        | (path, oid) :: tail ->
                            match! store.ReadObject oid with
                            | None ->
                                return Error(StorageInvalid.MalformedEnvelope(sprintf "missing event blob at %s" path))
                            | Some bytes ->
                                match CanonicalEventCodec.tryDecodeUtf8 bytes with
                                | Error err -> return Error err
                                | Ok envelope ->
                                    match EventIdShard.tryParseEventId path with
                                    | Some pathId when pathId <> envelope.EventId ->
                                        return Error(StorageInvalid.NonCanonical "event path EventId mismatch")
                                    | _ -> return! decode tail (envelope :: acc)
                    }

                return! decode blobs []
        }

    let listPayloadNames (store: IGitRawStore) (root: RootOid) : Task<Result<string list, StorageInvalid>> =
        task {
            match! store.ReadTree(RootOid.value root) with
            | None -> return Error(StorageInvalid.MalformedEnvelope "missing store root tree")
            | Some rootEntries ->
                match
                    rootEntries
                    |> List.tryFind (fun e -> e.Name = StoreTree.PayloadsDir && StoreTree.isTreeMode e.Mode)
                with
                | None -> return Ok []
                | Some payloadsEntry ->
                    match! store.ReadTree payloadsEntry.Oid with
                    | None -> return Error(StorageInvalid.MalformedEnvelope "missing payloads/ tree")
                    | Some entries ->
                        return
                            entries
                            |> List.filter (fun e -> not (StoreTree.isTreeMode e.Mode))
                            |> List.map (fun e -> e.Name)
                            |> List.sort
                            |> Ok
        }

    /// Navigate events/<shard>/<EventId>.jsonl under a store root, if present.
    let tryReadEvent
        (store: IGitRawStore)
        (root: RootOid)
        (eventId: EventId)
        : Task<Result<EventEnvelope option, StorageInvalid>> =
        task {
            match! store.ReadTree(RootOid.value root) with
            | None -> return Error(StorageInvalid.MalformedEnvelope "missing store root tree")
            | Some rootEntries ->
                match
                    rootEntries
                    |> List.tryFind (fun e -> e.Name = StoreTree.EventsDir && StoreTree.isTreeMode e.Mode)
                with
                | None -> return Ok None
                | Some eventsEntry ->
                    match! store.ReadTree eventsEntry.Oid with
                    | None -> return Error(StorageInvalid.MalformedEnvelope "missing events/ tree")
                    | Some shardEntries ->
                        let shard = EventIdShard.prefix eventId

                        match
                            shardEntries
                            |> List.tryFind (fun e -> e.Name = shard && StoreTree.isTreeMode e.Mode)
                        with
                        | None -> return Ok None
                        | Some shardEntry ->
                            match! store.ReadTree shardEntry.Oid with
                            | None ->
                                return Error(StorageInvalid.MalformedEnvelope(sprintf "missing events/%s tree" shard))
                            | Some fileEntries ->
                                let fileName = EventIdShard.fileName eventId

                                match
                                    fileEntries
                                    |> List.tryFind (fun e -> e.Name = fileName && not (StoreTree.isTreeMode e.Mode))
                                with
                                | None -> return Ok None
                                | Some fileEntry ->
                                    match! store.ReadObject fileEntry.Oid with
                                    | None ->
                                        return
                                            Error(
                                                StorageInvalid.MalformedEnvelope(
                                                    sprintf "missing event blob %s" (EventIdShard.relativePath eventId)
                                                )
                                            )
                                    | Some bytes ->
                                        match CanonicalEventCodec.tryDecodeUtf8 bytes with
                                        | Error err -> return Error err
                                        | Ok envelope -> return Ok(Some envelope)
        }
