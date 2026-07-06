module Wanxiangshu.E2e.MimoTuiPluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert

[<Emit("$0($1)")>]
let private callRaw (fn: obj) (arg: obj) : obj = jsNative

let private dynIsNull (o: obj) = isNullish o
let private dynTypeIs (o: obj) (t: string) = typeIs o t
let private dynStr (o: obj) (k: string) = str o k
let private dynGet (o: obj) (k: string) = get o k

let private asObjArray (value: obj) : obj array =
    if Dyn.isNullish value || not (Dyn.isArray value) then [||] else unbox<obj array> value

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()

        let! plugin =
            importDefault<obj>("../build/src/Opencode/PluginMimoTui.js")
            |> Promise.lift

        let mutable ok = 0
        let chk label cond =
            check label cond
            if cond then ok <- ok + 1

        // --- 1. Identity ------------------------------------------------------
        chk "tui.id" (dynStr plugin "id" = "vibe-fs-mimo-tui")

        // --- 2. tui is function ---------------------------------------------
        let tui = dynGet plugin "tui"
        chk "tui.isFunction" (not (dynIsNull tui) && dynTypeIs tui "function")

        // --- 3-11. Fallback logic tests --------------------------------------
        let tuiFn = tui

        // Helper: build mock api
        let mutable capturedCmdRegister = None
        let mutable lastReplacedDialog = null
        let mutable toastMessage = ""
        let mutable navigationTarget = ""
        let mutable dialogCleared = false

        let buildMockApi (todoResult: obj) (messages: obj[]) (parts: obj[]) (childrenData: obj[]) =
            let originalTodo = fun (_: obj) -> todoResult
            let sessionState = createObj [
                "todo", box originalTodo
                "messages", box (fun (_: string) -> box messages)
            ]
            let mutable disposeCb = None
            let apiObj = createObj [
                "state", box (createObj [
                    "session", box sessionState
                    "path", box (createObj [ "directory", box "/tmp/test" ])
                    "part", box (fun (_: string) -> box parts)
                ])
                "client", box (createObj [
                    "session", box (createObj [
                        "get", box (fun _ -> Promise.lift (box {| data = {| parentID = "parent-id" |} |}))
                        "children", box (fun _ -> Promise.lift (box {| data = childrenData |}))
                    ])
                ])
                "route", box (createObj [
                    "current", box {| name = "session"; ``params`` = {| sessionID = "test-sess" |} |}
                    "navigate", box (System.Action<string, obj>(fun _ opts -> navigationTarget <- dynStr opts "sessionID"))
                ])
                "command", box (createObj [
                    "register", box (fun (fn: obj) -> capturedCmdRegister <- Some fn)
                ])
                "ui", box (createObj [
                    "dialog", box (createObj [
                        "replace", box (fun (fn: obj) ->
                            let fnTyped = unbox<System.Func<obj>> fn
                            lastReplacedDialog <- fnTyped.Invoke()
                        )
                        "clear", box (fun () -> dialogCleared <- true)
                    ])
                    "toast", box (fun (opts: obj) -> toastMessage <- dynStr opts "message")
                    "DialogSelect", box (fun (props: obj) -> props)
                ])
                "lifecycle", box (createObj [
                    "onDispose", box (fun (cb: obj) -> disposeCb <- Some cb)
                ])
            ]
            let triggerDispose () =
                match disposeCb with
                | Some cb -> (unbox<System.Func<unit>> cb).Invoke()
                | None -> ()
            (apiObj, triggerDispose, originalTodo, sessionState)

        // Test 3: empty todo + no messages → fallback returns empty
        let (api3, _, _, sess3) = buildMockApi (box [||]) [||] [||] [||]
        let! _ = callRaw tuiFn api3 |> unbox<JS.Promise<unit>>
        let todo3 = dynGet sess3 "todo"
        let r3 = asObjArray (callRaw todo3 (box "test-sess"))
        chk "tui.fallback.emptyReturnsEmpty" (r3.Length = 0)

        // Test 4: non-empty todo → fallback returns original
        let originalTodoVal = box [| box {| content = "existing"; status = "pending" |} |]
        let (api4, _, _, sess4) = buildMockApi originalTodoVal [||] [||] [||]
        let! _ = callRaw tuiFn api4 |> unbox<JS.Promise<unit>>
        let todo4 = dynGet sess4 "todo"
        let r4 = asObjArray (callRaw todo4 (box "test-sess"))
        chk "tui.fallback.nonEmptyReturnsOriginal" (r4.Length = 1 && dynStr r4.[0] "content" = "existing")

        // Test 5: empty todo + successful task in messages → recovers
        let taskSuccessMsg = [|
            box (createObj [ "id", box "msg-1" ])
        |]
        let taskSuccessPart = [|
            box (createObj [
                   "type", box "tool"
                   "tool", box "task"
                   "state", box (createObj [
                       "status", box "success"
                       "input", box (createObj [
                           "todos", box [| box (createObj [ "content", box "recovered"; "status", box "pending" ]) |]
                       ])
                   ])
            ])
        |]
        let (api5, _, _, sess5) = buildMockApi (box [||]) taskSuccessMsg taskSuccessPart [||]
        let! _ = callRaw tuiFn api5 |> unbox<JS.Promise<unit>>
        let todo5 = dynGet sess5 "todo"
        let r5 = asObjArray (callRaw todo5 (box "test-sess"))
        chk "tui.fallback.recoversFromTask" (r5.Length = 1 && dynStr r5.[0] "content" = "recovered")

        // Test 6: empty todo + error task in messages → no recovery
        let taskErrorMsg = [|
            box (createObj [ "id", box "msg-2" ])
        |]
        let taskErrorPart = [|
            box (createObj [
                   "type", box "tool"
                   "tool", box "task"
                   "state", box (createObj [
                       "status", box "error"
                   ])
            ])
        |]
        let (api6, _, _, sess6) = buildMockApi (box [||]) taskErrorMsg taskErrorPart [||]
        let! _ = callRaw tuiFn api6 |> unbox<JS.Promise<unit>>
        let todo6 = dynGet sess6 "todo"
        let r6 = asObjArray (callRaw todo6 (box "test-sess"))
        chk "tui.fallback.errorTaskNoRecover" (r6.Length = 0)

        // Test 7: onDispose restores original todo
        let (api7, triggerDispose7, origTodo7, sess7) = buildMockApi (box [||]) [||] [||] [||]
        let! _ = callRaw tuiFn api7 |> unbox<JS.Promise<unit>>
        triggerDispose7 ()
        let todoAfterDispose = dynGet sess7 "todo"
        chk "tui.dispose.restoresOriginal" (obj.ReferenceEquals(todoAfterDispose, origTodo7))

        // Test 8: command.register and subagents dialog branch
        let children = [|
            box (createObj [
                "id", box "child-agent-1"
                "title", box "child-1"
                "time", box (createObj [
                    "created", box 1000.0
                    "updated", box 2000.0
                ])
            ])
            box (createObj [
                "id", box "checkpoint-writer-1"
                "title", box "checkpoint-writer:1"
                "time", box (createObj [
                    "created", box 1000.0
                    "updated", box 2000.0
                ])
            ])
        |]
        let (api8, _, _, _) = buildMockApi (box [||]) [||] [||] children
        let! _ = callRaw tuiFn api8 |> unbox<JS.Promise<unit>>
        chk "tui.commandRegister.captured" capturedCmdRegister.IsSome
        
        let cmdFn = unbox<System.Func<obj>> capturedCmdRegister.Value
        let cmds = unbox<obj[]> (cmdFn.Invoke())
        chk "tui.command.hasSubagents" (cmds.Length > 0 && dynStr cmds.[0] "value" = "vibe.subagents")
        
        let onSelect = dynGet cmds.[0] "onSelect"
        let onSelectFn = unbox<System.Func<unit>> onSelect
        onSelectFn.Invoke()
        
        // Wait for promise chain in openSubagents to settle
        do! Promise.sleep 100
        
        chk "tui.subagents.replacedDialog" (not (dynIsNull lastReplacedDialog))
        if not (dynIsNull lastReplacedDialog) then
            let options = unbox<obj[]> (dynGet lastReplacedDialog "options")
            // Check that checkpoint-writer is filtered out
            chk "tui.subagents.optionsLength" (options.Length = 1)
            if options.Length > 0 then
                let opt = options.[0]
                chk "tui.subagents.optionTitle" (dynStr opt "title" = "child-1")
                chk "tui.subagents.optionValue" (dynStr opt "value" = "child-agent-1")
                // Check relTime formatting
                let desc = dynStr opt "description"
                chk "tui.subagents.relTime" (desc.Contains "ago" || desc = "active now")
                
                // Select subagent
                let dialogOnSelect = dynGet lastReplacedDialog "onSelect"
                let dialogOnSelectFn = unbox<System.Func<obj, unit>> dialogOnSelect
                dialogOnSelectFn.Invoke(opt)
                
                chk "tui.subagents.navigateCalled" (navigationTarget = "child-agent-1")
                chk "tui.subagents.dialogCleared" dialogCleared

        printfn "\n✓ %d mimotui plugin e2e checks passed" ok
        return summary ()
    }
