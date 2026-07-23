module Wanxiangshu.Tests.OmpToolsCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore

module Dyn = Wanxiangshu.Runtime.Dyn

let toolNames (h: PiHarness) =
    h.tools |> Seq.map (fun t -> Dyn.str t "name") |> Seq.toList |> List.rev

let commandNames (h: PiHarness) =
    h.commands |> Seq.map (fun c -> Dyn.str c "name") |> Seq.toList |> List.rev

let run () =
    promise {
        // ---- ExecutorTools ----
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        Wanxiangshu.Hosts.Omp.ExecutorTools.registerExecutorTools pi
        let names = toolNames h |> Set.ofList
        check "executor tool registered" (names.Contains "executor")
        check "executor_wait tool not registered" (not (names.Contains "executor_wait"))
        check "executor_abort tool not registered" (not (names.Contains "executor_abort"))
        let execTool = h.tools |> Seq.find (fun t -> Dyn.str t "name" = "executor")
        check "executor has parameters" (Dyn.has execTool "parameters")
        check "executor has execute" (Dyn.has execTool "execute")


        // ---- ReviewToolsRegister: registerLoopFeatures ----
        resetPluginState ()
        let h3 = createPiHarness ()
        let pi3 = piObject h3
        let store = createReviewStore ()
        Wanxiangshu.Hosts.Omp.ReviewToolsRegister.registerLoopFeatures pi3 store
        let names3 = toolNames h3 |> Set.ofList
        check "submit_review tool registered" (names3.Contains "submit_review")
        check "return_reviewer tool registered" (names3.Contains "return_reviewer")
        let cmds = commandNames h3 |> Set.ofList
        check "loop command registered" (cmds.Contains "loop")

        // ---- ReviewToolsRegister: registerInputHandler ----
        resetPluginState ()
        let h4 = createPiHarness ()
        let pi4 = piObject h4
        let store4 = createReviewStore ()
        Wanxiangshu.Hosts.Omp.ReviewToolsRegister.registerInputHandler pi4 store4
        let events = Dyn.get (Dyn.get h4.hookStore "events") "input"
        check "input handler registered" (Dyn.isArray events && (unbox<obj array> events).Length > 0)

        // ---- ReviewToolsLoop: handleLoopCommand minimal path ----
        resetPluginState ()
        let h5 = createPiHarness ()
        let pi5 = piObject h5
        let store5 = createReviewStore ()
        let! workspaceDir = mkdtempAsync "omp-cov-session-"
        // ctx with getSessionId returning a valid id and a no-op notify
        let ctx =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box "cov-session") ])
                  "ui", box (createObj [ "notify", box (fun (_: string) (_: string) -> ()) ])
                  "cwd", box workspaceDir ]
        // active review → activates loop, should call sendMessage
        do! Wanxiangshu.Hosts.Omp.ReviewToolsLoop.handleLoopCommand pi5 store5 "fix login flow" ctx
        check "loop activated without error" true
        let msgs = Dyn.get h5.hookStore "messages"
        check "sendMessage captured after activate" (Dyn.isArray msgs && (unbox<obj array> msgs).Length = 1)
        let msg = (unbox<obj array> msgs).[0]

        check
            "message customType is wanxiangshu-loop-activated"
            (Dyn.str (Dyn.get msg "message") "customType" = "wanxiangshu-loop-activated")
        // cancel the loop
        do! Wanxiangshu.Hosts.Omp.ReviewToolsLoop.handleLoopCommand pi5 store5 "" ctx
        check "loop cancelled without error" true
        do! rmAsync workspaceDir
    }
