namespace Wanxiangshu.Infrastructure.Persist

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity

/// Specification oracle (§10.6): append-only set union + identity dedupe.
/// Not a production merge algorithm — contract-test reference only.
[<RequireQualifiedAccess>]
module EventStoreMergeSpec =

    let private asMergeError (error: StorageInvalid) : MergeError = MergeError.StorageInvalid error

    /// Pure event-set oracle. EventId sort is physical tie-break only (§5.0).
    let mergeEvents (events: EventEnvelope list) : Result<EventEnvelope list, MergeError> =
        match CanonicalEventCodec.mergeByIdentity events with
        | Ok merged -> Ok merged
        | Error err -> Error(asMergeError err)

    /// Load every event blob under each snapshot, then apply mergeEvents.
    /// Uses GitRawStore.loadEventEnvelopes (linear) rather than decoding with
    /// `list @ [envelope]`, which was O(|events|²) on the boot path.
    let merge (store: IGitRawStore) (MergeInput snapshots) : Task<Result<EventEnvelope list, MergeError>> =
        task {
            let acc = ResizeArray<EventEnvelope>()

            let rec loadAll remaining =
                task {
                    match remaining with
                    | [] -> return mergeEvents (Seq.toList acc)
                    | snapshot :: rest ->
                        match! GitRawStore.loadEventEnvelopes store snapshot.RootOid with
                        | Error err -> return Error(asMergeError err)
                        | Ok envelopes ->
                            for envelope in envelopes do
                                acc.Add envelope

                            return! loadAll rest
                }

            return! loadAll snapshots
        }

