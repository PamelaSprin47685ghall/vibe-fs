module Wanxiangshu.Tests.OmpExecutorToolsTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

let private reset () = resetRunnerJobsForTesting ()

let private jsUndefined : obj = emitJsExpr () "undefined"

let registersExecutorTools () =
    reset ()
    let h = createPiHarness ()
    let pi = piObject h
    registerExecutorTools pi
    let names = h.tools |> Seq.map (fun t -> Dyn.str t "name") |> Set.ofSeq
    check "executor tool registered" (names.Contains "executor")
    check "executor_wait tool registered" (names.Contains "executor_wait")
    check "executor_abort tool registered" (names.Contains "executor_abort")

let executorWaitNoSessionReturnsError () = promise {
    reset ()
    let h = createPiHarness ()
    let pi = piObject h
    registerExecutorTools pi
    let wait =
        h.tools |> Seq.find (fun t -> Dyn.str t "name" = "executor_wait")
    let execute = Dyn.get wait "execute"
    let ctx = createObj [ "cwd", box "/tmp" ]
    let! result =
        emitJsExpr (execute, "wait-call", createObj [ "ms", box 100 ], jsUndefined, jsUndefined, ctx)
            "$0($1)($2)($3)($4)($5)"
        |> unbox<JS.Promise<obj>>
    let content = unbox<obj array> (Dyn.get result "content")
    let text = Dyn.str content.[0] "text"
    check "executor_wait error when no session" (text.Contains "No executor session found")
}

let executorWaitWithSessionReturnsLogSnippet () = promise {
    reset ()
    Wanxiangshu.Omp.ExecutorTools.executorMinWaitMs <- 0
    try
        let sessionId = "executor-wait-session-1"
        appendRunnerLog sessionId "log-line-a\nlog-line-b"
        let h = createPiHarness ()
        let pi = piObject h
        registerExecutorTools pi
        let wait =
            h.tools |> Seq.find (fun t -> Dyn.str t "name" = "executor_wait")
        let execute = Dyn.get wait "execute"
        let ctx = createObj [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box sessionId) ]) ]
        let! result =
            emitJsExpr (execute, "wait-call", createObj [ "ms", box 0 ], jsUndefined, jsUndefined, ctx)
                "$0($1)($2)($3)($4)($5)"
            |> unbox<JS.Promise<obj>>
        let content = unbox<obj array> (Dyn.get result "content")
        let text = Dyn.str content.[0] "text"
        check "executor_wait returns log snippet with session" (text.Contains "log-line-b")
    finally
        Wanxiangshu.Omp.ExecutorTools.executorMinWaitMs <- 500
}

let executorAbortNoSessionReturnsError () = promise {
    reset ()
    let h = createPiHarness ()
    let pi = piObject h
    registerExecutorTools pi
    let abort =
        h.tools |> Seq.find (fun t -> Dyn.str t "name" = "executor_abort")
    let execute = Dyn.get abort "execute"
    let ctx = createObj [ "cwd", box "/tmp" ]
    let! result =
        emitJsExpr (execute, "abort-call", createObj [], jsUndefined, jsUndefined, ctx)
            "$0($1)($2)($3)($4)($5)"
        |> unbox<JS.Promise<obj>>
    let content = unbox<obj array> (Dyn.get result "content")
    let text = Dyn.str content.[0] "text"
    check "executor_abort error when no session" (text.Contains "No executor session found")
}

let executorAbortWithSessionReturnsAbortMessage () = promise {
    reset ()
    let sessionId = "executor-abort-session-1"
    setRunnerJobStateForTest sessionId "running"
    let h = createPiHarness ()
    let pi = piObject h
    registerExecutorTools pi
    let abort =
        h.tools |> Seq.find (fun t -> Dyn.str t "name" = "executor_abort")
    let execute = Dyn.get abort "execute"
    let ctx = createObj [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box sessionId) ]) ]
    let! result =
        emitJsExpr (execute, "abort-call", createObj [], jsUndefined, jsUndefined, ctx)
            "$0($1)($2)($3)($4)($5)"
        |> unbox<JS.Promise<obj>>
    let content = unbox<obj array> (Dyn.get result "content")
    let text = Dyn.str content.[0] "text"
    check "executor_abort returns abort message with session" (text = "Runner abort requested.")
    check "job state cleared after abort" (not (hasRunningRunnerJob sessionId))
}

let run () = promise {
    registersExecutorTools ()
    do! executorWaitNoSessionReturnsError ()
    do! executorWaitWithSessionReturnsLogSnippet ()
    do! executorAbortNoSessionReturnsError ()
    do! executorAbortWithSessionReturnsAbortMessage ()
}
