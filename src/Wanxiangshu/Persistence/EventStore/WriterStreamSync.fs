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
open FsToolkit.ErrorHandling

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

    let private readRemoteTrees (raw: IGitRawStore) (writerTree: TreeEntry) (payloadTree: TreeEntry) =
        taskResult {
            let! writerEntries = readRequiredTree raw writerTree.Oid "writers"
            let! writerBlobs = readBlobList raw writerEntries
            let! payloadEntries = readRequiredTree raw payloadTree.Oid "payloads"
            let! payloadBlobs = readBlobList raw payloadEntries
            return List.map writerTextFromBlob writerBlobs, payloadBlobs
        }

    let private readRemote
        (raw: IGitRawStore)
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
            | Some writerTree, Some payloadTree -> return! readRemoteTrees raw writerTree payloadTree
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
            return! materializeLocal raw commonDir |> TaskResultCE.ofTask
        }

    let private syncWithRemote (raw: IGitRawStore) (commonDir: string) (snapshot: StoreSnapshot) =
        taskResult {
            let! writers, payloads = readRemote raw snapshot
            do! importRemote commonDir writers payloads
            return! materializeLocal raw commonDir |> TaskResultCE.ofTask
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
