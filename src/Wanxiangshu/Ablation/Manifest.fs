namespace Wanxiangshu.Ablation

open System
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

    [<AllowNullLiteral>]
    type private AblationLaw =
        abstract validateNodes: nodes: obj -> obj
        abstract findCycle: nodes: obj * edges: obj -> obj
        abstract validateModes: edges: obj * modes: obj -> obj

    [<Import("Ablation", "wanxiangli/ablation")>]
    let private createAblation: unit -> AblationLaw = jsNative

    /// The DAG law (main nodes, slice parents, cycles, three-state edges) is owned by
    /// wanxiangli/ablation; this module keeps reading, decoding and registry assembly.
    let private law = createAblation ()

    let private nodesToJs (document: ManifestDocument) : obj =
        document.Nodes
        |> List.map (fun node ->
            createObj
                [ "id" ==> node.Id
                  "package" ==> node.Package
                  "kind" ==> node.Kind
                  "parent" ==> (node.Parent |> Option.toObj) ])
        |> List.toArray
        |> box

    let private edgesToJs (document: ManifestDocument) : obj =
        document.Edges
        |> List.map (fun edge -> createObj [ "from" ==> edge.From; "to" ==> edge.To; "kind" ==> edge.Kind ])
        |> List.toArray
        |> box

    let private modesToJs (modes: Map<AblationNodeId, AblationMode>) : obj =
        // DSL-MUTABLE: algorithm-scratch — plain record built once per call for the library message
        let record = createEmpty<obj>

        for KeyValue(id, mode) in modes do
            record?(AblationNodeId.value id) <- AblationMode.wire mode

        record

    let private toError (violation: obj) : AblationLoadError option =
        if isNull violation then
            None
        else
            let reason: string = violation?reason

            match (violation?kind: string) with
            | "DagViolation" -> Some(DagViolation reason)
            | _ -> Some(InvalidManifest reason)

    let validateNodes (document: ManifestDocument) : Result<unit, AblationLoadError> =
        match law.validateNodes (nodesToJs document) |> toError with
        | Some err -> Error err
        | None -> Ok()

    let private detectCycle (document: ManifestDocument) : AblationLoadError option =
        law.findCycle (nodesToJs document, edgesToJs document) |> toError

    let loadNodes () =
        readJson (pathJoin (resourcesDir (), "nodes.json")) decodeDocument
        |> Result.bind (fun doc ->
            validateNodes doc
            |> Result.bind (fun () ->
                match detectCycle doc with
                | Some err -> Error err
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

    let validateDag
        (document: ManifestDocument)
        (modes: Map<AblationNodeId, AblationMode>)
        : Result<unit, AblationLoadError> =
        match detectCycle document with
        | Some err -> Error err
        | None ->
            match law.validateModes (edgesToJs document, modesToJs modes) |> toError with
            | Some err -> Error err
            | None -> Ok()

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

    /// Build a registry straight from a document the caller already holds.
    ///
    /// Same validation as reading from disk, minus the read: this is the seam that
    /// lets a caller ask about a manifest it constructed itself without touching what
    /// other callers are reading.
    let registryOf
        (document: ManifestDocument)
        (profileName: string option)
        (explicit: Map<AblationNodeId, AblationMode>)
        : Result<AblationRegistry, AblationLoadError> =
        buildRegistry document profileName explicit

    /// Decode a manifest document from a JS value the caller already holds.
    ///
    /// Same decoder the file reader uses, so a caller cannot construct a document the
    /// loader would have rejected.
    let decodeFromJs (document: obj) : Result<ManifestDocument, AblationLoadError> =
        Decode.fromValue "$" decodeDocument document
        |> Result.mapError (fun reason -> InvalidManifest reason)
