module VibeFs.MuxPlugin.MuxWrappers

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Mux.TodoWriteNudge
open VibeFs.MuxPlugin.CallStore
open VibeFs.MuxPlugin.MuxTools.IoTools
open VibeFs.Shell.TreeSitterShell

let private bindExecute (tool: obj) : obj = tool?execute

let private disabledResult () : JS.Promise<string> =
    async { return "disabled" } |> Async.StartAsPromise

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

let private mkTodoNudgeWrapper () : obj =
    mkSyncResultWrapper "todo_write" (fun result -> appendMeditatorNudge result)

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

let createAllWrappers (tools: obj) (hostReadExec: HostReadExec) (callStore: CallStore) : obj array =
    Array.append
        (mkSyntaxWrappers ())
        [| mkFileReadCapture hostReadExec
           mkTodoNudgeWrapper ()
           mkAgentReportOverride callStore
           mkWebOverride "websearch" tools "web_search"
           mkWebOverride "webfetch" tools "web_fetch" |]
