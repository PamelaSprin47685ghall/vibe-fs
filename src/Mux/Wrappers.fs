module VibeFs.Mux.Wrappers

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Shell.TreeSitterShell
open VibeFs.Mux.CallStore

type JsonSchema =
    { ``type``: string
      properties: obj
      required: string array option
      additionalProperties: bool option }

type ToolDefinition =
    { name: string
      description: string
      parameters: JsonSchema
      execute: obj -> obj -> JS.Promise<string>
      condition: (obj -> bool) option }

let mutable registeredToolNames: string array = [||]

let resolveStr (s: string) : JS.Promise<string> = async { return s } |> Async.StartAsPromise

let jsonStringify (o: obj) : string = JS.JSON.stringify(o)

let optInt (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<int> v)
let optBool (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<bool> v)
let optField (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some v

let strField (a: obj) (k: string) : string option =
    let v = Dyn.get a k
    if Dyn.isNullish v then None else Some(string v)

let requireStrArray (a: obj) (k: string) : string array =
    let v = Dyn.get a k
    if Dyn.isNullish v || not (Dyn.isArray v) then [||]
    else v :?> obj array |> Array.map string

let mkSchema (props: obj) (required: string array) : JsonSchema =
    { ``type`` = "object"; properties = props; required = Some required; additionalProperties = Some false }

let strProp (desc: string) : obj = createObj [ "type", box "string"; "description", box desc ]
let numProp (desc: string) : obj = createObj [ "type", box "number"; "description", box desc ]
let boolProp (desc: string) : obj = createObj [ "type", box "boolean"; "description", box desc ]
let strEnumProp (desc: string) (values: string array) : obj = createObj [ "type", box "string"; "enum", box values; "description", box desc ]
let strArrayProp (desc: string) : obj =
    createObj [ "type", box "array"; "items", box (createObj [ "type", box "string" ]); "description", box desc ]

let requireWorkspaceId (config: obj) (toolName: string) : Result<string, string> =
    let wid = Dyn.get config "workspaceId"
    if isNull wid || string wid = "" then Result.Error $"{toolName} requires workspaceId"
    else Result.Ok(string wid)

let private applySyntaxCheck (result: obj) (args: obj) (config: obj) : JS.Promise<obj> =
    async {
        match extractFilePath args with
        | None -> return result
        | Some filePath ->
            try
                let! formatted = readAndCheckSyntax filePath (Dyn.str config "cwd") false |> Async.AwaitPromise
                match formatted with
                | None -> return result
                | Some f ->
                    if Dyn.typeIs result "string" then return box (string result + "\n\n" + f)
                    elif Dyn.typeIs result "object" && Dyn.truthy (Dyn.get result "success") then
                        return Dyn.withKey result "syntax_diagnostics" (box f)
                    else return result
            with _ -> return result
    } |> Async.StartAsPromise

let private bindExecute (tool: obj) : obj = tool?execute

let private disabledResult () : JS.Promise<string> =
    async { return "disabled" } |> Async.StartAsPromise

let private appendMeditatorNudge (result: obj) : obj =
    if Dyn.isNullish result then result
    elif Dyn.typeIs result "string" then
        let s = string result
        if s.Contains(meditatorNudge : string) then result else box $"{s}\n\n{meditatorNudge}"
    elif Dyn.typeIs result "object" then
        let success = Dyn.get result "success"
        if not (Dyn.isNullish success) && unbox<bool> success then
            let existingNudge = Dyn.get result "nudge"
            if not (Dyn.isNullish existingNudge) && (string existingNudge).Contains(meditatorNudge : string) then result
            else Dyn.withKey result "nudge" (box meditatorNudge)
        else result
    else result

type HostReadExec = obj option ref

let agentReportDefinition (_store: CallStore) : ToolDefinition =
    { name = "agent_report"
      description = "Submit structured work results. Provide callId plus the stage fields; the plugin forwards a markdown rendering to the upstream UI."
      parameters =
          { ``type`` = "object"
            properties =
                createObj
                    [ "reportMarkdown", box (createObj [ "type", box "string"; "description", box "Human-friendly markdown shown in the upstream UI." ])
                      "callId", box (createObj [ "type", box "string"; "description", box "Internal call id supplied by the prompt." ])
                      "verdict", box (createObj [ "type", box "string"; "description", box "Verdict string (e.g. PASS or REJECT)." ])
                      "feedback", box (createObj [ "type", box "string"; "description", box "Detailed feedback when rejecting." ]) ]
            required = Some [| "callId" |]
            additionalProperties = Some true }
      execute = fun _config args ->
          let callId = defaultArg (strField args "callId") ""
          if callId = "" then
              resolveStr (defaultArg (strField args "reportMarkdown") "")
          elif resolveCall _store callId args then
              resolveStr "Submitted."
          else
              resolveStr $"No pending call for {callId}"
      condition = None }

let formatAgentReportMarkdown (args: obj) : string =
    let copied = Dyn.clone args
    copied?("callId") <- null
    let content = JS.JSON.stringify(copied)
    "# Agent Report\n\n```json\n" + content + "\n```"

let private isThenable (value: obj) : bool =
    not (Dyn.isNullish value) && Dyn.typeIs (Dyn.get value "then") "function"

let private mkResultWrapper (targetTool: string) (callback: obj -> obj -> obj -> JS.Promise<obj>) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun tool config ->
            let orig = tool?execute
            if not (Dyn.typeIs orig "function") then
                tool
            else
                let executeFn =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args opts ->
                        async {
                            let raw = tool?execute(args, opts)
                            let! v =
                                if isThenable raw then
                                    Async.AwaitPromise(unbox<JS.Promise<obj>> raw)
                                else
                                    async { return raw }
                            return! callback v args config |> Async.AwaitPromise
                        }
                        |> Async.StartAsPromise)
                Dyn.withKey tool "execute" (box executeFn))
    createObj [ "targetTool", box targetTool; "wrapper", box wrapperFn ]

