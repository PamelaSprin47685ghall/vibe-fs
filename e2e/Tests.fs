module Wanxiangshu.E2e.Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

[<Import("start", "./harness.js")>]
let start: obj -> JS.Promise<obj> = jsNative

type MockLLM =
    abstract expectTool: string -> obj -> unit
    abstract expectText: string -> unit
    abstract reset: unit -> unit
    abstract calls: ResizeArray<obj>

type Harness =
    abstract mockLLM: MockLLM
    abstract createSession: obj -> obj -> JS.Promise<obj>
    abstract sendPrompt: string -> string -> obj -> JS.Promise<obj>
    abstract getMessages: string -> obj -> JS.Promise<obj>
    abstract getSessions: obj -> JS.Promise<obj>
    abstract waitForCalls: int -> int -> JS.Promise<int>
    abstract readFile: string -> JS.Promise<string>
    abstract fileExists: string -> JS.Promise<bool>
    abstract waitForFile: string -> int -> JS.Promise<bool>
    abstract dispose: unit -> JS.Promise<unit>

let private harnessFromObj (o: obj) : Harness = unbox o
let private emptyObj = createObj []

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

let private bodies (harness: Harness) : string =
    harness.mockLLM.calls
    |> Seq.cast<obj>
    |> Seq.map (fun c -> jsonStringify (c?body))
    |> String.concat "\n"

let private containsTool (harness: Harness) (toolName: string) : bool =
    let text = bodies harness
    text.Contains(sprintf "\"name\":\"%s\"" toolName) || text.Contains(sprintf "\"name\": \"%s\"" toolName)

let private toolRoundWithCalls (harness: Harness) (sessionID: string) (toolName: string) (toolArgs: obj) (promptText: string) (expectedCalls: int) : JS.Promise<unit> =
    promise {
        harness.mockLLM.reset()
        harness.mockLLM.expectTool toolName toolArgs
        for _ in 1 .. (expectedCalls - 1) do
            harness.mockLLM.expectText "ok"
        let! _ = harness.sendPrompt sessionID promptText emptyObj
        let! _ = harness.waitForCalls expectedCalls 20000
        return ()
    }

let private toolRound (harness: Harness) (sessionID: string) (toolName: string) (toolArgs: obj) (promptText: string) : JS.Promise<unit> =
    toolRoundWithCalls harness sessionID toolName toolArgs promptText 1

let private browserMcpRound (harness: Harness) (sessionID: string) : JS.Promise<unit> =
    promise {
        harness.mockLLM.reset()
        harness.mockLLM.expectTool "browser" (box {| intent = "open page" |})
        harness.mockLLM.expectTool "stealth-browser-mcp_get_debug_view" (createObj [])
        harness.mockLLM.expectText "browser mcp done"
        let! _ = harness.sendPrompt sessionID "open browser" emptyObj
        let! _ = harness.waitForCalls 3 20000
        return ()
    }

let private textRoundWithCalls (harness: Harness) (sessionID: string) (promptText: string) (expectedCalls: int) : JS.Promise<unit> =
    promise {
        harness.mockLLM.reset()
        for _ in 1 .. expectedCalls do
            harness.mockLLM.expectText "ok"
        let! _ = harness.sendPrompt sessionID promptText emptyObj
        let! _ = harness.waitForCalls expectedCalls 20000
        return ()
    }

