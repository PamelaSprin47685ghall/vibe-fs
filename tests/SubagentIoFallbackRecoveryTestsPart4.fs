module Wanxiangshu.Tests.SubagentIoFallbackRecoveryTestsPart4

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Opencode.SubagentIo

let runSubagentContinueDoesNotResetTaskComplete () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-continue-no-reset"
        registry.RegisterChildAgent(childId, "investigator", Some "parent-3")

        let s0 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s0 with
                Lifecycle = FallbackLifecycle.TaskComplete }

        let textExtracted = ref false
        let promptStartedResolver = ref (fun () -> ())

        let promptStarted =
            Promise.create (fun resolve _ -> promptStartedResolver.Value <- resolve)

        let finalMessages =
            createObj
                [ "data",
                  box
                      [| createObj
                             [ "info", box (createObj [ "role", box "assistant" ])
                               "parts", box [| box (createObj [ "type", box "text"; "text", box "final-output" ]) |] ] |] ]

        let session =
            createObj
                [ "create",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ -> Promise.lift (box {| data = box {| id = childId |} |}))
                  )
                  "prompt",
                  box (
                      System.Func<obj, JS.Promise<unit>>(fun _ ->
                          promise {
                              promptStartedResolver.Value()
                              do! yieldMicrotask ()
                          })
                  )
                  "messages",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ ->
                          textExtracted.Value <- true
                          Promise.lift finalMessages)
                  )
                  "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]

        let client = createObj [ "session", box session ]

        let runP =
            runSubagentCoreResult
                rt
                registry
                client
                "investigator"
                "Continue"
                "go"
                "/tmp"
                "parent-3"
                (box null)
                (box null)
                false
                (Some childId)

        let! result = runP

        check
            "TaskComplete not reset on continue, resolves early"
            ((rt.GetOrCreateState childId).Lifecycle = FallbackLifecycle.TaskComplete)

        check "text extracted immediately" textExtracted.Value

        match result with
        | Ok text -> check "continue output present" (text.Contains "final-output")
        | Error _ -> failwith "expected Ok"
    }

let runSubagentContinueResetsTaskComplete () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-continue-reset"
        registry.RegisterChildAgent(childId, "investigator", Some "parent-4")

        let s0 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s0 with
                Lifecycle = FallbackLifecycle.TaskComplete }

        let textExtracted = ref false
        let promptStartedResolver = ref (fun () -> ())

        let promptStarted =
            Promise.create (fun resolve _ -> promptStartedResolver.Value <- resolve)

        let finalMessages =
            createObj
                [ "data",
                  box
                      [| createObj
                             [ "info", box (createObj [ "role", box "assistant" ])
                               "parts", box [| box (createObj [ "type", box "text"; "text", box "final-output" ]) |] ] |] ]

        let session =
            createObj
                [ "create",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ -> Promise.lift (box {| data = box {| id = childId |} |}))
                  )
                  "prompt",
                  box (
                      System.Func<obj, JS.Promise<unit>>(fun _ ->
                          promise {
                              promptStartedResolver.Value()
                              do! yieldMicrotask ()
                          })
                  )
                  "messages",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ ->
                          textExtracted.Value <- true
                          Promise.lift finalMessages)
                  )
                  "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]

        let client = createObj [ "session", box session ]

        let runP =
            runSubagentCoreResult
                rt
                registry
                client
                "investigator"
                "Continue"
                "go"
                "/tmp"
                "parent-4"
                (box null)
                (box null)
                false
                (Some childId)

        do! promptStarted
        do! yieldMicrotask ()
        do! yieldMicrotask ()

        check "TaskComplete=false blocks early extract on continue" (not textExtracted.Value)

        rt.SetTaskComplete childId true
        let! result = runP
        check "text extracted after continue task completes" textExtracted.Value
    }

let runSubagentSpawnResetsTaskComplete () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-spawn-reset"

        let s0 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s0 with
                Lifecycle = FallbackLifecycle.TaskComplete }

        let textExtracted = ref false
        let promptStartedResolver = ref (fun () -> ())

        let promptStarted =
            Promise.create (fun resolve _ -> promptStartedResolver.Value <- resolve)

        let finalMessages =
            createObj
                [ "data",
                  box
                      [| createObj
                             [ "info", box (createObj [ "role", box "assistant" ])
                               "parts", box [| box (createObj [ "type", box "text"; "text", box "final-output" ]) |] ] |] ]

        let session =
            createObj
                [ "create",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ -> Promise.lift (box {| data = box {| id = childId |} |}))
                  )
                  "prompt",
                  box (
                      System.Func<obj, JS.Promise<unit>>(fun _ ->
                          promise {
                              promptStartedResolver.Value()
                              do! yieldMicrotask ()
                          })
                  )
                  "messages",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ ->
                          textExtracted.Value <- true
                          Promise.lift finalMessages)
                  )
                  "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]

        let client = createObj [ "session", box session ]

        let runP =
            runSubagentCoreResult
                rt
                registry
                client
                "investigator"
                "Spawn"
                "go"
                "/tmp"
                "parent-3"
                (box null)
                (box null)
                false
                None

        do! promptStarted
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        check "TaskComplete reset to false blocks early extract on spawn" (not textExtracted.Value)

        rt.SetTaskComplete childId true
        let! result = runP
        check "text extracted after spawn task completes" textExtracted.Value
    }

let run () =
    promise {
        do! runSubagentContinueDoesNotResetTaskComplete ()
        do! runSubagentContinueResetsTaskComplete ()
        do! runSubagentSpawnResetsTaskComplete ()
    }
