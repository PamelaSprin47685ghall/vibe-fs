module Wanxiangshu.Tests.OmpTodoToolTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Omp.TodoTool

let private findTodoTool (h: PiHarness) : obj =
    h.tools |> Seq.find (fun t -> Dyn.str t "name" = "todowrite")

let private invokeExecute (tool: obj) (params': obj) : JS.Promise<obj> =
    let exec = Dyn.get tool "execute"
    emitJsExpr (exec, box "call-1", params', null, null, null) "$0($1)($2)($3)($4)($5)"
    |> unbox<JS.Promise<obj>>

let private hasText (result: obj) (substring: string) : bool =
    let content = Dyn.get result "content"
    if Dyn.isArray content then
        let arr = unbox<obj array> content
        arr
        |> Array.exists (fun entry ->
            let t = Dyn.str entry "text"
            t.Contains(substring))
    else
        let t = string content
        t.Contains(substring)

let registerTodoTool_addsTool () =
    let h = createPiHarness ()
    let pi = piObject h
    registerTodoTool pi
    check "tool registered" (toolNames h |> Set.contains "todowrite")

let execute_missingReportReturnsError () : JS.Promise<unit> =
    let h = createPiHarness ()
    let pi = piObject h
    registerTodoTool pi
    let tool = findTodoTool h
    let params' =
        createObj [
            "completedWorkReport", box ""
            "select_methodology", box [| "first_principles" |]
            "todos", box [| createObj [ "content", box "x"; "status", box "pending" ] |]
        ]
    promise {
        let! result = invokeExecute tool params'
        check "error when report missing" (Dyn.truthy (Dyn.get result "isError"))
        check "error mentions completedWorkReport" (hasText result "completedWorkReport")
    }

let execute_missingMethodologyReturnsError () : JS.Promise<unit> =
    let h = createPiHarness ()
    let pi = piObject h
    registerTodoTool pi
    let tool = findTodoTool h
    let params' =
        createObj [
            "completedWorkReport", box "done"
            "select_methodology", box [||]
            "todos", box [| createObj [ "content", box "x"; "status", box "pending" ] |]
        ]
    promise {
        let! result = invokeExecute tool params'
        check "error when methodology missing" (Dyn.truthy (Dyn.get result "isError"))
        check "error mentions select_methodology" (hasText result "select_methodology")
    }

let execute_invalidTodoReturnsError () : JS.Promise<unit> =
    let h = createPiHarness ()
    let pi = piObject h
    registerTodoTool pi
    let tool = findTodoTool h
    let params' =
        createObj [
            "completedWorkReport", box "done"
            "select_methodology", box [| "first_principles" |]
            "todos", box [| createObj [ "content", box ""; "status", box "pending" ] |]
        ]
    promise {
        let! result = invokeExecute tool params'
        check "error when todo invalid" (Dyn.truthy (Dyn.get result "isError"))
        check "error mentions todos" (hasText result "todos")
    }

let execute_validReturnsTextResult () : JS.Promise<unit> =
    let h = createPiHarness ()
    let pi = piObject h
    registerTodoTool pi
    let tool = findTodoTool h
    let params' =
        createObj [
            "completedWorkReport", box "## Done\n- fixed"
            "select_methodology", box [| "first_principles" |]
            "todos", box [| createObj [ "content", box "x"; "status", box "pending" ] |]
        ]
    promise {
        let! result = invokeExecute tool params'
        check "not error when valid" (not (Dyn.truthy (Dyn.get result "isError")))
        check "result mentions methodology hint" (hasText result "methodology_first_principles")
        check "result mentions apply" (hasText result "apply")
    }

let run () : JS.Promise<unit> =
    promise {
        clearFailuresForRun ()
        registerTodoTool_addsTool ()
        do! execute_missingReportReturnsError ()
        do! execute_missingMethodologyReturnsError ()
        do! execute_invalidTodoReturnsError ()
        do! execute_validReturnsTextResult ()
    }
