namespace Wanxiangshu.Ablation

open System
open System.Collections.Generic
open Thoth.Json
open Fable.Core
open Fable.Core.JsInterop

module AblationManifest =

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private moduleUrl () : string = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("createHash", "node:crypto")>]
    let private createHash (algo: string) : obj = jsNative

    let private fingerprintText (text: string) =
        emitJsExpr (text, createHash "sha256") "($1).update($0).digest('hex').slice(0, 12)"

    let private decodeNode =
        Decode.object (fun get ->
            { Id = get.Required.Field "id" Decode.string
              Package = get.Required.Field "package" Decode.string
              Station = get.Required.Field "station" Decode.int
              Kind = get.Optional.Field "kind" Decode.string |> Option.defaultValue "package"
              Parent = get.Optional.Field "parent" Decode.string
              BorrowedSurface =
                get.Optional.Field "borrowed_surface" (Decode.list Decode.string)
                |> Option.defaultValue [] })

    let private decodeEdge =
        Decode.object (fun get ->
            { From = get.Required.Field "from" Decode.string
              To = get.Required.Field "to" Decode.string
              Kind = get.Optional.Field "kind" Decode.string |> Option.defaultValue "station-order" })

    let private decodeDocument =
        Decode.object (fun get ->
            { Version = get.Optional.Field "version" Decode.string |> Option.defaultValue "ablation-v1"
              Nodes = get.Required.Field "nodes" (Decode.list decodeNode)
              Edges = get.Required.Field "edges" (Decode.list decodeEdge) })

    let private packageRoot () =
        let moduleDir = dirname (fileURLToPath (moduleUrl ()))
        pathJoin (pathJoin (moduleDir, ".."), "..")

    let resourcesDir () =
        pathJoin (packageRoot (), "resources/ablation")

    let private readJson path decoder =
        if not (existsSync path) then
            Error(MissingManifest path)
        else
            readFileSync (path, "utf8")
            |> Decode.fromString decoder
            |> Result.mapError (fun reason -> InvalidManifest(sprintf "%s: %s" path reason))

    let private validateSliceNode (nodeMap: Map<string, ManifestNode>) (node: ManifestNode) : AblationLoadError option =
        let parentNode =
            node.Parent |> Option.bind (fun parentId -> Map.tryFind parentId nodeMap)

        match node.Parent, parentNode with
        | None, _ -> Some(InvalidManifest(sprintf "Slice node '%s' must declare parent" node.Id))
        | Some parentId, None -> Some(InvalidManifest(sprintf "Slice node '%s' parent '%s' not found" node.Id parentId))
        | Some parentId, Some parent when parent.Kind <> "package" ->
            Some(
                InvalidManifest(
                    sprintf
                        "Slice node '%s' parent '%s' must be kind 'package' but is '%s'"
                        node.Id
                        parentId
                        parent.Kind
                )
            )
        | Some parentId, Some parent when parent.Package <> node.Package ->
            Some(
                InvalidManifest(
                    sprintf
                        "Slice node '%s' (package '%s') and parent '%s' (package '%s') must belong to same package"
                        node.Id
                        node.Package
                        parentId
                        parent.Package
                )
            )
        | Some _, Some _ -> None

    let private validatePackageMainNodes (document: ManifestDocument) : AblationLoadError option =
        let packages = document.Nodes |> List.map (fun n -> n.Package) |> List.distinct

        packages
        |> List.tryPick (fun pkg ->
            let nodesInPkg = document.Nodes |> List.filter (fun n -> n.Package = pkg)

            let mainNodes =
                nodesInPkg |> List.filter (fun n -> n.Id = pkg && n.Kind = "package")

            let pkgKindNodes = nodesInPkg |> List.filter (fun n -> n.Kind = "package")

            if mainNodes.IsEmpty then
                Some(
                    InvalidManifest(
                        sprintf "Package '%s' is missing a main node (expected id '%s' and kind 'package')" pkg pkg
                    )
                )
            elif mainNodes.Length > 1 || pkgKindNodes.Length > 1 then
                Some(InvalidManifest(sprintf "Package '%s' has multiple main nodes" pkg))
            else
                None)

    let validateNodes (document: ManifestDocument) : Result<unit, AblationLoadError> =
        let nodeMap = document.Nodes |> List.map (fun n -> n.Id, n) |> Map.ofList

        let sliceError =
            document.Nodes
            |> List.filter (fun node -> node.Kind = "slice")
            |> List.tryPick (validateSliceNode nodeMap)

        let mainNodeError = validatePackageMainNodes document

        match sliceError, mainNodeError with
        | Some err, _ -> Error err
        | None, Some err -> Error err
        | None, None -> Ok()

    let private detectCycle (document: ManifestDocument) : string option =
        let edges =
            document.Edges
            |> List.filter (fun e -> e.Kind = "station-order" || e.Kind = "borrow")

        let adj =
            edges
            |> List.groupBy (fun e -> e.From)
            |> List.map (fun (fromNode, es) -> fromNode, es |> List.map (fun e -> e.To))
            |> Map.ofList

        let allNodes = document.Nodes |> List.map (fun n -> n.Id)
        let state = Dictionary<string, int>()

        for id in allNodes do
            state.[id] <- 0

        let rec dfs node =
            state.[node] <- 1
            let neighbors = adj |> Map.tryFind node |> Option.defaultValue []

            let cycleFound =
                neighbors
                |> List.tryPick (fun nextNode ->
                    match state.TryGetValue nextNode with
                    | true, 1 -> Some(sprintf "Cycle detected involving edge %s -> %s" node nextNode)
                    | true, 0 -> dfs nextNode
                    | _ -> None)

            state.[node] <- 2
            cycleFound

        allNodes
        |> List.tryPick (fun node -> if state.[node] = 0 then dfs node else None)

    let loadNodes () =
        readJson (pathJoin (resourcesDir (), "nodes.json")) decodeDocument
        |> Result.bind (fun doc ->
            validateNodes doc
            |> Result.bind (fun () ->
                match detectCycle doc with
                | Some cycle -> Error(DagViolation cycle)
                | None -> Ok doc))

    let nodesFingerprint () =
        let path = pathJoin (resourcesDir (), "nodes.json")

        if not (existsSync path) then
            "000000000000"
        else
            fingerprintText (readFileSync (path, "utf8"))

    let private decodeProfile =
        Decode.object (fun get -> get.Required.Field "modes" (Decode.dict Decode.string))

    let private decodeProfiles =
        Decode.object (fun get ->
            let profiles = get.Required.Field "profiles" (Decode.dict decodeProfile)
            { Profiles = profiles })

    let loadProfiles () =
        readJson (pathJoin (resourcesDir (), "profiles.json")) decodeProfiles

    let loadToolMap () =
        readJson
            (pathJoin (resourcesDir (), "tool-map.json"))
            (Decode.object (fun get ->
                let tools = get.Required.Field "tools" (Decode.dict Decode.string)
                { Tools = tools }))

    let loadFactMap () =
        readJson
            (pathJoin (resourcesDir (), "fact-map.json"))
            (Decode.object (fun get ->
                let facts = get.Required.Field "facts" (Decode.dict Decode.string)
                { Facts = facts }))

    let private validateEdge
        (modes: Map<AblationNodeId, AblationMode>)
        (edge: ManifestEdge)
        : AblationLoadError option =
        let modeOf raw =
            modes
            |> Map.tryFind (AblationNodeId.create raw)
            |> Option.defaultValue AblationMode.Ablated

        let fromMode = modeOf edge.From
        let toMode = modeOf edge.To

        let fromAtLeastBorrowed =
            fromMode = AblationMode.Active || fromMode = AblationMode.Borrowed

        let toActive = toMode = AblationMode.Active

        let toAtLeastBorrowed =
            toMode = AblationMode.Active || toMode = AblationMode.Borrowed

        match edge.Kind with
        | "station-order" when toActive && not fromAtLeastBorrowed ->
            Some(
                DagViolation(
                    sprintf "station-order: %s requires %s at least borrowed before active downstream" edge.To edge.From
                )
            )
        | "borrow" when toAtLeastBorrowed && not fromAtLeastBorrowed ->
            Some(DagViolation(sprintf "borrow: %s requires %s at least borrowed" edge.To edge.From))
        | _ -> None

    let validateDag
        (document: ManifestDocument)
        (modes: Map<AblationNodeId, AblationMode>)
        : Result<unit, AblationLoadError> =
        let cycleError = detectCycle document |> Option.map DagViolation
        let edgeError = document.Edges |> List.tryPick (validateEdge modes)

        match cycleError, edgeError with
        | Some err, _ -> Error err
        | None, Some err -> Error err
        | None, None -> Ok()

    let nodeIds (document: ManifestDocument) =
        document.Nodes |> List.map (fun node -> AblationNodeId.create node.Id)

    let private modesFromProfile (document: ManifestDocument) (name: string) (profiles: ProfilesDocument) =
        let nodeMode profileModes (node: ManifestNode) acc =
            match profileModes |> Map.tryFind node.Id with
            | None -> Ok(Map.add (AblationNodeId.create node.Id) AblationMode.Ablated acc)
            | Some raw ->
                AblationMode.parse raw
                |> Option.map (fun mode -> Ok(Map.add (AblationNodeId.create node.Id) mode acc))
                |> Option.defaultValue (Error(InvalidMode(node.Id, raw)))

        match profiles.Profiles |> Map.tryFind name with
        | None -> Error(UnknownProfile name)
        | Some profileModes ->
            document.Nodes
            |> List.fold (fun state node -> state |> Result.bind (nodeMode profileModes node)) (Ok Map.empty)

    let buildRegistry
        (document: ManifestDocument)
        (profileName: string option)
        (explicit: Map<AblationNodeId, AblationMode>)
        : Result<AblationRegistry, AblationLoadError> =
        validateNodes document
        |> Result.bind (fun () ->
            let nodeMap =
                document.Nodes
                |> List.map (fun n -> AblationNodeId.create n.Id, n)
                |> Map.ofList

            let baseModes =
                document.Nodes
                |> List.map (fun node -> AblationNodeId.create node.Id, AblationMode.Active)
                |> Map.ofList

            let fromProfile =
                match profileName with
                | None -> Ok baseModes
                | Some name -> loadProfiles () |> Result.bind (modesFromProfile document name)

            fromProfile
            |> Result.bind (fun profileModes ->
                let merged =
                    explicit |> Map.fold (fun acc key value -> Map.add key value acc) profileModes

                validateDag document merged
                |> Result.map (fun () ->
                    { Modes = merged
                      Audit =
                        { Profile = profileName
                          ManifestVersion = document.Version
                          ManifestFingerprint = nodesFingerprint ()
                          NodeCount = document.Nodes.Length }
                      Nodes = nodeMap })))
