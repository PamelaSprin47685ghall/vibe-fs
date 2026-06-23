module VibeFs.Tests.IntegrationToolSetup

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Shell.KnowledgeGraphFiles
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

let executorLanguageSchema (pluginObject: obj) : obj =
    let schema = executorSchema pluginObject
    let direct = get schema "language"
    if not (isNullish direct) then direct
    else
        let shape = get schema "shape"
        if not (isNullish shape) then get shape "language"
        else
            let properties = get schema "properties"
            if isNullish properties then null else get properties "language"

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

let muxToolByName (reg: obj) (name: string) : obj =
    let tools = unbox<obj[]> (get reg "tools")
    tools
    |> Array.tryFind (fun t -> str t "name" = name)
    |> Option.defaultValue null

let muxToolSchema (toolDef: obj) : obj =
    if isNullish toolDef then null else get toolDef "parameters"

let muxToolSchemaRequired (toolDef: obj) : string array =
    if isNullish toolDef then [||]
    else
        let schema = muxToolSchema toolDef
        if isNullish schema then [||]
        else
            let req = get schema "required"
            if isArray req then unbox<string[]> req else [||]

let muxExecutorModeSchema (reg: obj) : obj =
    let executor = muxToolByName reg "executor"
    let schema = muxToolSchema executor
    if isNullish schema then null
    else
        let props = get schema "properties"
        if isNullish props then null else get props "mode"

let muxKnowledgeGraphRuntime (reg: obj) : obj =
    let direct = get reg "__knowledgeGraphRuntime"
    if not (isNullish direct) then direct
    else
        let rt = get reg "knowledgeGraphRuntime"
        if isNullish rt then null else rt

let muxReviewStore (reg: obj) : obj = get reg "__reviewStore"
let muxCallStore (reg: obj) : obj = get reg "__callStore"

let muxActivateReviewForTest (reg: obj) (sessionID: string) (task: string) : unit =
    let store = muxReviewStore reg
    let activate = get store "activateReview" |> unbox<System.Func<string, string, int64, unit>>
    activate.Invoke(sessionID, task, 0L)

let muxIsReviewActiveForTest (reg: obj) (sessionID: string) : bool =
    let store = muxReviewStore reg
    let fn = get store "isReviewActive" |> unbox<System.Func<string, bool>>
    fn.Invoke(sessionID)

let muxPendingCallIdsForTest (reg: obj) : string array =
    let store = muxCallStore reg
    let fn = get store "pendingCallIds" |> unbox<System.Func<string array>>
    fn.Invoke()

let muxResolveFirstMatchingCallForTest (reg: obj) (prefix: string) (args: obj) : bool =
    let store = muxCallStore reg
    let fn = get store "resolveFirstMatching" |> unbox<System.Func<string, obj, bool>>
    fn.Invoke(prefix, args)

let minimalMuxDeps () : obj =
    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj []))) ]

let muxDepsWithChatHistory (sessionID: string) (messages: obj array) : obj =
    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
          "getChatHistory",
          box (System.Func<string, JS.Promise<obj array>>(fun sid ->
              promise { return if sid = sessionID then messages else [||] })) ]

let muxMutableDepsWithChatHistory (sessionID: string) (messages: ResizeArray<obj>) : obj =
    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
          "getChatHistory",
          box (System.Func<string, JS.Promise<obj array>>(fun sid ->
              promise { return if sid = sessionID then messages.ToArray() else [||] })) ]

let mockMuxTaskServiceCapturingPrompt (prompts: ResizeArray<string>) : obj =
    createObj
        [ "create",
          box (System.Func<obj, JS.Promise<obj>>(fun input ->
              promise {
                  let promptText = str input "prompt"
                  if promptText <> "" then prompts.Add(promptText)
                  return box {| success = true; data = box {| taskId = "reviewer-task-1"; kind = "agent" |} |}
              }))
          "waitForAgentReport",
          box (System.Func<string, obj, JS.Promise<obj>>(fun _ _ ->
              Promise.reject (exn "simulated reviewer timeout"))) ]

let registerMuxKnowledgeGraphJobForTest (reg: obj) (sessionID: string) (workspaceRoot: string) (kindTag: string) (payload: obj) : unit =
    let runtime = muxKnowledgeGraphRuntime reg
    let registrar = get runtime "registerJobForTesting" |> unbox<System.Func<string, string, string, obj, unit>>
    registrar.Invoke(sessionID, workspaceRoot, kindTag, payload)

let readKnowledgeGraphProjectionAsync (workspaceRoot: string) : JS.Promise<KnowledgeGraphProjection> =
    readProjection workspaceRoot

let readAllKnowledgeGraphFiles (workspaceRoot: string) : JS.Promise<KnowledgeGraphFile list> =
    readKnowledgeGraphFiles workspaceRoot

let muxMessageTransform (reg: obj) : obj =
    get reg "messagesTransform"

let muxTextMessage (id: string) (role: string) (text: string) : obj =
    box {| id = id; role = role; parts = [| box {| ``type`` = "text"; text = text; state = "done" |} |] |}

let firstTextPartText (msg: obj) : string =
    let parts = get msg "parts"
    if isNullish parts then ""
    else
        let arr = unbox<obj[]> parts
        if arr.Length = 0 then ""
        else str arr.[0] "text"

let hasDynamicToolReadPart (msg: obj) : bool =
    let parts = get msg "parts"
    if isNullish parts then false
    else
        unbox<obj[]> parts
        |> Array.exists (fun p ->
            str p "type" = "dynamic-tool"
            && str p "toolName" = "file_read")
