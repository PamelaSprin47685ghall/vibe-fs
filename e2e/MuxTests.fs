module Wanxiangshu.E2e.MuxTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.MuxEventHooksAndSlashTests

[<Import("start", "./mux-runner.js")>]
let private startMux: obj -> JS.Promise<obj> = jsNative

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private fileExists (path: string) : bool = jsNative

open Wanxiangshu.E2e.MuxEventHooksAndSlashTests

let private harnessFromObj (o: obj) : Harness = unbox o
let private createEmpty () = createObj []

let private dynGet (o: obj) (k: string) = get o k
let private dynIsNull (o: obj) = isNullish o
let private dynIsArr (o: obj) = isArray o
let private dynTypeIs (o: obj) (t: string) = typeIs o t
let private dynStr (o: obj) (k: string) = str o k

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

let private nudgeCount (harness: Harness) : int =
    let nudges = dynGet harness.helpers "nudges"

    if dynIsNull nudges then
        0
    else
        let arr: obj[] = unbox<obj[]> nudges
        arr.Length

let private setTodos (harness: Harness) (todos: obj[]) : unit =
    let setter = dynGet harness.helpers "_setTodoList"

    if not (dynIsNull setter) then
        setter $ (box todos) |> ignore

let private toolSchemaProperties (harness: Harness) (name: string) : obj =
    let schema = harness.getToolSchema name

    if dynIsNull schema then
        null
    else
        dynGet schema "properties"

let private toolSchemaRequiredArray (harness: Harness) (name: string) : string array =
    let schema = harness.getToolSchema name

    if dynIsNull schema then
        [||]
    else
        let req = dynGet schema "required"

        if dynIsNull req || not (dynIsArr req) then
            [||]
        else
            unbox<string[]> req

let private warnTddValue =
    "i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated"

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()

        let histTextPart: obj =
            createObj [ "type", box "text"; "text", box "I have completed the previous task." ]

        let histParts: obj[] = [| histTextPart |]

        let histMsg: obj =
            createObj [ "id", box "hist-1"; "role", box "assistant"; "parts", box histParts ]

        let chatHistory: obj[] = [| histMsg |]

        let startOpts: obj =
            createObj [ "workspaceId", box "mux-e2e-session"; "chatHistory", box chatHistory ]

        let! apiObj = withTimeoutCustom 30000 (startMux startOpts)
        let harness = harnessFromObj apiObj
        let reg = harness.registration

        let mutable ok = 0

        let chk label cond =
            check label cond

            if cond then
                ok <- ok + 1

        let runTool name args pred label =
            promise {
                let! res = withTimeout (harness.executeTool name args (createEmpty ()))
                let cond = pred res

                if not cond then
                    printfn "DEBUG FAIL: %s -> %s" label res

                chk label cond
            }

        // --- 1. Plugin registration structure --------------------------------
        let tools = dynGet reg "tools"
        chk "mux.reg.tools.isArray" (not (dynIsNull tools) && dynIsArr tools)
        chk "mux.reg.tools.nonEmpty" (not (dynIsNull tools) && (unbox<obj[]> tools).Length > 0)

        let eventHook = dynGet reg "eventHook"
        chk "mux.reg.eventHook.isFunction" (not (dynIsNull eventHook) && dynTypeIs eventHook "function")

        let messagesTransform = dynGet reg "messagesTransform"

        chk
            "mux.reg.messagesTransform.isFunction"
            (not (dynIsNull messagesTransform) && dynTypeIs messagesTransform "function")

        let slashCommands = dynGet reg "slashCommands"
        chk "mux.reg.slashCommands.isArray" (not (dynIsNull slashCommands) && dynIsArr slashCommands)

        chk
            "mux.reg.slashCommands.hasLoop"
            (let cmds: obj[] = unbox<obj[]> slashCommands in cmds |> Array.exists (fun c -> dynStr c "key" = "loop"))



        // --- 2. Tool schemas -------------------------------------------------
        let propsWrite = toolSchemaProperties harness "write"
        chk "mux.schema.write.hasFilePath" (not (dynIsNull (dynGet propsWrite "file_path")))
        chk "mux.schema.write.hasContent" (not (dynIsNull (dynGet propsWrite "content")))
        chk "mux.schema.write.requiredFilePath" (toolSchemaRequiredArray harness "write" |> Array.contains "file_path")
        chk "mux.schema.write.requiredContent" (toolSchemaRequiredArray harness "write" |> Array.contains "content")

        let propsRead = toolSchemaProperties harness "read"
        chk "mux.schema.read.hasPath" (not (dynIsNull (dynGet propsRead "path")))
        chk "mux.schema.read.requiredPath" (toolSchemaRequiredArray harness "read" |> Array.contains "path")

        let propsExec = toolSchemaProperties harness "executor"
        chk "mux.schema.executor.hasCommand" (not (dynIsNull (dynGet propsExec "command")))
        chk "mux.schema.executor.hasLanguage" (not (dynIsNull (dynGet propsExec "language")))

        chk
            "mux.schema.executor.requiredCommand"
            (toolSchemaRequiredArray harness "executor" |> Array.contains "command")

        let propsFuzzy = toolSchemaProperties harness "fuzzy_find"
        chk "mux.schema.fuzzyFind.hasPattern" (not (dynIsNull (dynGet propsFuzzy "pattern")))

        // --- 3. Tool execution -----------------------------------------------
        do!
            runTool
                "write"
                (createObj
                    [ "file_path", box "mux-e2e-test.md"
                      "content", box "hello from mux e2e"
                      "warn_tdd", box warnTddValue ])
                (fun r -> fileExists (harness.workDir + "/mux-e2e-test.md") && r.Contains "Successfully")
                "mux.execute.write.success"

        let writeOk = fileExists (harness.workDir + "/mux-e2e-test.md")

        chk
            "mux.execute.write.contentCorrect"
            (writeOk
             && (readFileSync (harness.workDir + "/mux-e2e-test.md") "utf8").Contains "hello from mux e2e")

        do!
            runTool
                "read"
                (createObj [ "path", box "mux-e2e-test.md" ])
                (fun r -> r.Contains "hello from mux e2e")
                "mux.execute.read.success"

        do!
            runTool
                "executor"
                (createObj
                    [ "command", box "echo hello-executor"
                      "language", box "shell"
                      "mode", box "ro"
                      "max_bytes", box 8192
                      "timeout_type", box "short"
                      "what_to_summarize", box "keep stdout only"
                      "warn_tdd", box warnTddValue
                      "warn",
                      box
                          "it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it" ])
                (fun r -> r.Contains "hello-executor")
                "mux.execute.executor.success"

        do!
            runTool
                "fuzzy_find"
                (createObj [ "pattern", box [| "mux-e2e" |] ])
                (fun r -> r.Contains "mux-e2e-test.md")
                "mux.execute.fuzzyFind.success"

        do!
            runTool
                "fuzzy_grep"
                (createObj [ "pattern", box [| "hello" |] ])
                (fun r -> r.Contains "mux-e2e-test.md")
                "mux.execute.fuzzyGrep.success"

        do!
            MuxEventHooksAndSlashTests.runRest
                harness
                chk
                runTool
                warnTddValue
                fileExists
                readFileSync
                dynGet
                dynIsNull
                dynIsArr
                dynTypeIs
                dynStr
                nudgeCount
                setTodos
                createEmpty

        printfn "\n✓ %d/53 mux e2e checks passed" ok
        return summary ()
    }
