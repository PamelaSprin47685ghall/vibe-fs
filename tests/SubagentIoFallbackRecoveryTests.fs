module Wanxiangshu.Tests.SubagentIoFallbackRecoveryTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Opencode.SubagentIo

/// prompt rejects immediately; fallback handler sets Consumed on next microtask — without
/// waitForRecovery, GetConsumed would be None and subagent would surface UnknownJsError.
let runSubagentRecoversWhenFallbackConsumesError () = promise {
    let rt = FallbackRuntimeState()
    let registry = ChildAgentRegistry.Create()
    let childId = "child-fb-recover"
    registry.RegisterChildAgent(childId, "coder", Some "parent-1")

    let client =
        createObj [
            "session", box (createObj [
                "create", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                    promise { return box {| data = box {| id = childId |} |} }))
                "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                    promise { return! Promise.reject (exn "model failed") }))
                "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                    promise {
                        return box {| data = [|
                            box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "done" |} |] |}
                        |] |}
                    }))
                "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
            ])
        ]

    let runP =
        runSubagentCoreResult rt registry client "coder" "t" "go" "/tmp" "parent-1" (box null) (box null) false

    do! yieldMicrotask ()
    rt.SetConsumed childId true

    let! result = runP
    match result with
    | Ok text -> check "recovered text present" (text.Contains "done")
    | Error _ -> failwith "expected Ok after fallback consumed"
}

let run () = promise {
    do! runSubagentRecoversWhenFallbackConsumesError ()
}