/// Production K-way merge: structural tree union by EventId path (§10.6).
/// Only reads blob bytes when the same path has differing OIDs.
[<RequireQualifiedAccess>]
module EventStoreMerge =

    let private asMergeError (error: StorageInvalid) : MergeError = MergeError.StorageInvalid error

    let private collisionAt (path: string) : MergeError =
        match EventIdShard.tryParseEventId path with
        | Some eventId -> asMergeError (StorageInvalid.IdentityCollision eventId)
        | None -> asMergeError (StorageInvalid.IdentityCollision(EventId.create path))

    let private bytesEqual (left: byte[]) (right: byte[]) : bool =
        emitJsExpr (left, right) "Buffer.from($0).equals(Buffer.from($1))"

    let rec private mergeEntryLists
        (store: IGitRawStore)
        (pathPrefix: string)
        (sources: TreeEntry list list)
        : Task<Result<TreeEntry list, MergeError>> =
        task {
            let byName =
                sources
                |> List.collect id
                |> List.groupBy (fun (entry: TreeEntry) -> entry.Name)
                |> List.sortBy fst

            let acc = ResizeArray<TreeEntry>()

            let rec mergeGroups
                (remaining: (string * TreeEntry list) list)
                : Task<Result<TreeEntry list, MergeError>> =
                task {
                    match remaining with
                    | [] -> return Ok(Seq.toList acc)
                    | (name, entries: TreeEntry list) :: rest ->
                        let path = if pathPrefix = "" then name else pathPrefix + "/" + name

                        let normalize (entry: TreeEntry) : TreeEntry =
                            { entry with
                                Mode = StoreTree.normalizeMode entry.Mode }

                        let normalized = entries |> List.map normalize

                        let modes = normalized |> List.map (fun (entry: TreeEntry) -> entry.Mode) |> List.distinct

                        match modes with
                        | [ mode ] when StoreTree.isTreeMode mode ->
                            let childOids =
                                normalized
                                |> List.map (fun (entry: TreeEntry) -> entry.Oid)
                                |> List.distinctBy GitObjectId.value

                            match childOids with
                            | [ oid ] ->
                                acc.Add(
                                    { Mode = StoreTree.TreeMode
                                      Name = name
                                      Oid = oid }
                                )

                                return! mergeGroups rest
                            | many ->
                                let childTrees = ResizeArray<TreeEntry list>()
                                // DSL-MUTABLE: algorithm-scratch — missing-child short-circuit while scanning merge inputs
                                let mutable missing = false

                                for oid in many do
                                    if not missing then
                                        match! store.ReadTree oid with
                                        | None -> missing <- true
                                        | Some tree -> childTrees.Add tree

                                if missing then
                                    return
                                        Error(
                                            asMergeError (StorageInvalid.MalformedEnvelope(sprintf "missing tree at %s" path))
                                        )
                                else
                                    match! mergeEntryLists store path (Seq.toList childTrees) with
                                    | Error e -> return Error e
                                    | Ok mergedChildren ->
                                        let! oid = store.WriteTree mergedChildren

                                        acc.Add(
                                            { Mode = StoreTree.TreeMode
                                              Name = name
                                              Oid = oid }
                                        )

                                        return! mergeGroups rest
                        | [ mode ] ->
                            let oids =
                                normalized
                                |> List.map (fun (entry: TreeEntry) -> entry.Oid)
                                |> List.distinctBy GitObjectId.value
                                |> List.sortWith GitObjectId.compare

                            match oids with
                            | [ oid ] ->
                                acc.Add({ Mode = mode; Name = name; Oid = oid })
                                return! mergeGroups rest
                            | many ->
                                let bodies = ResizeArray<GitObjectId * byte[] option>()

                                for oid in many do
                                    let! body = store.ReadObject oid
                                    bodies.Add((oid, body))

                                let bodyList = Seq.toList bodies

                                if bodyList |> List.exists (fun (_, body) -> Option.isNone body) then
                                    return
                                        Error(
                                            asMergeError (StorageInvalid.MalformedEnvelope(sprintf "missing blob at %s" path))
                                        )
                                else
                                    let contents = bodyList |> List.map (fun (oid, body) -> oid, Option.get body)

                                    let firstBytes = contents |> List.head |> snd

                                    if contents |> List.forall (fun (_, bytes) -> bytesEqual firstBytes bytes) then
                                        let oid = contents |> List.head |> fst

                                        acc.Add({ Mode = mode; Name = name; Oid = oid })
                                        return! mergeGroups rest
                                    elif path.StartsWith(StoreTree.EventsDir + "/") then
                                        return Error(collisionAt path)
                                    else
                                        return
                                            Error(
                                                asMergeError (
                                                    StorageInvalid.NonCanonical(sprintf "payload path conflict at %s" path)
                                                )
                                            )
                        | _ ->
                            return Error(asMergeError (StorageInvalid.NonCanonical(sprintf "mixed modes at %s" path)))
                }

            return! mergeGroups byName
        }

    let private readRootEntries
        (store: IGitRawStore)
        (snapshot: StoreSnapshot)
        : Task<Result<TreeEntry list, MergeError>> =
        task {
            match! store.ReadTree(RootOid.value snapshot.RootOid) with
            | None -> return Error(asMergeError (StorageInvalid.MalformedEnvelope "missing store root tree"))
            | Some entries -> return Ok entries
        }

    /// Structural K-way merge → new StoreSnapshot (same object DB).
    let merge (store: IGitRawStore) (MergeInput snapshots) : Task<Result<StoreSnapshot, MergeError>> =
        task {
            match snapshots with
            | [] ->
                match! GitRawStore.materializeSnapshot store [] with
                | Ok snapshot -> return Ok snapshot
                | Error err -> return Error(asMergeError err)
            | only :: [] -> return Ok only
            | many ->
                let rec load remaining (acc: TreeEntry list list) =
                    task {
                        match remaining with
                        | [] ->
                            match! mergeEntryLists store "" (List.rev acc) with
                            | Error e -> return Error e
                            | Ok mergedRootEntries ->
                                let! rootOid = store.WriteTree mergedRootEntries
                                return Ok { RootOid = RootOid.create rootOid }
                        | head :: tail ->
                            match! readRootEntries store head with
                            | Error e -> return Error e
                            | Ok entries -> return! load tail (entries :: acc)
                    }

                return! load many []
        }
