module Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Tests.Assert

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
    abstract runToolExecuteHooks: string -> obj -> string -> JS.Promise<obj>
    abstract runCommandExecuteBefore: string -> string -> JS.Promise<obj>
    abstract runMessageTransform: obj -> obj -> JS.Promise<obj>
    abstract runSystemTransform: obj -> JS.Promise<obj>
    abstract runConfigHook: obj -> JS.Promise<obj>
    abstract runLifecycleHook: string -> obj -> obj -> JS.Promise<obj>
    abstract fireEvent: obj -> JS.Promise<obj>
    abstract fireStreamAbort: string -> JS.Promise<obj>
    abstract getReviewStore: unit -> obj
    abstract getFallbackRuntime: unit -> obj
    abstract readPartsText: obj -> string
    abstract readFile: string -> string
    abstract fileExists: string -> bool
    abstract dispose: unit -> JS.Promise<unit>

let runToolLifecycle
    (harness: Harness)
    (chk: string -> bool -> unit)
    (warnTddValue: string)
    (warnValue: string)
    (execArgs: obj)
    (createEmpty: unit -> obj)
    (dynGet: obj -> string -> obj)
    (dynIsNull: obj -> bool)
    (dynStr: obj -> string -> string)
    : JS.Promise<unit> =
    promise {
        // --- 4. System Transform & Tools --------------------------------------
        let! systemOutput = harness.runSystemTransform (createEmpty ())
        chk "op.sysTrans.hasWorkDir" ((string (dynGet systemOutput "system")).IndexOf(harness.workDir) >= 0)
        chk "op.meth.ok" (not (dynIsNull (harness.getToolEntry "meditator")))

        let! wsResult =
            harness.executePluginTool
                "web_search"
                (createObj
                    [ "query", box "test query"
                      "numResults", box 5
                      "what_to_summarize", box "keep all" ])
                (createEmpty ())

        chk
            "op.websearch.missingKey"
            ((string wsResult).IndexOf "failed" >= 0
             || (string wsResult).IndexOf "Missing" >= 0
             || (string wsResult).IndexOf "(no output)" >= 0)

        let! rrResult =
            harness.executePluginTool
                "return_reviewer"
                (createObj [ "verdict", box "PERFECT"; "feedback", box "" ])
                (createEmpty ())

        chk
            "op.returnReviewer.ok"
            (not (isNull rrResult)
             && ((string rrResult).IndexOf "No active review" >= 0
                 || (string rrResult).IndexOf "double-check" >= 0))

        // --- 5. Lifecycle hooks: config -----------------------------------------
        let configArgs =
            createObj [ "agent", box (createObj [ "build", box (createObj [ "model", box "test" ]) ]) ]

        let! configRes = harness.runConfigHook configArgs
        chk "op.configHook.run" (not (dynIsNull configRes) && not (dynIsNull (dynGet configRes "command")))

        // --- 6. Lifecycle hooks: chat.message ----------------------------------
        let chatOutput =
            createObj
                [ "message",
                  box (createObj [ "tools", box [| box {| name = "executor" |}; box {| name = "pty_spawn" |} |] ]) ]

        let! chatRes =
            harness.runLifecycleHook "chat.message" (createObj [ "sessionID", box harness.sessionId ]) chatOutput

        chk "op.chatMessage.processed" (not (dynIsNull (dynGet chatRes "message")))

        // --- 7. tool.execute.before check ------------------------------------
        let! coderBeforeRes =
            harness.runToolExecuteHooks "coder" (createObj [ "intents", box [||]; "tdd", box "green" ]) "success"

        chk "op.coder.before.missingWarnTdd" (dynIsNull (dynGet coderBeforeRes "error"))
        let! execBeforeRes = harness.runToolExecuteHooks "executor" (createObj [ "command", box "echo" ]) "success"
        chk "op.executor.before.missingWarn" (dynIsNull (dynGet execBeforeRes "error"))

        // --- 8. tool.execute.after boundaries --------------------------------
        let! netRes = harness.runToolExecuteHooks "executor" execArgs "network error"
        chk "op.executor.networkErrorConverted" ((string (dynGet netRes "error")) = "network connection lost")

        let execArgs2 =
            createObj
                [ "command", box "echo hello-executor"
                  "language", box "shell"
                  "mode", box "ro"
                  "timeout_type", box "short"
                  "what_to_summarize", box "keep stdout only"
                  "warn_tdd", box warnTddValue
                  "warn", box warnValue ]

        let! liveRes1 = harness.runToolExecuteHooks "executor" execArgs2 "hello-livelock"
        let! liveRes2 = harness.runToolExecuteHooks "executor" execArgs2 "hello-livelock"
        let! liveRes3 = harness.runToolExecuteHooks "executor" execArgs2 "hello-livelock"
        chk "op.executor.livelockIntercepted" ((string (dynGet liveRes3 "error")).IndexOf("livelock guard") >= 0)

        // --- 9. todowrite intercept flow --------------------------------------
        let twArgs =
            createObj
                [ "todos",
                  box
                      [| box
                             {| content = "do task"
                                status = "pending"
                                priority = "high" |} |]
                  "select_methodology", box [| "first_principles" |] ]

        let! twRes = harness.runToolExecuteHooks "todowrite" twArgs "success"
        chk "op.todowrite.rewritten" ((dynStr twRes "output").IndexOf("first_principles") >= 0)
        chk "op.todowrite.noErr" (dynIsNull (dynGet twRes "error"))

        chk
            "op.todowrite.eventAppended"
            (((if harness.fileExists ".wanxiangshu.ndjson" then
                   harness.readFile ".wanxiangshu.ndjson"
               else
                   "")
                .IndexOf("work_backlog_committed")
              >= 0))

        // Bad todo (empty content) should still block the event.
        let chkTwErr label (errSub: string) extra =
            promise {
                let beforeLog =
                    if harness.fileExists ".wanxiangshu.ndjson" then
                        harness.readFile ".wanxiangshu.ndjson"
                    else
                        ""

                let baseArgs =
                    [ "todos",
                      box
                          [| box
                                 {| content = "do task"
                                    status = "pending"
                                    priority = "high" |} |]
                      "select_methodology", box [| "first_principles" |] ]

                let merged = createObj (baseArgs @ extra)
                let! _ = harness.runToolExecuteHooks "todowrite" merged "success"

                let afterLog =
                    if harness.fileExists ".wanxiangshu.ndjson" then
                        harness.readFile ".wanxiangshu.ndjson"
                    else
                        ""

                chk label (beforeLog = afterLog)
            }

        do!
            chkTwErr
                "op.todowrite.badTodoErr"
                "content"
                [ "todos",
                  box
                      [| box
                             {| content = ""
                                status = "pending"
                                priority = "high" |} |] ]
    }
