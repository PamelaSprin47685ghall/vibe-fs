namespace Wanxiangshu.Sphinx.Runtime

open System
open Wanxiangshu.Sphinx.Core

type RegistryError =
    { Code: string
      Message: string }

module PluginRegistry =

    let private error code message = Error { Code = code; Message = message }

    let private compareOrdinal left right = String.Compare(left, right, StringComparison.Ordinal)

    let private checkSingles manifests =
        let rec loop remaining =
            match remaining with
            | [] -> Ok ()
            | manifest :: rest ->
                if String.IsNullOrWhiteSpace manifest.Id then
                    error "invalid-manifest" "plugin id must not be blank"
                elif String.IsNullOrWhiteSpace manifest.Release then
                    error "invalid-manifest" "plugin release must not be blank"
                elif String.IsNullOrWhiteSpace manifest.AbiHash then
                    error "invalid-manifest" "plugin abi hash must not be blank"
                else
                    loop rest

        loop manifests

    let private checkDuplicates manifests =
        manifests
        |> List.groupBy (fun manifest -> manifest.Id)
        |> List.tryPick (fun (id, group) ->
            if List.length group <= 1 then
                None
            else
                let releases = group |> List.map (fun manifest -> manifest.Release) |> Set.ofList
                let abiHashes = group |> List.map (fun manifest -> manifest.AbiHash) |> Set.ofList

                if Set.count releases > 1 then
                    Some(error "duplicate-release" (sprintf "plugin %s binds more than one release" id))
                elif Set.count abiHashes > 1 then
                    Some(error "abi-mismatch" (sprintf "plugin %s binds conflicting abi hashes" id))
                else
                    Some(error "duplicate-release" (sprintf "plugin %s is bound more than once" id)))
        |> Option.defaultValue (Ok ())

    let private checkSchemas manifests =
        manifests
        |> List.fold
            (fun result manifest ->
                result
                |> Result.bind (fun known ->
                    manifest.Schemas
                    |> Map.fold
                        (fun inner _ schemaRef ->
                            inner
                            |> Result.bind (fun seen ->
                                if String.IsNullOrWhiteSpace schemaRef.Id then
                                    error "schema-mismatch" "schema id must not be blank"
                                elif String.IsNullOrWhiteSpace schemaRef.Hash then
                                    error "schema-mismatch" "schema hash must not be blank"
                                else
                                    match Map.tryFind schemaRef.Id seen with
                                    | Some hash when hash <> schemaRef.Hash ->
                                        error
                                            "schema-mismatch"
                                            (sprintf "schema %s carries conflicting content hashes" schemaRef.Id)
                                    | _ -> Ok(Map.add schemaRef.Id schemaRef.Hash seen)))
                        (Ok known)))
            (Ok Map.empty)
        |> Result.map (fun _ -> ())

    let private checkDependencies manifests =
        let ids = manifests |> List.map (fun manifest -> manifest.Id) |> Set.ofList

        manifests
        |> List.collect (fun manifest -> manifest.Dependencies |> Set.toList)
        |> List.tryFind (fun dependency -> not (Set.contains dependency ids))
        |> function
            | None -> Ok ()
            | Some dependency ->
                error "missing-dependency" (sprintf "plugin dependency %s is not bound" dependency)

    let private topological manifests =
        let byId = manifests |> List.map (fun manifest -> manifest.Id, manifest) |> Map.ofList

        let dependents =
            manifests
            |> List.fold
                (fun current manifest ->
                    manifest.Dependencies
                    |> Set.fold
                        (fun inner dependency ->
                            let waiting = Map.tryFind dependency inner |> Option.defaultValue []
                            Map.add dependency (manifest.Id :: waiting) inner)
                        current)
                Map.empty

        let initial =
            manifests
            |> List.map (fun manifest -> manifest.Id, Set.count manifest.Dependencies)
            |> Map.ofList

        let ready =
            initial
            |> Map.filter (fun _ count -> count = 0)
            |> Map.toList
            |> List.map fst
            |> List.sortWith compareOrdinal

        let rec drain queue order remaining =
            match queue with
            | [] ->
                if List.length order = List.length manifests then
                    Ok(order |> List.rev |> List.map (fun id -> Map.find id byId))
                else
                    error "dependency-cycle" "plugin dependencies contain a cycle"
            | id :: rest ->
                let waiting = Map.tryFind id dependents |> Option.defaultValue []

                let nextQueue, nextRemaining =
                    waiting
                    |> List.fold
                        (fun (queueInner, remainingInner) dependent ->
                            let count = (Map.find dependent remainingInner) - 1
                            let updated = Map.add dependent count remainingInner

                            if count = 0 then
                                (dependent :: queueInner, updated)
                            else
                                (queueInner, updated))
                        (rest, remaining)

                drain (List.sortWith compareOrdinal nextQueue) (id :: order) nextRemaining

        drain ready [] initial

    let ordered manifests =
        checkSingles manifests
        |> Result.bind (fun () -> checkDuplicates manifests)
        |> Result.bind (fun () -> checkSchemas manifests)
        |> Result.bind (fun () -> checkDependencies manifests)
        |> Result.bind (fun () -> topological manifests)

    let bind manifests = ordered manifests |> Result.map (List.map Plugin.toLockEntry)

    let private lockMap entries =
        let rec loop seen remaining =
            match remaining with
            | [] -> Ok seen
            | entry :: rest ->
                if Map.containsKey entry.Plugin.Id seen then
                    error
                        "plugin-swapped"
                        (sprintf "plugin %s appears more than once in the lock" entry.Plugin.Id)
                else
                    loop (Map.add entry.Plugin.Id entry seen) rest

        loop Map.empty entries

    let compatible existing candidate =
        lockMap existing
        |> Result.bind (fun existingMap ->
            lockMap candidate
            |> Result.bind (fun candidateMap ->
                let shared =
                    existingMap
                    |> Map.toList
                    |> List.choose (fun (id, locked) ->
                        Map.tryFind id candidateMap |> Option.map (fun current -> id, locked, current))
                    |> List.sortWith (fun (left, _, _) (right, _, _) -> compareOrdinal left right)

                shared
                |> List.fold
                    (fun result (id, locked, current) ->
                        result
                        |> Result.bind (fun () ->
                            if current.Plugin.Release <> locked.Plugin.Release then
                                error "plugin-swapped" (sprintf "locked plugin %s runs a different release" id)
                            elif current.Plugin.AbiHash <> locked.Plugin.AbiHash then
                                error "abi-mismatch" (sprintf "locked plugin %s carries a different abi hash" id)
                            elif current.Schemas <> locked.Schemas then
                                error "schema-mismatch" (sprintf "locked plugin %s carries different schemas" id)
                            elif
                                current.Capabilities <> locked.Capabilities
                                || current.Dependencies <> locked.Dependencies
                            then
                                error "plugin-swapped" (sprintf "locked plugin %s changed its binding" id)
                            else
                                Ok()))
                    (Ok ())
                |> Result.bind (fun () ->
                    match
                        existingMap
                        |> Map.tryPick (fun id _ -> if Map.containsKey id candidateMap then None else Some id)
                    with
                    | Some id -> error "plugin-swapped" (sprintf "locked plugin %s is missing" id)
                    | None ->
                        match
                            candidateMap
                            |> Map.tryPick (fun id _ -> if Map.containsKey id existingMap then None else Some id)
                        with
                        | Some id -> error "plugin-swapped" (sprintf "plugin %s was never bound" id)
                        | None -> Ok())))

    let checkObservation inquiryLock observationLock schema =
        compatible inquiryLock observationLock
        |> Result.bind (fun () ->
            if String.IsNullOrWhiteSpace schema.Id || String.IsNullOrWhiteSpace schema.Hash then
                error "schema-mismatch" "observation schema must carry an id and content hash"
            else
                let known =
                    inquiryLock
                    |> List.collect (fun entry -> entry.Schemas |> Map.toList |> List.map snd)
                    |> List.filter (fun schemaRef -> schemaRef.Id = schema.Id)

                match known with
                | [] -> error "schema-mismatch" (sprintf "observation schema %s is not locked" schema.Id)
                | locked when locked |> List.forall (fun schemaRef -> schemaRef.Hash = schema.Hash) -> Ok ()
                | _ ->
                    error
                        "schema-mismatch"
                        (sprintf "observation schema %s content hash drifted" schema.Id))
