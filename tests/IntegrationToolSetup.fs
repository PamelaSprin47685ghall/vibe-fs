module Wanxiangshu.Tests.IntegrationToolSetup

open Fable.Core
open Fable.Core.JsInterop

open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Runtime.Dyn

[<Import("createRequire", "node:module")>]
let private createRequire': string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta: obj = jsNative

[<Import("mkdtempSync", "node:fs")>]
let private mkdtempSync (template: string) : string = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Import("tmpdir", "node:os")>]
let private tmpdir () : string = jsNative

let requireFn: string -> obj = createRequire' (string importMeta?url)
let fsAsync: obj = get (requireFn "fs") "promises"
let pathModule: obj = requireFn "path"

let muxDepsWithFixedNow () : obj =
    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
          "nowUtc", box (System.Func<unit, System.DateTime>(fun () -> System.DateTime(2026, 6, 25))) ]

let mutable cachedMuxRegistration: obj option = None

let sharedMuxRegistration () : obj =
    match cachedMuxRegistration with
    | Some reg -> reg
    | None ->
        let deps = muxDepsWithFixedNow ()
        let isolatedDir = mkdtempSync (pathJoin (tmpdir ()) "wanxiang-shared-mux-")
        deps?directory <- isolatedDir
        let reg = createRegistration deps
        cachedMuxRegistration <- Some reg
        reg

let unlinkAsync (p: string) : JS.Promise<unit> = unbox (fsAsync?unlink (p))

let executorDefinition (pluginObject: obj) : obj =
    get (get pluginObject "tool") "executor"

let objectKeys (value: obj) : string array =
    JS.Constructors.Object.keys (value) |> Seq.toArray

let executorSchema (pluginObject: obj) : obj =
    let definition = executorDefinition pluginObject
    let args = get definition "args"

    if not (isNullish args) then
        args
    else
        get definition "parameters"

let private executorFieldSchema (pluginObject: obj) (field: string) : obj =
    let schema = executorSchema pluginObject
    let direct = get schema field

    if not (isNullish direct) then
        direct
    else
        let shape = get schema "shape"

        if not (isNullish shape) then
            get shape field
        else
            let properties = get schema "properties"
            if isNullish properties then null else get properties field

let executorModeSchema (pluginObject: obj) : obj = executorFieldSchema pluginObject "mode"

let executorLanguageSchema (pluginObject: obj) : obj =
    executorFieldSchema pluginObject "language"

let enumValues (modeSchema: obj) : string array =
    let candidates =
        [ get (get modeSchema "def") "entries"
          get modeSchema "enum"
          get modeSchema "options" ]

    candidates
    |> List.tryPick (fun candidate ->
        if isNullish candidate then
            None
        elif isArray candidate then
            Some(unbox<obj[]> candidate |> Array.map string)
        else
            let values = objectKeys candidate
            if values.Length = 0 then None else Some values)
    |> Option.defaultValue [||]

let assistantCompletionMessage (sessionID: string) (text: string) : obj =
    box
        {| info =
            createObj
                [ "id", box (sessionID + "-assistant")
                  "agent", box "manager"
                  "sessionID", box sessionID
                  "role", box "assistant"
                  "finish", box "stop"
                  "time", box (createObj [ "created", box 1; "completed", box 2 ]) ]
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

let userTextMessage (sessionID: string) (text: string) : obj =
    box
        {| info =
            createObj
                [ "id", box (sessionID + "-user")
                  "agent", box "user"
                  "sessionID", box sessionID
                  "role", box "user" ]
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

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
