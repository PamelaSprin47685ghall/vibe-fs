module Wanxiangshu.E2e.MimocodePluginTaskAndArgsTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

type Harness =
    abstract plugin: obj
    abstract workDir: string
    abstract home: string
    abstract sessionId: string
    abstract getPlugin: unit -> obj
    abstract getToolNames: unit -> string[]
    abstract getToolEntry: string -> obj
    abstract runToolDefinition: string -> JS.Promise<obj>
    abstract executePluginTool: string -> obj -> obj -> JS.Promise<string>
    abstract runToolWithHooks: string -> obj -> obj -> JS.Promise<string>
    abstract runCommandExecuteBefore: string -> string -> JS.Promise<obj>
    abstract runMessageTransform: obj -> obj -> JS.Promise<obj>
    abstract runSystemTransform: obj -> JS.Promise<obj>
    abstract runConfigHook: obj -> JS.Promise<obj>
    abstract runSessionPost: obj -> JS.Promise<obj>
    abstract runSessionUserQueryPost: obj -> JS.Promise<obj>
    abstract fireEvent: obj -> JS.Promise<obj>
    abstract fireStreamAbort: string -> JS.Promise<obj>
    abstract getReviewStore: unit -> obj
    abstract readPartsText: obj -> string
    abstract readFile: string -> string
    abstract fileExists: string -> bool
    abstract dispose: unit -> JS.Promise<unit>

let createEmpty () = createObj []

let dynGet (o: obj) (k: string) = get o k
let dynIsNull (o: obj) = isNullish o
let dynIsArr (o: obj) = isArray o
let dynTypeIs (o: obj) (t: string) = typeIs o t
let dynStr (o: obj) (k: string) = str o k

let warnTddValue =
    "i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated"

let warnValue =
    "MUST acknowledge that this task cannot be done with other tools and only run tests when static analysis cannot handle it"

let taskArgsBase todos selectMethodology =
    createObj [ "todos", box todos; "select_methodology", box selectMethodology ]