let private mkSyncResultWrapper (targetTool: string) (callback: obj -> obj) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun tool config ->
            let orig = tool?execute
            if not (Dyn.typeIs orig "function") then
                tool
            else
                let executeFn =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args opts ->
                        async {
                            let raw = tool?execute(args, opts)
                            let! v =
                                if isThenable raw then
                                    Async.AwaitPromise(unbox<JS.Promise<obj>> raw)
                                else
                                    async { return raw }
                            return callback v
                        }
                        |> Async.StartAsPromise)
                Dyn.withKey tool "execute" (box executeFn))
    createObj [ "targetTool", box targetTool; "wrapper", box wrapperFn ]

let private mkSyntaxWrappers () : obj array =
    [| mkResultWrapper "file_edit_replace_string" (fun result args config -> applySyntaxCheck result args config)
       mkResultWrapper "file_edit_insert" (fun result args config -> applySyntaxCheck result args config) |]

let private mkTodoNudgeWrapper (host: Host) : obj =
    mkSyncResultWrapper (todoWritePromptName host) (fun result -> appendMeditatorNudge result)

let private mkFileReadCapture (hostReadExec: HostReadExec) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (hostTool: obj) (_config: obj) ->
            hostReadExec.Value <- Some (bindExecute hostTool)
            let execFn =
                System.Func<obj, obj, JS.Promise<string>>(fun (_args: obj) (_opts: obj) ->
                    disabledResult ())
            createObj [ "execute", box execFn ])
    createObj [ "targetTool", box "file_read"; "wrapper", box wrapperFn ]

let private mkWebOverride (sourceToolName: string) (tools: obj) (targetTool: string) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (_tool: obj) (config: obj) ->
            let def = Dyn.get tools sourceToolName
            let execFn =
                System.Func<obj, obj, JS.Promise<string>>(fun (args: obj) (opts: obj) ->
                    let abortSignal = if Dyn.isNullish opts then unbox null else Dyn.get opts "abortSignal"
                    let mergedConfig = Dyn.withKey config "abortSignal" abortSignal
                    Dyn.call2 (Dyn.get def "execute") mergedConfig args :?> JS.Promise<string>)
            createObj [ "description", box (Dyn.str def "description")
                        "parameters", box (Dyn.get def "parameters")
                        "execute", box execFn ])
    createObj [ "targetTool", box targetTool; "wrapper", box wrapperFn ]

let private mkAgentReportOverride (callStore: CallStore) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (tool: obj) (_config: obj) ->
            let execFn =
                System.Func<obj, obj, JS.Promise<obj>>(fun (args: obj) (opts: obj) ->
                    async {
                        let callId = Dyn.str args "callId"
                        if callId <> "" && hasCall callStore callId then
                            resolveCall callStore callId args |> ignore
                            let upstreamArgs = createObj [ "reportMarkdown", box (formatAgentReportMarkdown args) ]
                            let raw = tool?execute(upstreamArgs, opts)
                            return!
                                if isThenable raw then
                                    Async.AwaitPromise(unbox<JS.Promise<obj>> raw)
                                else
                                    async { return raw }
                        else
                            let raw = tool?execute(args, opts)
                            return!
                                if isThenable raw then
                                    Async.AwaitPromise(unbox<JS.Promise<obj>> raw)
                                else
                                    async { return raw }
                    }
                    |> Async.StartAsPromise)
            let definition = agentReportDefinition callStore
            createObj [ "description", box definition.description
                        "parameters", box definition.parameters
                        "execute", box execFn ])
    createObj [ "targetTool", box "agent_report"; "wrapper", box wrapperFn ]


let createAllWrappersFor (host: Host) (tools: obj) (hostReadExec: HostReadExec) (callStore: CallStore) : obj array =
    Array.append
        (mkSyntaxWrappers ())
        [| mkFileReadCapture hostReadExec
           mkTodoNudgeWrapper host
           mkAgentReportOverride callStore
           mkWebOverride "websearch" tools "web_search"
           mkWebOverride "webfetch" tools "web_fetch" |]

let createAllWrappers (tools: obj) (hostReadExec: HostReadExec) (callStore: CallStore) : obj array =
    createAllWrappersFor opencode tools hostReadExec callStore
