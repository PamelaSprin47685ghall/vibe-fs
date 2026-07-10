module Wanxiangshu.E2e.Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes

[<Import("start", "./harness.js")>]
let start: obj -> JS.Promise<obj> = jsNative

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

    text.Contains(sprintf "\"name\":\"%s\"" toolName)
    || text.Contains(sprintf "\"name\": \"%s\"" toolName)

let private toolRoundWithCalls
    (harness: Harness)
    (sessionID: string)
    (toolName: string)
    (toolArgs: obj)
    (promptText: string)
    (expectedCalls: int)
    : JS.Promise<unit> =
    promise {
        harness.mockLLM.reset ()
        harness.mockLLM.expectTool toolName toolArgs

        for _ in 1 .. (expectedCalls - 1) do
            harness.mockLLM.expectText "ok"

        let! _ = harness.sendPrompt sessionID promptText emptyObj
        let! _ = harness.waitForCalls expectedCalls 60000
        return ()
    }

let private toolRound
    (harness: Harness)
    (sessionID: string)
    (toolName: string)
    (toolArgs: obj)
    (promptText: string)
    : JS.Promise<unit> =
    toolRoundWithCalls harness sessionID toolName toolArgs promptText 1

let private browserMcpRound (harness: Harness) (sessionID: string) : JS.Promise<unit> =
    promise {
        harness.mockLLM.reset ()
        harness.mockLLM.expectTool "browser" (box {| intent = "open page" |})
        harness.mockLLM.expectTool "stealth-browser-mcp_get_debug_view" (createObj [])
        harness.mockLLM.expectText "browser mcp done"
        let! _ = harness.sendPrompt sessionID "open browser" emptyObj
        let! _ = harness.waitForCalls 3 60000
        return ()
    }

let private textRoundWithCalls
    (harness: Harness)
    (sessionID: string)
    (promptText: string)
    (expectedCalls: int)
    : JS.Promise<unit> =
    promise {
        harness.mockLLM.reset ()

        for _ in 1..expectedCalls do
            harness.mockLLM.expectText "ok"

        let! _ = harness.sendPrompt sessionID promptText emptyObj
        let! _ = harness.waitForCalls expectedCalls 60000
        return ()
    }

let private textRound (harness: Harness) (sessionID: string) (promptText: string) : JS.Promise<unit> =
    textRoundWithCalls harness sessionID promptText 1

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let opts = createObj [ "plugin", box true ]
        let! apiObj = start opts
        let harness = harnessFromObj apiObj

        let expected = 53
        let mutable ok = 0

        let chk l c =
            check l c

            if c then
                ok <- ok + 1

        let! outcome =
            promise {
                let! sessionID =
                    promise {
                        let! createRes =
                            harness.createSession
                                (createObj [ "model", createObj [ "id", box "test-model"; "providerID", box "test" ] ])
                                emptyObj

                        let createData = unbox<obj> createRes
                        check "e2e.session-create.ok" (createData?ok = true)
                        return string (createData?data?data?id)
                    }

                // 1. caps-prelude
                do! textRound harness sessionID "hello"
                let b = bodies harness
                chk "e2e.caps-prelude.injected" (b.Contains "# Kolmolgorov 宝典")
                chk "e2e.caps-prelude.has-iron-law" (b.Contains "# 铁律")

                // 2. write
                do!
                    toolRound
                        harness
                        sessionID
                        "write"
                        (box
                            {| filePath = "test.txt"
                               content = "hello" |})
                        "write hello to test.txt"

                chk "e2e.write.tool-called" (containsTool harness "write")

                // 3. read
                do! toolRound harness sessionID "read" (box {| filePath = "test.txt" |}) "read test.txt"
                chk "e2e.read.tool-called" (containsTool harness "read")

                // 4. executor
                do!
                    toolRound
                        harness
                        sessionID
                        "executor"
                        (box
                            {| language = "shell"
                               program = "echo hi" |})
                        "run echo hi"

                chk "e2e.executor.tool-called" (containsTool harness "executor")

                // 5. fuzzy_find
                do! toolRound harness sessionID "fuzzy_find" (box {| pattern = [| "README" |] |}) "find README files"
                chk "e2e.fuzzy-find.tool-called" (containsTool harness "fuzzy_find")

                // 6. fuzzy_grep
                do! toolRound harness sessionID "fuzzy_grep" (box {| pattern = [| "test" |] |}) "grep for test"
                chk "e2e.fuzzy-grep.tool-called" (containsTool harness "fuzzy_grep")

                // 7. investigator
                do!
                    toolRound
                        harness
                        sessionID
                        "investigator"
                        (box
                            {| objective = "find README"
                               background = "looking for readme"
                               questions = ResizeArray([ "where is README?" ]) |})
                        "investigate README location"

                chk "e2e.investigator.tool-called" (containsTool harness "investigator")

                // 8. coder
                do!
                    toolRound
                        harness
                        sessionID
                        "coder"
                        (box
                            {| intents = ResizeArray([])
                               tdd = "green" |})
                        "run coder"

                chk "e2e.coder.tool-called" (containsTool harness "coder")

                // 9. meditator
                do!
                    toolRound
                        harness
                        sessionID
                        "meditator"
                        (box
                            {| methodology = "first_principles"
                               intent = "reasoning"
                               background = "context"
                               note = "analysis" |})
                        "run meditator"

                chk "e2e.meditator.tool-called" (containsTool harness "meditator")
                let bMeditator = bodies harness
                chk "e2e.meditator.prompt-contains-background" (bMeditator.Contains "context")

                // 10. browser
                do! browserMcpRound harness sessionID
                chk "e2e.browser.tool-called" (containsTool harness "browser")
                chk "e2e.browser.mcp-tool-called" (containsTool harness "stealth-browser-mcp_get_debug_view")
                chk "e2e.browser.mcp-tool-result-fed-back" ((bodies harness).Contains "e2e stealth mcp debug view")

                // 11. submit_review
                do!
                    toolRound
                        harness
                        sessionID
                        "submit_review"
                        (box
                            {| report = "test report"
                               wip = false
                               affectedFiles = ResizeArray([]) |})
                        "submit review"

                chk "e2e.submit-review.tool-called" (containsTool harness "submit_review")

                // 12. todowrite
                do!
                    toolRound
                        harness
                        sessionID
                        "todowrite"
                        (box
                            {| todos = ResizeArray([])
                               completedWorkReport = "test"
                               select_methodology = ResizeArray([ "first_principles" ]) |})
                        "write todo"

                chk "e2e.todowrite.tool-called" (containsTool harness "todowrite")

                // 13. tool-result-backfill
                do!
                    toolRoundWithCalls
                        harness
                        sessionID
                        "read"
                        (box {| filePath = "README.md" |})
                        "read README.md then say ok"
                        1

                chk "e2e.tool-result-backfill.tool-called" (containsTool harness "read")
                let b3 = bodies harness
                chk "e2e.tool-result-backfill.body" (b3.Contains "README")

                return!
                    Wanxiangshu.E2e.TestsPart2.runRest
                        harness
                        sessionID
                        chk
                        toolRound
                        toolRoundWithCalls
                        textRound
                        containsTool
                        bodies
                        emptyObj
                        jsonStringify
                        textRoundWithCalls
                        expected
                        summary
            }
            |> Promise.map Ok
            |> Promise.catch (fun ex -> Error ex)

        do! harness.dispose ()

        match outcome with
        | Ok result -> return result
        | Error ex -> return raise ex
    }
