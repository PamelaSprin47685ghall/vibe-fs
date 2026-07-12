module Wanxiangshu.E2e.TestsOmpSpecsLifecycle

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.E2e.HarnessTypes

[<Emit("JSON.stringify($0)")>]
let jsonStringify (o: obj) : string = jsNative

let runOmpTodowrite (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let todowriteArgs =
            box
                {| todos =
                    ResizeArray(
                        [ box
                              {| content = "verify omp e2e"
                                 status = "in_progress"
                                 priority = "high" |} ]
                    )
                   ahaMoments = String.replicate 1100 "a"
                   changesAndReasons = String.replicate 1100 "c"
                   gotchas = String.replicate 1100 "g"
                   lessonsAndConventions = String.replicate 1100 "l"
                   plan = String.replicate 1100 "p"
                   select_methodology = ResizeArray([ box "first_principles" ]) |}

        let! _ = withTimeout (h.triggerTool "todowrite" todowriteArgs sessionId (createObj []))
        chk "e2e-omp.todowrite.ran" true
        let! ndWritten = withTimeout (h.waitForNdjson 1 1000)
        chk "e2e-omp.todowrite.ndjson-written" ndWritten
        let! ndLines = withTimeout (h.readNdjson ())
        chk "e2e-omp.todowrite.ndjson-has-work-backlog" (ndLines.Contains "work_backlog_committed")
        chk "e2e-omp.todowrite.ndjson-has-session" (ndLines.Contains sessionId)
    }

let runOmpLoopCommand (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let! _ = withTimeout (h.runCommand "loop" "implement task X" sessionId)

        for c in 1..2 do
            let! _ = withTimeout (h.waitForNdjson c 1000)
            ()

        let! ndLines = withTimeout (h.readNdjson ())
        chk "e2e-omp.cmd.loop.success" (ndLines.Contains "loop_activated" && ndLines.Contains "implement task X")
    }

let runOmpReview (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let! submitWipRes =
            withTimeout (
                h.triggerTool
                    "submit_review"
                    (createObj
                        [ "report", box "wip progress report"
                          "affectedFiles", box [||]
                          "wip", box true ])
                    sessionId
                    (createObj [])
            )

        let wipStr = jsonStringify submitWipRes

        chk
            "e2e-omp.submit_review.wip_true.success"
            (wipStr.Contains "Your progress report was recorded"
             && not (wipStr.Contains "error"))

        do! h.expectTool "return_reviewer" (box {| verdict = "PERFECT"; feedback = "" |})

        do!
            h.expectTool
                "return_reviewer"
                (box
                    {| verdict = "REVISE"
                       feedback = "precheck requires details" |})

        let! submitFinalRes =
            withTimeout (
                h.triggerTool
                    "submit_review"
                    (createObj
                        [ "report", box "final review submission"
                          "affectedFiles", box [||]
                          "wip", box false ])
                    sessionId
                    (createObj [])
            )

        let finalStr = jsonStringify submitFinalRes

        chk
            "e2e-omp.submit_review.wip_false.success"
            (finalStr.Contains "Review passed. Loop mode ended."
             && not (finalStr.Contains "error"))

        do! yieldMicrotask ()
    }

let runOmpLoopReview (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let! _ = withTimeout (h.runCommand "loop-review" "implement task Y" sessionId)

        for c in 1..5 do
            let! _ = withTimeout (h.waitForNdjson c 1000)
            ()

        let! ndLines = withTimeout (h.readNdjson ())
        chk "e2e-omp.cmd.loop-review.success" (ndLines.Contains "implement task Y")
    }

let runOmpShutdown (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let remaining = h.getRemainingExpectations ()
        chk "e2e-omp.mock-llm.expectations-empty" (remaining = 0)
        let! _ = withTimeout (h.emitEvent "session_shutdown" (createObj []) sessionId)
        do! withTimeout (h.dispose ())
    }
