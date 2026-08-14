namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Enforcer
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.Persistence

open System
open System.Text
open System.Threading.Tasks

/// DURABLE-CONVERGENCE-002/003/007/008.
/// Sync is deliberately physical: complete WriterId NDJSON files and payload
/// files in, Git blobs/tree out. It never interprets domain state.
[<RequireQualifiedAccess>]
module WriterStreamSync =

    let private blobMode = "100644"
    let private treeMode = "40000"

    let private asStorage reason =
        ConvergeError.StorageInvalid(StorageInvalid.NonCanonical reason)

    let private writeBlobEntries (raw: IGitRawStore) (items: (string * byte[]) list) : Task<TreeEntry list> =
        let rec loop remaining acc =
            task {
                match remaining with
                | [] -> return List.rev acc
                | (name, bytes) :: tail ->
                    let! oid = raw.WriteBlob bytes

                    return!
                        loop
                            tail
                            ({ Mode = blobMode
                               Name = name
                               Oid = oid }
                             :: acc)
            }

        loop items []

    /// One complete writer file becomes exactly one Git blob for this sync snapshot.
    let private materializeWriterFiles (raw: IGitRawStore) (commonDir: string) : Task<GitObjectId> =
        task {
            let items =
                ProcessEventLog.readWriterTexts commonDir
                |> List.map (fun (writerId, text) -> writerId + ".ndjson", Encoding.UTF8.GetBytes text)

            let! entries = writeBlobEntries raw items
            return! raw.WriteTree entries
        }

    let private materializePayloadFiles (raw: IGitRawStore) (commonDir: string) : Task<GitObjectId> =
        task {
            let! entries = writeBlobEntries raw (ProcessEventLog.readPayloadFiles commonDir)
            return! raw.WriteTree entries
        }

    let materializeLocal (raw: IGitRawStore) (commonDir: string) : Task<StoreSnapshot> =
        task {
            let! writers = materializeWriterFiles raw commonDir
            let! payloads = materializePayloadFiles raw commonDir

            let! root =
                raw.WriteTree
                    [ { Mode = treeMode
                        Name = "writers"
                        Oid = writers }
                      { Mode = treeMode
                        Name = "payloads"
                        Oid = payloads } ]

            return { RootOid = RootOid.create root }
        }

    let private readRequiredTree (raw: IGitRawStore) (oid: GitObjectId) (label: string) =
        task {
            match! raw.ReadTree oid with
            | Some entries -> return Ok entries
            | None -> return Error(asStorage (sprintf "missing %s tree" label))
        }

    let private readBlobList
        (raw: IGitRawStore)
        (entries: TreeEntry list)
        : Task<Result<(string * byte[]) list, ConvergeError>> =
        let rec loop remaining acc =
            task {
                match remaining with
                | [] -> return Ok(List.rev acc)
                | entry :: tail when entry.Mode = blobMode ->
                    match! raw.ReadObject entry.Oid with
                    | None -> return Error(asStorage (sprintf "missing sync blob: %s" entry.Name))
                    | Some bytes -> return! loop tail ((entry.Name, bytes) :: acc)
                | entry :: _ -> return Error(asStorage (sprintf "sync leaf is not a blob: %s" entry.Name))
            }

        loop entries []

    let private readRemote
        (raw: IGitRawStore)
        (snapshot: StoreSnapshot)
        : Task<Result<(string * string) list * (string * byte[]) list, ConvergeError>> =
        task {
            match! readRequiredTree raw (RootOid.value snapshot.RootOid) "root" with
            | Error error -> return Error error
            | Ok rootEntries ->
                let writers =
                    rootEntries
                    |> List.tryFind (fun entry -> entry.Name = "writers" && entry.Mode = treeMode)

                let payloads =
                    rootEntries
                    |> List.tryFind (fun entry -> entry.Name = "payloads" && entry.Mode = treeMode)

                match writers, payloads with
                | Some writerTree, Some payloadTree ->
                    match! readRequiredTree raw writerTree.Oid "writers" with
                    | Error error -> return Error error
                    | Ok writerEntries ->
                        match! readBlobList raw writerEntries with
                        | Error error -> return Error error
                        | Ok writerBlobs ->
                            match! readRequiredTree raw payloadTree.Oid "payloads" with
                            | Error error -> return Error error
                            | Ok payloadEntries ->
                                match! readBlobList raw payloadEntries with
                                | Error error -> return Error error
                                | Ok payloadBlobs ->
                                    let writerTexts =
                                        writerBlobs
                                        |> List.map (fun (name, bytes) ->
                                            if not (name.EndsWith(".ndjson", StringComparison.Ordinal)) then
                                                raise (
                                                    InvalidOperationException(
                                                        sprintf "invalid writer filename: %s" name
                                                    )
                                                )

                                            let writerId = name.Substring(0, name.Length - ".ndjson".Length)
                                            writerId, Encoding.UTF8.GetString bytes)

                                    return Ok(writerTexts, payloadBlobs)
                | _ -> return Error(asStorage "sync root must contain writers/ and payloads/")
        }

    let private decodeRemoteWriters
        (writers: (string * string) list)
        : Result<(string * Wanxiangshu.Persistence.EventStore.EventEnvelope list) list, ConvergeError> =
        let rec loop remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | (writerId, text) :: tail ->
                match ProcessEventLog.decodeWriterText ("remote:" + writerId) text with
                | Error error -> Error(ConvergeError.StorageInvalid error)
                | Ok events -> loop tail (("remote:" + writerId, events) :: acc)

        loop writers []

    let private validateUnion
        (commonDir: string)
        (remoteWriters: (string * string) list)
        : Result<unit, ConvergeError> =
        match ProcessEventLog.readStreams commonDir, decodeRemoteWriters remoteWriters with
        | Error error, _ -> Error(ConvergeError.StorageInvalid error)
        | _, Error error -> Error error
        | Ok local, Ok remote ->
            let taggedLocal =
                local |> List.map (fun (writerId, events) -> "local:" + writerId, events)

            match EventKWayMerge.merge (taggedLocal @ remote) with
            | Error error -> Error(ConvergeError.StorageInvalid error)
            | Ok ordered ->
                let missing =
                    ordered
                    |> List.collect (fun event -> event.PayloadRefs)
                    |> List.tryFind (ProcessEventLog.payloadExists commonDir >> not)

                match missing with
                | Some payloadRef -> Error(ConvergeError.StorageInvalid(StorageInvalid.MissingPayload payloadRef))
                | None -> Ok()

    let private importRemote
        (commonDir: string)
        (writers: (string * string) list)
        (payloads: (string * byte[]) list)
        : Result<unit, ConvergeError> =
        let rec mergePayloads remaining =
            match remaining with
            | [] -> Ok()
            | (name, bytes) :: tail ->
                match ProcessEventLog.mergePayloadFile commonDir name bytes with
                | Ok() -> mergePayloads tail
                | Error error -> Error(asStorage error)

        let rec mergeWriters remaining =
            match remaining with
            | [] -> Ok()
            | (writerId, text) :: tail ->
                match ProcessEventLog.mergeWriterText commonDir writerId text with
                | Ok() -> mergeWriters tail
                | Error error -> Error(asStorage error)

        // Payloads are content-addressed and may be safely imported before facts.
        // Validate the combined k-way history/closure before changing writer truth.
        match mergePayloads payloads with
        | Error error -> Error error
        | Ok() ->
            match validateUnion commonDir writers with
            | Error error -> Error error
            | Ok() -> mergeWriters writers

    /// Merge remote physical truth into local writer files, then materialize the
    /// whole local truth as the candidate remote snapshot. Business integration
    /// happens only after this returns, through CanonicalIntegrator.
    let syncWriterStreams
        (raw: IGitRawStore)
        (commonDir: string)
        (remote: StoreSnapshot option)
        : Task<Result<StoreSnapshot, ConvergeError>> =
        task {
            try
                match remote with
                | None ->
                    match validateUnion commonDir [] with
                    | Error error -> return Error error
                    | Ok() ->
                        let! local = materializeLocal raw commonDir
                        return Ok local
                | Some snapshot ->
                    match! readRemote raw snapshot with
                    | Error error -> return Error error
                    | Ok(writers, payloads) ->
                        match importRemote commonDir writers payloads with
                        | Error error -> return Error error
                        | Ok() ->
                            let! local = materializeLocal raw commonDir
                            return Ok local
            with ex ->
                return Error(ConvergeError.Transport ex.Message)
        }
