module Wanxiangshu.Tests.OmpFuzzyTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Hosts.Omp.Plugin
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Kernel.FuzzyQuery

let private createPiHarness () =
    let tools = ResizeArray<obj>()

    let hookStore =
        createObj
            [ "tools", box tools
              "commands", box (ResizeArray<obj>())
              "messages", box (ResizeArray<obj>())
              "events", box (createObj [])
              "activeTools", box [||] ]

    tools, hookStore

let private piObject (hookStore: obj) : obj =
    let tb =
        createObj
            [ "Type",
              box (
                  createObj
                      [ "Object", box (fun (p: obj) -> createObj [ "type", box "object"; "properties", box p ])
                        "String", box (fun (_: obj) -> createObj [ "type", box "string" ])
                        "Number", box (fun (_: obj) -> createObj [ "type", box "number" ])
                        "Boolean", box (fun (_: obj) -> createObj [ "type", box "boolean" ])
                        "Null", box (fun (_: obj) -> createObj [ "type", box "null" ])
                        "Union", box (fun (items: obj array) -> createObj [ "anyOf", box items ])
                        "Enum",
                        box (fun (values: obj array) (_: obj) -> createObj [ "type", box "enum"; "values", box values ])
                        "Array", box (fun (items: obj) -> createObj [ "type", box "array"; "items", box items ])
                        "Optional", box (fun (schema: obj) -> schema) ]
              ) ]

    emitJsExpr
        hookStore
        """((hs) => ({
        on(event, handler) {
            if (!hs.events[event]) hs.events[event] = [];
            hs.events[event].push(handler);
        },
        registerTool(tool) { hs.tools.push(tool); },
        registerCommand() {},
        sendMessage() {},
        getActiveTools() { return hs.activeTools; },
        setActiveTools(names) { hs.activeTools = names; return Promise.resolve(); }
    }))($0)"""
    |> unbox<obj>
    |> fun pi ->
        pi?("typebox") <- tb
        pi

let fuzzyFindIteratorSingleUse () =
    let store = createTypedIteratorStore 10

    let state: FuzzyFindState =
        { query = "src main"
          pageSize = 30
          pageIndex = 1
          externalBasePath = None }

    let id = storeFindIterator store "scope" state
    let resumed = consumeFindIterator store id
    check "find resume present" resumed.IsSome
    equal "find pageIndex" 1 resumed.Value.pageIndex
    check "find single-use" ((consumeFindIterator store id).IsNone)

let fuzzyGrepIteratorSingleUse () =
    let store = createTypedIteratorStore 10

    let core: FuzzyGrepState =
        { query = "x"
          mode = "plain"
          smartCase = true
          beforeContext = 0
          afterContext = 0
          pageSize = 50
          externalBasePath = Some "/tmp/demo" }

    let wrapped = { core = core; cursor = None }
    let id = storeGrepIterator store "scope" wrapped
    let resumed = consumeGrepIterator store id
    check "grep resume present" resumed.IsSome
    equal "grep external path" (Some "/tmp/demo") resumed.Value.core.externalBasePath
    check "grep single-use" ((consumeGrepIterator store id).IsNone)

let registeredFuzzyToolsExposeIteratorParam () =
    promise {
        let tools, hookStore = createPiHarness ()
        let pi = piObject hookStore
        do! wanxiangshuExtension pi

        let continueTool = tools |> Seq.find (fun t -> str t "name" = "fuzzy_continue")
        let props = Dyn.get (Dyn.get continueTool "parameters") "properties"
        check "fuzzy_continue has iterator param" (Dyn.has props "iterator")

        for toolName in [| "fuzzy_find"; "fuzzy_grep" |] do
            let tool = tools |> Seq.find (fun t -> str t "name" = toolName)
            let props = Dyn.get (Dyn.get tool "parameters") "properties"
            check (toolName + " has no iterator param") (not (Dyn.has props "iterator"))
    }
