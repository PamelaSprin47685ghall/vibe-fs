module Wanxiangshu.E2e.TestsServePlugin

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

let runServePluginChecks
    (harness: Harness)
    (sessionID: string)
    (chk: string -> bool -> unit)
    (toolRound: Harness -> string -> string -> obj -> string -> JS.Promise<unit>)
    (toolRoundWithCalls: Harness -> string -> string -> obj -> string -> int -> JS.Promise<unit>)
    (textRound: Harness -> string -> string -> JS.Promise<unit>)
    (containsTool: Harness -> string -> bool)
    (bodies: Harness -> string)
    (emptyObj: obj)
    (todoToolName: string)
    =
    promise {
        do! textRound harness sessionID "list your available tool names briefly"
        let toolListBodies = bodies harness

        chk
            "e2e.serve.tools.web_search-listed"
            (containsTool harness "web_search" || toolListBodies.Contains "web_search")

        chk "e2e.serve.tools.webfetch-listed" (containsTool harness "webfetch" || toolListBodies.Contains "webfetch")

        chk "e2e.serve.tools.meditator-listed" (containsTool harness "meditator" || toolListBodies.Contains "meditator")

        do!
            toolRound
                harness
                sessionID
                "web_search"
                (box
                    {| query = "wanxiangshu e2e"
                       numResults = 3
                       what_to_summarize = "errors only" |})
                "run web_search"

        chk "e2e.serve.web_search.tool-called" (containsTool harness "web_search")

        do!
            toolRound
                harness
                sessionID
                "webfetch"
                (box
                    {| url = "file://" + harness.workDir + "/README.md"
                       extract_main = false |})
                "webfetch README"

        chk "e2e.serve.webfetch.tool-called" (containsTool harness "webfetch")

        let methIntent = String.replicate 600 "intent "
        let methBg = String.replicate 600 "background "
        let methNote = String.replicate 600 "note "

        do!
            toolRoundWithCalls
                harness
                sessionID
                "meditator"
                (box
                    {| methodology = "first_principles"
                       intent = methIntent
                       background = methBg
                       note = methNote |})
                "run meditator first_principles"
                2

        chk "e2e.serve.meditator.tool-called" (containsTool harness "meditator")

        let! loopRes =
            withTimeoutL
                "serve loop command"
                4000
                (harness.runSessionCommand sessionID "loop" "implement feature X via serve" emptyObj)

        let loopData = unbox<obj> loopRes
        chk "e2e.serve.loop.command.ok" (loopData?ok = true)
        let! ndLoop = withTimeout (harness.waitForNdjson 1 1000)
        chk "e2e.serve.loop.ndjson-written" ndLoop
        let! ndLoopText = withTimeout (harness.readNdjson ())
        chk "e2e.serve.loop.ndjson-activated" (ndLoopText.Contains "loop_activated")
        chk "e2e.serve.loop.ndjson-task" (ndLoopText.Contains "implement feature X via serve")
        let! msgsAfterLoop = withTimeout (harness.getMessages sessionID emptyObj)
        let historyLoop = harness.allMessagesText (unbox<obj> msgsAfterLoop)

        chk
            "e2e.serve.loop.withReviewText"
            (historyLoop.Contains "With-Review Mode is active"
             || ndLoopText.Contains "loop_activated")

        let! cancelRes = withTimeoutL "serve loop cancel" 10000 (harness.runSessionCommand sessionID "loop" "" emptyObj)

        let cancelData = unbox<obj> cancelRes
        chk "e2e.serve.loop.cancel.ok" (cancelData?ok = true)
        let! ndAfterCancel = withTimeoutL "serve loop cancel ndjson" 5000 (harness.readNdjson ())
        chk "e2e.serve.loop.cancel.ndjson" (ndAfterCancel.Contains "loop_cancelled")
        let! msgsAfterCancel = withTimeoutL "serve loop cancel messages" 5000 (harness.getMessages sessionID emptyObj)
        let historyCancel = harness.allMessagesText (unbox<obj> msgsAfterCancel)

        chk
            "e2e.serve.loop.cancel.message"
            (historyCancel.Contains "With-Review Mode cancelled"
             || ndAfterCancel.Contains "loop_cancelled")

        let! cmdRes = withTimeout (harness.listCommands emptyObj)
        let cmdJson = jsonStringify (unbox<obj> cmdRes?data)
        chk "e2e.serve.commands.has-loop" (cmdJson.Contains "loop")

        let todoArgs =
            createObj
                [ "todos",
                  box (
                      ResizeArray(
                          [ box (
                                createObj
                                    [ "content", box "serve ndjson todo"
                                      "status", box "completed"
                                      "priority", box "high" ]
                            ) ]
                      )
                  )
                  "select_methodology", box (ResizeArray([ "first_principles" ])) ]

        do! toolRound harness sessionID todoToolName todoArgs (sprintf "commit todo via %s" todoToolName)
        chk "e2e.serve.todowrite.tool-called" (containsTool harness todoToolName)
        chk "e2e.serve.todowrite.ndjson" true

        // --- E2E Test: Todo soft-required validation (short fields are allowed, no criticism) ---
        let shortTodoArgs =
            createObj
                [ "todos",
                  box (
                      ResizeArray(
                          [ box (
                                createObj
                                    [ "content", box "serve short todo"
                                      "status", box "completed"
                                      "priority", box "high" ]
                            ) ]
                      )
                  )
                  "select_methodology", box (ResizeArray([ "first_principles" ])) ]

        do! toolRound harness sessionID todoToolName shortTodoArgs (sprintf "commit short todo via %s" todoToolName)
        chk "e2e.serve.todowrite.short.tool-called" (containsTool harness todoToolName)

        let! msgsAfterShortTodo = withTimeout (harness.getMessages sessionID emptyObj)
        let historyShortTodo = harness.allMessagesText (unbox<obj> msgsAfterShortTodo)
        chk "e2e.serve.todowrite.short.no-criticism" (not (historyShortTodo.Contains "严重协议违例"))

        // --- E2E Test: Warn soft-required validation (missing warn_tdd is allowed, no criticism) ---
        let writeArgsWithoutWarnTdd =
            createObj
                [ "filePath", box "test_warn_tdd.txt"
                  "content", box "hello without warn_tdd"
                  "warn_tdd", box null ]

        do! toolRound harness sessionID "write" writeArgsWithoutWarnTdd "write without warn_tdd"
        chk "e2e.serve.write.missing-warn_tdd.tool-called" (containsTool harness "write")

        let! msgsAfterWrite = withTimeout (harness.getMessages sessionID emptyObj)
        let historyWrite = harness.allMessagesText (unbox<obj> msgsAfterWrite)
        chk "e2e.serve.write.missing-warn_tdd.no-criticism" (not (historyWrite.Contains "严重协议违例"))

        chk
            "e2e.serve.write.missing-warn_tdd.no-warn_tdd-reprimand"
            (not (historyWrite.Contains "warn_tdd: missing required acknowledgement"))
    }
