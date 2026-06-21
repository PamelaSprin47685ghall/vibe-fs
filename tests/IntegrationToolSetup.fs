module VibeFs.Tests.IntegrationToolSetup

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Shell.WikiFiles
open VibeFs.Tests.TempWorkspace

[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let requireFn : string -> obj = createRequire'(string importMeta?url)
let fsAsync : obj = requireFn("fs")?promises
let pathModule : obj = requireFn("path")

let unlinkAsync (p: string) : JS.Promise<unit> =
    unbox (fsAsync?unlink(p))

let wikiEntry idStr q a : WikiEntry =
    match tryParseId idStr with
    | Some id -> { id = id; q = q; a = a }
    | None -> failwithf "invalid wiki id: %s" idStr

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

let executorModeSchema (pluginObject: obj) : obj =
    let schema = executorSchema pluginObject
    let direct = get schema "mode"
    if not (isNullish direct) then direct
    else
        let shape = get schema "shape"
        if not (isNullish shape) then get shape "mode"
        else
            let properties = get schema "properties"
            if isNullish properties then null else get properties "mode"

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

let pluginWikiRuntime (pluginObject: obj) : obj =
    get pluginObject "__wikiRuntime"

let takeBookkeeperLaunchesForTesting (pluginObject: obj) : obj array =
    let wikiRuntime = pluginWikiRuntime pluginObject
    let takeLaunches = get wikiRuntime "takeBookkeeperLaunchesForTesting"
    if typeIs takeLaunches "function" then
        unbox<obj[]> ((takeLaunches $ null))
    else
        [||]

let waitForBackgroundJobsForTesting (pluginObject: obj) : JS.Promise<unit> =
    let wikiRuntime = pluginWikiRuntime pluginObject
    let waiter = get wikiRuntime "waitForBackgroundJobsForTesting"
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
                box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = messages |} }))
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

let writeWikiFileAsync (filePath: string) (header: WikiHeader) (entries: WikiEntry list) : JS.Promise<unit> =
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

let wikiDraftEntry (id: string option) (q: string) (a: string) : obj =
    let fields =
        [ match id with
          | Some value -> yield "id", box value
          | None -> ()
          yield "q", box q
          yield "a", box a ]
    createObj fields

let registerWikiJobForTest (wikiRuntime: obj) (sessionID: string) (workspaceRoot: string) (kindTag: string) (payload: obj) : unit =
    let registrar = get wikiRuntime "registerJobForTesting" |> unbox<System.Func<string, string, string, obj, unit>>
    registrar.Invoke(sessionID, workspaceRoot, kindTag, payload)

let submitWikiTool (pluginObject: obj) : obj =
    get (get pluginObject "tool") "return_bookkeeper"

let readProjectionAsync (workspaceRoot: string) : JS.Promise<WikiProjection> =
    readProjection workspaceRoot

let readAllWikiFiles (workspaceRoot: string) : JS.Promise<WikiFile list> =
    readWikiFiles workspaceRoot