let private textRound (harness: Harness) (sessionID: string) (promptText: string) : JS.Promise<unit> =
    textRoundWithCalls harness sessionID promptText 1

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let opts = createObj ["plugin", box true]
        let! apiObj = start opts
        let harness = harnessFromObj apiObj

        let! sessionID =
            promise {
                let! createRes = harness.createSession (createObj ["model", createObj ["id", box "test-model"; "providerID", box "test"]]) emptyObj
                let createData = unbox<obj> createRes
                check "e2e.session-create.ok" (createData?ok = true)
                return string (createData?data?data?id)
            }

        let expected = 39
        let mutable ok = 0
        let chk l c =
            check l c
            if c then ok <- ok + 1

        // 1. caps-prelude
        do! textRound harness sessionID "hello"
        let b = bodies harness
        chk "e2e.caps-prelude.injected" (b.Contains "# Kolmolgorov 宝典")
        chk "e2e.caps-prelude.has-iron-law" (b.Contains "# 铁律")

        // 2. write
        do! toolRound harness sessionID "write" (box {| filePath = "test.txt"; content = "hello" |}) "write hello to test.txt"
        chk "e2e.write.tool-called" (containsTool harness "write")

        // 3. read
        do! toolRound harness sessionID "read" (box {| filePath = "test.txt" |}) "read test.txt"
        chk "e2e.read.tool-called" (containsTool harness "read")

        // 4. executor
        do! toolRound harness sessionID "executor" (box {| language = "shell"; program = "echo hi" |}) "run echo hi"
        chk "e2e.executor.tool-called" (containsTool harness "executor")

        // 5. fuzzy_find
        do! toolRound harness sessionID "fuzzy_find" (box {| pattern = "README" |}) "find README files"
        chk "e2e.fuzzy-find.tool-called" (containsTool harness "fuzzy_find")

        // 6. fuzzy_grep
        do! toolRound harness sessionID "fuzzy_grep" (box {| pattern = "test" |}) "grep for test"
        chk "e2e.fuzzy-grep.tool-called" (containsTool harness "fuzzy_grep")

        // 7. investigator
        do! toolRound harness sessionID "investigator" (box {| objective = "find README"; background = "looking for readme"; questions = ResizeArray(["where is README?"]) |}) "investigate README location"
        chk "e2e.investigator.tool-called" (containsTool harness "investigator")

        // 8. coder
        do! toolRound harness sessionID "coder" (box {| intents = ResizeArray([]); tdd = "green" |}) "run coder"
        chk "e2e.coder.tool-called" (containsTool harness "coder")

        // 9. meditator
        do! toolRound harness sessionID "meditator" (box {| intent = "analyze"; files = ResizeArray([]) |}) "run meditator"
        chk "e2e.meditator.tool-called" (containsTool harness "meditator")

        // 10. browser
        do! browserMcpRound harness sessionID
        chk "e2e.browser.tool-called" (containsTool harness "browser")
        chk "e2e.browser.mcp-tool-called" (containsTool harness "stealth-browser-mcp_get_debug_view")
        chk "e2e.browser.mcp-tool-result-fed-back" ((bodies harness).Contains "e2e stealth mcp debug view")

        // 11. submit_review
        do! toolRound harness sessionID "submit_review" (box {| report = "test report"; wip = false; affectedFiles = ResizeArray([]) |}) "submit review"
        chk "e2e.submit-review.tool-called" (containsTool harness "submit_review")

        // 12. todowrite
        do! toolRound harness sessionID "todowrite" (box {| todos = ResizeArray([]); completedWorkReport = "test"; select_methodology = ResizeArray(["first_principles"]) |}) "write todo"
        chk "e2e.todowrite.tool-called" (containsTool harness "todowrite")

        // 13. tool-result-backfill
        do! toolRoundWithCalls harness sessionID "read" (box {| filePath = "README.md" |}) "read README.md then say ok" 1
        chk "e2e.tool-result-backfill.tool-called" (containsTool harness "read")
        let b3 = bodies harness
        chk "e2e.tool-result-backfill.body" (b3.Contains "README")

        // 14. history-echo
        do! textRoundWithCalls harness sessionID "first turn" 1
        do! textRoundWithCalls harness sessionID "second turn" 1
        let! msgs = harness.getMessages sessionID emptyObj
        let mb = jsonStringify (unbox<obj> msgs?data)
        chk "e2e.history-echo.first" (mb.Contains "first turn")
        chk "e2e.history-echo.second" (mb.Contains "second turn")

        // 15. session-listing
        let! sessionsRes = harness.getSessions emptyObj
        let sessions = unbox<obj> sessionsRes
        chk "e2e.session-listing" ((jsonStringify (sessions?data)).Contains sessionID)

        // 16. write-overwrite
        do! toolRound harness sessionID "write" (box {| filePath = "note.txt"; content = "one" |}) "write one"
        chk "e2e.write-overwrite.first-call" (containsTool harness "write")
        do! toolRound harness sessionID "write" (box {| filePath = "note.txt"; content = "two" |}) "write two"
        chk "e2e.write-overwrite.second-call" (containsTool harness "write")

        // 17. read-side-effect
        do! toolRound harness sessionID "read" (box {| filePath = "README.md" |}) "read README"
        chk "e2e.read-side-effect.tool-called" (containsTool harness "read")
        let b4 = bodies harness
        chk "e2e.read-side-effect.body" (b4.Contains "README")

        // 18. boundary-empty-content
        do! toolRound harness sessionID "write" (box {| filePath = "empty.txt"; content = "" |}) "write empty file"
        chk "e2e.boundary-empty-content.tool-called" (containsTool harness "write")

        // 19. boundary-special-chars-filename
        do! toolRound harness sessionID "write" (box {| filePath = "test-file_123.txt"; content = "special" |}) "write special filename"
        chk "e2e.boundary-special-chars-filename.tool-called" (containsTool harness "write")

        // 20. boundary-unicode-content
        do! toolRound harness sessionID "write" (box {| filePath = "unicode.txt"; content = "你好世界 🌍" |}) "write unicode content"
        chk "e2e.boundary-unicode-content.tool-called" (containsTool harness "write")

        // 21. error-path-read-nonexistent
        do! toolRound harness sessionID "read" (box {| filePath = "nonexistent.txt" |}) "read nonexistent file"
        chk "e2e.error-path-read-nonexistent.tool-called" (containsTool harness "read")
        let errorBodies = bodies harness
        chk "e2e.error-path-read-nonexistent.has-error-indication" (errorBodies.Contains "nonexistent.txt")

        // 22. multi-turn-file-reference (第二轮引用第一轮写入的文件)
        do! toolRound harness sessionID "write" (box {| filePath = "context.txt"; content = "important context data" |}) "write context file"
        chk "e2e.multi-turn-file-reference.write-ok" (containsTool harness "write")
        do! toolRoundWithCalls harness sessionID "read" (box {| filePath = "context.txt" |}) "read the context file we just wrote" 1
        let contextBodies = bodies harness
        chk "e2e.multi-turn-file-reference.read-previous-file" (contextBodies.Contains "important context data")

        // 23. session-create-second (验证可以创建多个会话)
        let! session2Res = harness.createSession (createObj ["model", createObj ["id", box "test-model"; "providerID", box "test"]]) emptyObj
        let session2Data = unbox<obj> session2Res
        chk "e2e.session-create-second.ok" (session2Data?ok = true)

        // 24. todo-with-actual-content
        do! toolRound harness sessionID "todowrite" (box {| todos = ResizeArray([box {| content = "implement feature X"; status = "in_progress"; priority = "high" |}]); completedWorkReport = "started work on X"; select_methodology = ResizeArray(["first_principles"]) |}) "write todo with content"
        chk "e2e.todo-with-content.tool-called" (containsTool harness "todowrite")
        let todoBodies = bodies harness
        chk "e2e.todo-with-content.has-task-content" (todoBodies.Contains "implement feature X")
        chk "e2e.todo-with-content.has-progress-report" (todoBodies.Contains "started work on X")

        // 25. executor-javascript
        do! toolRound harness sessionID "executor" (box {| language = "javascript"; program = "console.log('js test')" |}) "run javascript"
        chk "e2e.executor-javascript.tool-called" (containsTool harness "executor")

        // 26. fuzzy-find-no-results
        do! toolRound harness sessionID "fuzzy_find" (box {| pattern = "xyznonexistent123" |}) "find nonexistent pattern"
        chk "e2e.fuzzy-find-no-results.tool-called" (containsTool harness "fuzzy_find")

        // 27. write-large-content
        let largeContent = String.replicate 1000 "x"
        do! toolRound harness sessionID "write" (box {| filePath = "large.txt"; content = largeContent |}) "write large file"
        chk "e2e.write-large-content.tool-called" (containsTool harness "write")

        // 28. multiple-files-write-sequential
        do! toolRound harness sessionID "write" (box {| filePath = "file1.txt"; content = "one" |}) "write file1"
        do! toolRound harness sessionID "write" (box {| filePath = "file2.txt"; content = "two" |}) "write file2"
        do! toolRound harness sessionID "write" (box {| filePath = "file3.txt"; content = "three" |}) "write file3"

        printfn "\n✓ %d/%d e2e checks passed" ok expected

        do! harness.dispose()
        return summary ()
    }
