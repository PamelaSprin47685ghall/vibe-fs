namespace Wanxiangshu.Sphinx.V2.Runtime

open System

open Wanxiangshu.Sphinx.V2.Core

type RegistryError = { Code: string; Message: string }

/// One locked plugin: the manifest plus the callable behind it. The two travel together
/// so a capability can never be advertised without something to run it.
type LockedPlugin =
    { Manifest: PluginManifest
      Execute: ExecutablePlugin }

type PluginLockEntry =
    { Id: string
      Release: string
      ImplementationHash: string
      AbiHash: string
      Capabilities: Set<string>
      Dependencies: Set<string>
      Schemas: Map<string, SchemaRef> }

module Registry =

    let private error code message : Result<'value, RegistryError> =
        Error { Code = code; Message = message }

    let private compareOrdinal (left: string) (right: string) =
        String.Compare(left, right, StringComparison.Ordinal)

    let toLockEntry (plugin: LockedPlugin) : PluginLockEntry =
        { Id = plugin.Manifest.Id
          Release = plugin.Manifest.Release
          ImplementationHash = plugin.Manifest.ImplementationHash
          AbiHash = plugin.Manifest.AbiHash
          Capabilities = plugin.Manifest.Capabilities
          Dependencies = plugin.Manifest.Dependencies
          Schemas = plugin.Manifest.Schemas }

    /// Topological order over plugin dependencies. A cycle is a build-time defect: no
    /// runtime protocol can resolve it, and silently picking an order would hide it.
    let ordered (plugins: LockedPlugin list) : Result<LockedPlugin list, RegistryError> =
        let byId = plugins |> List.map (fun plugin -> plugin.Manifest.Id, plugin) |> Map.ofList

        let rec visit (id: string) (visiting: Set<string>) (acc: LockedPlugin list) =
            match byId |> Map.tryFind id with
            | None -> Ok acc
            | Some plugin when visiting |> Set.contains id ->
                error "plugin-cycle" (sprintf "plugin dependency cycle at %s" id)
            | Some plugin ->
                let deeper = Set.add id visiting

                plugin.Manifest.Dependencies
                |> Set.toList
                |> List.sort
                |> List.fold
                    (fun state dep -> state |> Result.bind (fun collected -> visit dep deeper collected))
                    (Ok acc)
                |> Result.map (fun collected -> plugin :: collected)

        plugins
        |> List.map (fun plugin -> plugin.Manifest.Id)
        |> List.sort
        |> List.fold (fun state id -> state |> Result.bind (fun acc -> visit id Set.empty acc)) (Ok [])
        |> Result.map List.rev

    /// Bind manifests to executables. A declared capability with no executable behind it
    /// fails here, at startup, rather than at the moment of dispatch.
    let bind (plugins: LockedPlugin list) : Result<LockedPlugin list, RegistryError> =
        let manifests = plugins |> List.map (fun plugin -> plugin.Manifest)

        let validated =
            manifests
            |> List.fold
                (fun state manifest ->
                    state
                    |> Result.bind (fun () ->
                        PluginContract.validateManifest manifest
                        |> Result.mapError (fun fault -> { Code = fault.Code; Message = fault.Message })))
                (Ok())

        let declaredIds =
            manifests
            |> List.map (fun manifest -> manifest.Id)
            |> Set.ofList

        let missingDependencies =
            manifests
            |> List.collect (fun manifest -> manifest.Dependencies |> Set.toList)
            |> List.filter (fun dependency -> not (Set.contains dependency declaredIds))

        match validated with
        | Error fault -> Error fault
        | Ok () ->
            let grouped =
                manifests |> List.groupBy (fun manifest -> manifest.Id)

            let conflicting =
                grouped
                |> List.filter (fun (_, group) ->
                    let releases = group |> List.map (fun manifest -> manifest.Release) |> Set.ofList
                    Set.count releases > 1)
                |> List.map fst

            if not (List.isEmpty missingDependencies) then
                error "plugin-dependency-missing" (sprintf "plugins declare missing dependencies: %s" (String.concat ", " missingDependencies))
            elif not (List.isEmpty conflicting) then
                error "plugin-conflict" (sprintf "plugin id bound to multiple releases: %s" (String.concat ", " conflicting))
            else
                ordered plugins

    /// The lock a new proposal must match. Comparing implementation hashes, ABI hashes
    /// and schema hashes — not just release labels — is what makes a mid-inquiry
    /// implementation swap detectable (WHAT[sphinx-v2-003]).
    let lockOf (plugins: LockedPlugin list) : PluginLockEntry list =
        plugins |> List.map toLockEntry

    let compatible (existing: PluginLockEntry list) (candidate: LockedPlugin list) : Result<unit, RegistryError> =
        let wanted = candidate |> List.map toLockEntry

        let byId (entries: PluginLockEntry list) =
            entries |> List.map (fun entry -> entry.Id, entry) |> Map.ofList

        let current = byId existing
        let next = byId wanted

        let changed =
            existing
            |> List.filter (fun entry ->
                match next |> Map.tryFind entry.Id with
                | Some candidateEntry -> candidateEntry <> entry
                | None -> true)
            |> List.map (fun entry -> entry.Id)

        let added =
            wanted
            |> List.filter (fun entry -> not (Map.containsKey entry.Id current))
            |> List.map (fun entry -> entry.Id)

        if not (List.isEmpty changed) then
            error "plugin-swapped" (sprintf "locked plugins changed: %s" (String.concat ", " changed))
        elif not (List.isEmpty added) then
            error "plugin-added" (sprintf "plugins added after lock: %s" (String.concat ", " added))
        else
            Ok()
