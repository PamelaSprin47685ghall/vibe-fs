module Wanxiangshu.Tests.OmpExecutorToolsTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Omp
open Wanxiangshu.Shell
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

let private reset () =
    RunnerBackground.clearRunnerLogsForTest ExecutorTools.ompScope

let registersExecutorTools () =
    reset ()
    let h = createPiHarness ()
    let pi = piObject h
    ExecutorTools.registerExecutorTools pi
    let names = h.tools |> Seq.map (fun t -> Dyn.str t "name") |> Set.ofSeq
    check "executor tool registered" (names.Contains "executor")
    check "executor_wait tool not registered" (not (names.Contains "executor_wait"))
    check "executor_abort tool not registered" (not (names.Contains "executor_abort"))

let run () = promise { registersExecutorTools () }
