module Wanxiangshu.Tests.OmpPluginTestsHarness

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Omp.Plugin

type PiHarness =
    { hookStore: obj
      tools: ResizeArray<obj>
      commands: ResizeArray<obj>
      messages: ResizeArray<obj> }

let createPiHarness () : PiHarness =
    let tools = ResizeArray<obj>()
    let commands = ResizeArray<obj>()
    let messages = ResizeArray<obj>()
    let hookStore =
        createObj [
            "tools", box tools
            "commands", box commands
            "messages", box messages
            "events", box(createObj [])
            "activeTools",
                box
                    [| "read"; "edit"; "write"; "find"; "fuzzy_find"; "fuzzy_grep"; "lsp"; "browser"; "search"; "glob"
                       "bash"; "coder"; "investigator"; "meditator"; "executor"; "executor_wait"; "executor_abort"
                       "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "todowrite" |]
        ]
    { hookStore = hookStore; tools = tools; commands = commands; messages = messages }

let piObject (h: PiHarness) : obj =
    let tb =
        createObj [
            "Type",
                box(
                    createObj [
                        "Object", box(fun (p: obj) -> createObj [ "type", box "object"; "properties", box p ])
                        "String", box(fun (o: obj) -> createObj [ "type", box "string" ])
                        "Number", box(fun (o: obj) -> createObj [ "type", box "number" ])
                        "Boolean", box(fun (o: obj) -> createObj [ "type", box "boolean" ])
                        "Null", box(fun (_: obj) -> createObj [ "type", box "null" ])
                        "Union", box(fun (items: obj array) -> createObj [ "anyOf", box items ])
                        "Enum", box(fun (values: obj array) (o: obj) -> createObj [ "type", box "enum"; "values", box values ])
                        "Array",
                            box(System.Func<obj, obj, obj>(fun (items: obj) (opts: obj) ->
                                let result = createObj [ "type", box "array"; "items", box items ]
                                if not (Dyn.isNullish opts) then
                                    let mi = Dyn.get opts "minItems"
                                    if not (Dyn.isNullish mi) then result?("minItems") <- mi
                                    let d = Dyn.get opts "description"
                                    if not (Dyn.isNullish d) then result?("description") <- d
                                result))
                        "Optional", box(fun (schema: obj) -> schema)
                    ])
        ]
    let pi =
        emitJsExpr h.hookStore
            """((hs) => ({
            on(event, handler) {
                if (!hs.events[event]) hs.events[event] = [];
                hs.events[event].push(handler);
            },
            registerTool(tool) { hs.tools.push(tool); },
            registerCommand(name, config) { hs.commands.push({ name, config }); },
            sendMessage(message, options) { hs.messages.push({ message, options }); },
            getActiveTools() { return hs.activeTools; },
            setActiveTools(names) {
                hs.activeTools = names;
                return Promise.resolve();
            },
            getAllTools() { return hs.activeTools; }
        }))($0)"""
        |> unbox<obj>
    pi?("typebox") <- tb
    pi

let eventHandler (h: PiHarness) (event: string) : obj =
    let handlers = Dyn.get (Dyn.get h.hookStore "events") event
    if Dyn.isArray handlers then
        let arr = unbox<obj array> handlers
        if arr.Length > 0 then arr.[0]
        else failwith ("missing handler for " + event)
    else
        failwith ("missing handler for " + event)

let activeTools (h: PiHarness) : string array =
    unbox<string array> (Dyn.get h.hookStore "activeTools")

let toolNames (h: PiHarness) =
    h.tools |> Seq.map (fun t -> Dyn.str t "name") |> Seq.toList |> List.rev |> Set.ofList

let resetPluginState () = resetOmpPluginTestState ()

let lastMessageCustomType (h: PiHarness) : string =
    let entry = h.messages.[h.messages.Count - 1]
    Dyn.str (Dyn.get entry "message") "customType"

let invokeHandler (h: PiHarness) (event: string) (eventObj: obj) (ctx: obj) =
    emitJsExpr (eventHandler h event, eventObj, ctx)
        "Promise.resolve($0($1, $2))"
    |> unbox<JS.Promise<unit>>

let todoPhaseEntries () : obj array =
    let task = createObj [ "status", box "pending" ]
    let phase = createObj [ "tasks", box [| task |] ]
    let entry =
        createObj [
            "customType", box "todo-phases"
            "content", box [| phase |]
        ]
    [| entry |]