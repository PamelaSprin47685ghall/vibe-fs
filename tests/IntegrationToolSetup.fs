module VibeFs.Tests.IntegrationToolSetup

open Fable.Core
open Fable.Core.JsInterop

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphCodec
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Tests.TempWorkspace
open VibeFs.Shell.Dyn

[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let requireFn : string -> obj = createRequire'(string importMeta?url)
let fsAsync : obj = requireFn("fs")?promises
let pathModule : obj = requireFn("path")

let unlinkAsync (p: string) : JS.Promise<unit> =
    unbox (fsAsync?unlink(p))

let knowledgeGraphEntry idStr entity fact : KnowledgeGraphEntry =
    match tryParseId idStr with
    | Some id -> { id = id; entity = entity; fact = fact }
    | None -> failwithf "invalid knowledge graph id: %s" idStr

let dayMs (date: string) : float =
    match date.Split('-') with
    | [| year; month; day |] ->
        System.DateTimeOffset(int year, int month, int day, 0, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds() |> float
    | _ -> failwithf "invalid date: %s" date

let executorDefinition (pluginObject: obj) : obj =
    get (get pluginObject "tool") "executor"

let objectKeys (value: obj) : string array =
    JS.Constructors.Object.keys(value) |> Seq.toArray

let executorSchema (pluginObject: obj) : obj =
    let definition = executorDefinition pluginObject
    let args = get definition "args"
    if not (isNullish args) then args else get definition "parameters"

let private executorFieldSchema (pluginObject: obj) (field: string) : obj =
    let schema = executorSchema pluginObject
    let direct = get schema field
    if not (isNullish direct) then direct
    else
        let shape = get schema "shape"
        if not (isNullish shape) then get shape field
        else
            let properties = get schema "properties"
            if isNullish properties then null else get properties field

let executorModeSchema (pluginObject: obj) : obj = executorFieldSchema pluginObject "mode"
let executorLanguageSchema (pluginObject: obj) : obj = executorFieldSchema pluginObject "language"

let enumValues (modeSchema: obj) : string array =
    let candidates =
        [ get (get modeSchema "def") "entries"
          get modeSchema "enum"
          get modeSchema "options" ]
    candidates
    |> List.tryPick (fun candidate ->
        if isNullish candidate then None
        elif isArray candidate then Some (unbox<obj[]> candidate |> Array.map string)
        else
            let values = objectKeys candidate
            if values.Length = 0 then None else Some values)
    |> Option.defaultValue [||]

let pluginKnowledgeGraphRuntime (pluginObject: obj) : obj =
    get pluginObject "__knowledgeGraphRuntime"

let takeBookkeeperLaunchesForTesting (pluginObject: obj) : obj array =
    let kgRuntime = pluginKnowledgeGraphRuntime pluginObject
    let takeLaunches = get kgRuntime "takeBookkeeperLaunchesForTesting"
    if typeIs takeLaunches "function" then
        unbox<obj[]> ((takeLaunches $ null))
    else
        [||]

let waitForBackgroundJobsForTesting (pluginObject: obj) : JS.Promise<unit> =
    let kgRuntime = pluginKnowledgeGraphRuntime pluginObject
    let waiter = get kgRuntime "waitForBackgroundJobsForTesting"
    if typeIs waiter "function" then
        unbox<JS.Promise<unit>> ((waiter $ null))
    else
        Promise.lift ()

let bookkeeperMockClient (messages: obj array) : obj =
    createObj [
        "session",
        box (
            createObj [
                "messages",
                box (
                    System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = messages |} }))
                "todo",
                box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
                "prompt",
                box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
                "create",
                box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = box {| id = "child-bookkeeper-session" |} |} }))
                "abort",
                box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
            ]
        )
    ]

let assistantCompletionMessage (sessionID: string) (text: string) : obj =
    box {| info = createObj [ "id", box (sessionID + "-assistant"); "agent", box "manager"; "sessionID", box sessionID; "role", box "assistant"; "finish", box "stop"; "time", box (createObj [ "created", box 1; "completed", box 2 ]) ]
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

let userTextMessage (sessionID: string) (text: string) : obj =
    box {| info = createObj [ "id", box (sessionID + "-user"); "agent", box "bookkeeper"; "sessionID", box sessionID; "role", box "user" ]
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

let writeKnowledgeGraphFileAsync (filePath: string) (header: KnowledgeGraphHeader) (entries: KnowledgeGraphEntry list) : JS.Promise<unit> =
    writeFileAsync filePath (renderNdjson header entries)

let sampleCoderIntent (objective: string) (file: string) : obj =
    createObj
        [ "objective", box objective
          "background", box "test background"
          "targets", box [| createObj [ "file", box file; "guide", box "test guide" ] |] ]

let sampleCoderIntentWithDoNotTouch (objective: string) (file: string) (doNotTouch: string array) : obj =
    createObj
        [ "objective", box objective
          "background", box "test background"
          "do_not_touch", box doNotTouch
          "targets", box [| createObj [ "file", box file; "guide", box "test guide" ] |] ]

let sampleInvestigatorIntent (objective: string) : obj =
    createObj
        [ "objective", box objective
          "background", box "test background"
          "questions", box [| box "What did you find?" |] ]

let knowledgeGraphDraftEntry (id: string option) (entities: string list) (fact: string) : obj =
    let fields =
        [ match id with
          | Some value -> yield "id", box value
          | None -> ()
          yield "entity", box (Array.ofList entities)
          yield "fact", box fact ]
    createObj fields

let registerKnowledgeGraphJobForTest (kgRuntime: obj) (sessionID: string) (workspaceRoot: string) (kindTag: string) (payload: obj) : unit =
    let registrar = get kgRuntime "registerJobForTesting" |> unbox<System.Func<string, string, string, obj, unit>>
    registrar.Invoke(sessionID, workspaceRoot, kindTag, payload)

let submitKnowledgeGraphTool (pluginObject: obj) : obj =
    get (get pluginObject "tool") "return_bookkeeper"

let readKnowledgeGraphProjectionAsync (workspaceRoot: string) : JS.Promise<KnowledgeGraphProjection> =
    readProjection workspaceRoot

let readAllKnowledgeGraphFiles (workspaceRoot: string) : JS.Promise<KnowledgeGraphFile list> =
    readKnowledgeGraphFiles workspaceRoot
