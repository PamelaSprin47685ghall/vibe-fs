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
              Parent = get.Optional.Field "parent" Decode.string })

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
            let text = readFileSync (path, "utf8")

            match Decode.fromString decoder text with
            | Ok value -> Ok value
            | Error reason -> Error(InvalidManifest(sprintf "%s: %s" path reason))

    let loadNodes () =
        readJson (pathJoin (resourcesDir (), "nodes.json")) decodeDocument

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
        let modeOf (raw: string) =
            modes
            |> Map.tryFind (AblationNodeId.create raw)
            |> Option.defaultValue AblationMode.Ablated

        let atLeastBorrowed raw =
            match modeOf raw with
            | AblationMode.Active
            | AblationMode.Borrowed -> true
            | AblationMode.Ablated -> false

        let atLeastActive raw = modeOf raw = AblationMode.Active

        document.Edges
        |> List.tryPick (fun edge ->
            match edge.Kind with
            | "station-order" when atLeastActive edge.To && not (atLeastBorrowed edge.From) ->
                Some(
                    DagViolation(
                        sprintf
                            "station-order: %s requires %s at least borrowed before active downstream"
                            edge.To
                            edge.From
                    )
                )
            | "borrow" when atLeastBorrowed edge.To && not (atLeastBorrowed edge.From) ->
                Some(DagViolation(sprintf "borrow: %s requires %s at least borrowed" edge.To edge.From))
            | "parent" when atLeastActive edge.To && not (atLeastBorrowed edge.From) ->
                Some(DagViolation(sprintf "parent: %s requires %s at least borrowed" edge.To edge.From))
            | _ -> None)
        |> function
            | Some error -> Error error
            | None -> Ok()

    let nodeIds (document: ManifestDocument) =
        document.Nodes |> List.map (fun node -> AblationNodeId.create node.Id)

    let private modesFromProfile (document: ManifestDocument) (name: string) (profiles: ProfilesDocument) =
        match profiles.Profiles |> Map.tryFind name with
        | None -> Error(UnknownProfile name)
        | Some profileModes ->
            document.Nodes
            |> List.fold
                (fun state node ->
                    state
                    |> Result.bind (fun acc ->
                        match profileModes |> Map.tryFind node.Id with
                        | Some raw ->
                            match AblationMode.parse raw with
                            | Some mode -> Ok(Map.add (AblationNodeId.create node.Id) mode acc)
                            | None -> Error(InvalidMode(node.Id, raw))
                        | None -> Ok(Map.add (AblationNodeId.create node.Id) AblationMode.Ablated acc)))
                (Ok Map.empty)

    let buildRegistry
        (document: ManifestDocument)
        (profileName: string option)
        (explicit: Map<AblationNodeId, AblationMode>)
        : Result<AblationRegistry, AblationLoadError> =
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
                      NodeCount = document.Nodes.Length } }))
