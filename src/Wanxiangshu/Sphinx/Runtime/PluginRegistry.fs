namespace Wanxiangshu.Sphinx.Runtime

open System
open Wanxiangshu.Sphinx.Core

type RegistryError = { Code: string; Message: string }

module PluginRegistry =

    let private error code message =
        Error { Code = code; Message = message }

    let private compareOrdinal (left: string) (right: string) =
        String.Compare(left, right, StringComparison.Ordinal)

    let private validateSingleManifest (manifest: PluginManifest) =
        if String.IsNullOrWhiteSpace manifest.Id then
            error "invalid-manifest" "plugin id must not be blank"
        elif String.IsNullOrWhiteSpace manifest.Release then
            error "invalid-manifest" "plugin release must not be blank"
        elif String.IsNullOrWhiteSpace manifest.AbiHash then
            error "invalid-manifest" "plugin abi hash must not be blank"
        else
            Ok()

    let private checkSingles (manifests: PluginManifest list) =
        let rec loop (remaining: PluginManifest list) =
            match remaining with
            | [] -> Ok()
            | manifest :: rest -> validateSingleManifest manifest |> Result.bind (fun () -> loop rest)

        loop manifests

    let private classifyDuplicateGroup (id: string) (group: PluginManifest list) =
        let releases =
            group
            |> List.map (fun (manifest: PluginManifest) -> manifest.Release)
            |> Set.ofList

        let abiHashes =
            group
            |> List.map (fun (manifest: PluginManifest) -> manifest.AbiHash)
            |> Set.ofList

        if Set.count releases > 1 then
            Some(error "duplicate-release" (sprintf "plugin %s binds more than one release" id))
        elif Set.count abiHashes > 1 then
            Some(error "abi-mismatch" (sprintf "plugin %s binds conflicting abi hashes" id))
        else
            Some(error "duplicate-release" (sprintf "plugin %s is bound more than once" id))

    let private checkDuplicates (manifests: PluginManifest list) =
        manifests
        |> List.groupBy (fun (manifest: PluginManifest) -> manifest.Id)
        |> List.tryPick (fun (id, group) ->
            if List.length group <= 1 then
                None
            else
                classifyDuplicateGroup id group)
        |> Option.defaultValue (Ok())

    let private validateSchemaRefFields (schemaRef: SchemaRef) =
        if String.IsNullOrWhiteSpace schemaRef.Id then
            error "schema-mismatch" "schema id must not be blank"
        elif String.IsNullOrWhiteSpace schemaRef.Hash then
            error "schema-mismatch" "schema hash must not be blank"
        else
            Ok()

    let private matchSchemaHash (seen: Map<string, string>) (schemaRef: SchemaRef) =
        match Map.tryFind schemaRef.Id seen with
        | Some hash when hash <> schemaRef.Hash ->
            error "schema-mismatch" (sprintf "schema %s carries conflicting content hashes" schemaRef.Id)
        | _ -> Ok(Map.add schemaRef.Id schemaRef.Hash seen)

    let private insertSchemaEntry (seen: Map<string, string>) (schemaRef: SchemaRef) =
        validateSchemaRefFields schemaRef
        |> Result.bind (fun () -> matchSchemaHash seen schemaRef)

    let private checkSchemas (manifests: PluginManifest list) =
        manifests
        |> List.fold
            (fun result (manifest: PluginManifest) ->
                result
                |> Result.bind (fun known ->
                    manifest.Schemas
                    |> Map.fold
                        (fun inner _ (schemaRef: SchemaRef) ->
                            inner |> Result.bind (fun seen -> insertSchemaEntry seen schemaRef))
                        (Ok known)))
            (Ok Map.empty)
        |> Result.map (fun _ -> ())

    let private checkDependencies (manifests: PluginManifest list) =
        let ids = manifests |> List.map (fun manifest -> manifest.Id) |> Set.ofList

        manifests
        |> List.collect (fun manifest -> manifest.Dependencies |> Set.toList)
        |> List.tryFind (fun dependency -> not (Set.contains dependency ids))
        |> function
            | None -> Ok()
            | Some dependency -> error "missing-dependency" (sprintf "plugin dependency %s is not bound" dependency)

    let private finishDrain (order: string list) (manifests: PluginManifest list) (byId: Map<string, PluginManifest>) =
        if List.length order = List.length manifests then
            Ok(order |> List.rev |> List.map (fun id -> Map.find id byId))
        else
            error "dependency-cycle" "plugin dependencies contain a cycle"

    let private enqueueDependent
        (dependent: string)
        (queueInner: string list)
        (updated: Map<string, int>)
        (count: int)
        =
        if count = 0 then
            (dependent :: queueInner, updated)
        else
            (queueInner, updated)

    let private topological (manifests: PluginManifest list) =
        let byId =
            manifests |> List.map (fun manifest -> manifest.Id, manifest) |> Map.ofList

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
            | [] -> finishDrain order manifests byId
            | id :: rest ->
                let waiting = Map.tryFind id dependents |> Option.defaultValue []

                let nextQueue, nextRemaining =
                    waiting
                    |> List.fold
                        (fun (queueInner, remainingInner) dependent ->
                            let count = (Map.find dependent remainingInner) - 1
                            let updated = Map.add dependent count remainingInner

                            enqueueDependent dependent queueInner updated count)
                        (rest, remaining)

                drain (List.sortWith compareOrdinal nextQueue) (id :: order) nextRemaining

        drain ready [] initial

    let ordered (manifests: PluginManifest list) =
        checkSingles manifests
        |> Result.bind (fun () -> checkDuplicates manifests)
        |> Result.bind (fun () -> checkSchemas manifests)
        |> Result.bind (fun () -> checkDependencies manifests)
        |> Result.bind (fun () -> topological manifests)

    let bind (manifests: PluginManifest list) =
        ordered manifests |> Result.map (List.map Plugin.toLockEntry)

    let private insertLockEntry (seen: Map<string, PluginLockEntry>) (entry: PluginLockEntry) =
        if Map.containsKey entry.Plugin.Id seen then
            error "plugin-swapped" (sprintf "plugin %s appears more than once in the lock" entry.Plugin.Id)
        else
            Ok(Map.add entry.Plugin.Id entry seen)

    let private lockMap (entries: PluginLockEntry list) =
        let rec loop (seen: Map<string, PluginLockEntry>) (remaining: PluginLockEntry list) =
            match remaining with
            | [] -> Ok seen
            | entry :: rest -> insertLockEntry seen entry |> Result.bind (fun grown -> loop grown rest)

        loop Map.empty entries

    let compatible (existing: PluginLockEntry list) (candidate: PluginLockEntry list) =
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
                    (Ok())
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

    let private matchObservationSchema (inquiryLock: PluginLockEntry list) (schema: SchemaRef) =
        let declared =
            inquiryLock
            |> List.collect (fun (entry: PluginLockEntry) -> entry.Schemas |> Map.toList |> List.map snd)

        let known =
            declared |> List.filter (fun (schemaRef: SchemaRef) -> schemaRef.Id = schema.Id)

        match declared, known with
        | [], [] -> Ok()
        | _, [] -> error "schema-mismatch" (sprintf "observation schema %s is not locked" schema.Id)
        | _, locked when
            locked
            |> List.forall (fun (schemaRef: SchemaRef) -> schemaRef.Hash = schema.Hash)
            ->
            Ok()
        | _ -> error "schema-mismatch" (sprintf "observation schema %s content hash drifted" schema.Id)

    let checkObservation
        (inquiryLock: PluginLockEntry list)
        (observationLock: PluginLockEntry list)
        (schema: SchemaRef)
        =
        compatible inquiryLock observationLock
        |> Result.bind (fun () ->
            if String.IsNullOrWhiteSpace schema.Id || String.IsNullOrWhiteSpace schema.Hash then
                error "schema-mismatch" "observation schema must carry an id and content hash"
            else
                matchObservationSchema inquiryLock schema)
