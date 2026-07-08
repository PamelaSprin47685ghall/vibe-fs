module Wanxiangshu.Tests.SubagentIoFallbackRecoveryTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Opencode.SubagentIo

/// prompt rejects immediately; fallback handler sets Consumed on next microtask — without
/// waitForRecovery, GetConsumed would be None and subagent would surface UnknownJsError.
let runSubagentRecoversWhenFallbackConsumesError () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-fb-recover"
        registry.RegisterChildAgent(childId, "coder", Some "parent-1")

        let client =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "create",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise { return box {| data = box {| id = childId |} |} })
                            )
                            "prompt",
                            box (
                                System.Func<obj, JS.Promise<unit>>(fun _ ->
                                    promise { return! Promise.reject (exn "model failed") })
                            )
                            "messages",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| info = box {| role = "assistant" |}
                                                              parts = [| box {| ``type`` = "text"; text = "done" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let runP =
            runSubagentCoreResult rt registry client "coder" "t" "go" "/tmp" "parent-1" (box null) (box null) false

        do! yieldMicrotask ()
        rt.SetConsumed childId true

        let! result = runP

        match result with
        | Ok text -> check "recovered text present" (text.Contains "done")
        | Error _ -> failwith "expected Ok after fallback consumed"
    }

/// Model outputs malformed XML tool calls as raw text.  The event handler sets
/// phase to ScanningToolCallText.  After promptWithAbort resolves,
/// waitForToolCallTextRecovery blocks the subagent from reading text until the
/// phase clears to Idle — preventing the subagent from returning “empty” to the
/// parent.
let runSubagentWaitsForToolCallTextRecovery () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-tct-wait"
        registry.RegisterChildAgent(childId, "investigator", Some "parent-1")

        let textExtracted = ref false

        let client =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "create",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise { return box {| data = box {| id = childId |} |} })
                            )
                            "prompt",
                            box (
                                System.Func<obj, JS.Promise<unit>>(fun _ ->
                                    promise {
                                        // Simulate: session.idle fires, event handler sets
                                        // phase to ScanningToolCallText synchronously before
                                        // prompt resolves.
                                        let s0 = rt.GetOrCreateState childId

                                        rt.UpdateState
                                            childId
                                            { s0 with
                                                Phase = FallbackPhase.ScanningToolCallText }
                                    })
                            )
                            "messages",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        textExtracted.Value <- true

                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| info = box {| role = "assistant" |}
                                                              parts = [| box {| ``type`` = "text"; text = "recovered" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let runP =
            runSubagentCoreResult rt registry client "investigator" "t" "go" "/tmp" "parent-1" (box null) (box null) false

        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()

        check "text not extracted while recovery in progress" (not textExtracted.Value)

        let s1 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s1 with
                Phase = FallbackPhase.Idle }

        let! result = runP

        check "text extracted after recovery settled" textExtracted.Value

        match result with
        | Ok text -> check "recovered output present" (text.Contains "recovered")
        | Error _ -> failwith "expected Ok"
    }

let run () =
    promise {
        do! runSubagentRecoversWhenFallbackConsumesError ()
        do! runSubagentWaitsForToolCallTextRecovery ()
    }
