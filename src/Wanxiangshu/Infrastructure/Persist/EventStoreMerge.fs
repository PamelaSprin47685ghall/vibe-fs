namespace Wanxiangshu.Infrastructure.Persist

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open System.Collections.Generic
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
    let merge (store: IGitRawStore) (MergeInput snapshots) : Result<EventEnvelope list, MergeError> =
        let acc = ResizeArray<EventEnvelope>()

        let rec loadAll remaining =
            match remaining with
            | [] -> mergeEvents (Seq.toList acc)
            | snapshot :: rest ->
                match GitRawStore.loadEventEnvelopes store snapshot.RootOid with
                | Error err -> Error(asMergeError err)
                | Ok envelopes ->
                    for envelope in envelopes do
                        acc.Add envelope

                    loadAll rest

        loadAll snapshots

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
        : Result<TreeEntry list, MergeError> =
        let byName =
            sources
            |> List.collect id
            |> List.groupBy (fun entry -> entry.Name)
            |> List.sortBy fst

        let folder
            (acc: Result<TreeEntry list, MergeError>)
            ((name: string), (group: TreeEntry list))
            : Result<TreeEntry list, MergeError> =
            match acc with
            | Error e -> Error e
            | Ok collected ->
                let path = if pathPrefix = "" then name else pathPrefix + "/" + name

                let normalized =
                    group
                    |> List.map (fun entry ->
                        { entry with
                            Mode = StoreTree.normalizeMode entry.Mode })

                let modes = normalized |> List.map (fun e -> e.Mode) |> List.distinct

                match modes with
                | [ mode ] when StoreTree.isTreeMode mode ->
                    let childOids =
                        normalized |> List.map (fun e -> e.Oid) |> List.distinctBy GitObjectId.value

                    match childOids with
                    | [ oid ] ->
                        Ok(
                            collected
                            @ [ { Mode = StoreTree.TreeMode
                                  Name = name
                                  Oid = oid } ]
                        )
                    | many ->
                        let childTrees = many |> List.choose (fun oid -> store.ReadTree oid)

                        if childTrees.Length <> many.Length then
                            Error(asMergeError (StorageInvalid.MalformedEnvelope(sprintf "missing tree at %s" path)))
                        else
                            match mergeEntryLists store path childTrees with
                            | Error e -> Error e
                            | Ok mergedChildren ->
                                let oid = store.WriteTree mergedChildren

                                Ok(
                                    collected
                                    @ [ { Mode = StoreTree.TreeMode
                                          Name = name
                                          Oid = oid } ]
                                )
                | [ mode ] ->
                    let oids =
                        normalized
                        |> List.map (fun e -> e.Oid)
                        |> List.distinctBy GitObjectId.value
                        |> List.sortWith GitObjectId.compare

                    match oids with
                    | [ oid ] -> Ok(collected @ [ { Mode = mode; Name = name; Oid = oid } ])
                    | many ->
                        let bodies = many |> List.map (fun oid -> oid, store.ReadObject oid)

                        if bodies |> List.exists (fun (_, body) -> Option.isNone body) then
                            Error(asMergeError (StorageInvalid.MalformedEnvelope(sprintf "missing blob at %s" path)))
                        else
                            let contents = bodies |> List.map (fun (oid, body) -> oid, Option.get body)

                            let firstBytes = contents |> List.head |> snd

                            if contents |> List.forall (fun (_, bytes) -> bytesEqual firstBytes bytes) then
                                let oid = contents |> List.head |> fst

                                Ok(collected @ [ { Mode = mode; Name = name; Oid = oid } ])
                            elif path.StartsWith(StoreTree.EventsDir + "/") then
                                Error(collisionAt path)
                            else
                                Error(
                                    asMergeError (
                                        StorageInvalid.NonCanonical(sprintf "payload path conflict at %s" path)
                                    )
                                )
                | _ -> Error(asMergeError (StorageInvalid.NonCanonical(sprintf "mixed modes at %s" path)))

        List.fold folder (Ok []) byName

    let private readRootEntries (store: IGitRawStore) (snapshot: StoreSnapshot) : Result<TreeEntry list, MergeError> =
        match store.ReadTree(RootOid.value snapshot.RootOid) with
        | None -> Error(asMergeError (StorageInvalid.MalformedEnvelope "missing store root tree"))
        | Some entries -> Ok entries

    /// Structural K-way merge → new StoreSnapshot (same object DB).
    let merge (store: IGitRawStore) (MergeInput snapshots) : Result<StoreSnapshot, MergeError> =
        match snapshots with
        | [] ->
            match GitRawStore.materializeSnapshot store [] with
            | Ok snapshot -> Ok snapshot
            | Error err -> Error(asMergeError err)
        | only :: [] -> Ok only
        | many ->
            let rec load remaining (acc: TreeEntry list list) =
                match remaining with
                | [] ->
                    match mergeEntryLists store "" (List.rev acc) with
                    | Error e -> Error e
                    | Ok mergedRootEntries ->
                        let rootOid = store.WriteTree mergedRootEntries
                        Ok { RootOid = RootOid.create rootOid }
                | head :: tail ->
                    match readRootEntries store head with
                    | Error e -> Error e
                    | Ok entries -> load tail (entries :: acc)

            load many []
