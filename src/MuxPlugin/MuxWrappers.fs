module VibeFs.MuxPlugin.MuxWrappers

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Mux.TodoWriteNudge
open VibeFs.MuxPlugin.MuxTools.IoTools
open VibeFs.Shell.TreeSitterShell

let private bindExecute (tool: obj) : obj = tool?execute

let private disabledResult () : JS.Promise<string> =
    async { return "disabled" } |> Async.StartAsPromise

/// Append syntax-check diagnostics to a tool result after a file edit.
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

/// Create the syntax-check wrappers for file_edit tools.
let private mkSyntaxWrappers () : obj array =
    [| mkResultWrapper "file_edit_replace_string" (fun result args config -> applySyntaxCheck result args config)
       mkResultWrapper "file_edit_insert" (fun result args config -> applySyntaxCheck result args config) |]

/// Create the todo_write nudge wrapper.
let private mkTodoNudgeWrapper () : obj =
    mkSyncResultWrapper "todo_write" (fun result -> appendReverieNudge result)

/// Capture the host file_read tool so the plugin read tool can delegate to it.
let private mkFileReadCapture () : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (hostTool: obj) (_config: obj) ->
            hostFileReadExecute <- Some (bindExecute hostTool)
            let execFn =
                System.Func<obj, obj, JS.Promise<string>>(fun (_args: obj) (_opts: obj) ->
                    disabledResult ())
            createObj [ "execute", box execFn ])
    createObj [ "targetTool", box "file_read"; "wrapper", box wrapperFn ]

/// Create a web-override wrapper that replaces a host tool with the plugin definition.
/// Uses Func delegate so it emits a real two-arg JS function.
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

/// Build all wrappers.
let createAllWrappers (tools: obj) : obj array =
    Array.append
        (mkSyntaxWrappers ())
        [| mkFileReadCapture ()
           mkTodoNudgeWrapper ()
           mkWebOverride "websearch" tools "web_search"
           mkWebOverride "webfetch" tools "web_fetch" |]
