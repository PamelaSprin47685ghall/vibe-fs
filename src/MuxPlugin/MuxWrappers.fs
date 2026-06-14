module VibeFs.MuxPlugin.MuxWrappers

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Mux.TodoWriteNudge
open VibeFs.Shell.TreeSitterShell

[<Emit("$0.execute.bind($0)")>]
let private bindExecute (tool: obj) : obj = jsNative

[<Emit("Promise.resolve('disabled')")>]
let private disabledResult () : JS.Promise<string> = jsNative

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

/// Create a wrapper that post-processes each execute result with an async callback.
/// $0 = targetTool, $1 = F# callback (curried: v -> args -> config -> Promise)
[<Emit("{ targetTool: $0, wrapper: (tool, config) => { const orig = tool.execute; if (typeof orig !== 'function') return tool; const cb = $1; const fn = (...a) => { const r = orig.apply(tool, a); const rr = (r && typeof r.then === 'function') ? r : Promise.resolve(r); return rr.then(v => cb(v, a.length > 0 ? a[0] : undefined, config)); }; return { ...tool, execute: fn }; } }")>]
let private mkResultWrapper (targetTool: string) (callback: obj -> obj -> obj -> JS.Promise<obj>) : obj = jsNative

/// Create a wrapper that post-processes each execute result with a sync callback.
/// $0 = targetTool, $1 = F# callback (v -> result)
[<Emit("{ targetTool: $0, wrapper: (tool, config) => { const orig = tool.execute; if (typeof orig !== 'function') return tool; const cb = $1; const fn = (...a) => { const r = orig.apply(tool, a); const rr = (r && typeof r.then === 'function') ? r : Promise.resolve(r); return rr.then(v => cb(v)); }; return { ...tool, execute: fn }; } }")>]
let private mkSyncResultWrapper (targetTool: string) (callback: obj -> obj) : obj = jsNative

/// Create the syntax-check wrappers for file_edit tools.
let private mkSyntaxWrappers () : obj array =
    [| mkResultWrapper "file_edit_replace_string" (fun result args config -> applySyntaxCheck result args config)
       mkResultWrapper "file_edit_insert" (fun result args config -> applySyntaxCheck result args config) |]

/// Create the todo_write nudge wrapper.
let private mkTodoNudgeWrapper () : obj =
    mkSyncResultWrapper "todo_write" (fun result -> appendReverieNudge result)

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

/// Create a wrapper that captures the host's native file_read execute and
/// replaces the tool with a disabled stub so the custom read tool is used.
let private mkFileReadCapture () : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (hostTool: obj) (_config: obj) ->
            VibeFs.MuxPlugin.MuxTools.IoTools.hostFileReadExecute <- Some (bindExecute hostTool)
            let execFn =
                System.Func<obj, obj, JS.Promise<string>>(fun (_config: obj) (_args: obj) ->
                    disabledResult ())
            createObj [ "execute", box execFn ])
    createObj [ "targetTool", box "file_read"; "wrapper", box wrapperFn ]

/// Build all wrappers.
let createAllWrappers (tools: obj) : obj array =
    Array.append
        (mkSyntaxWrappers ())
        [| mkTodoNudgeWrapper ()
           mkWebOverride "websearch" tools "web_search"
           mkWebOverride "webfetch" tools "web_fetch" |]
