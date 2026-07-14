module Wanxiangshu.Tests.SubagentIoFallbackRecoveryTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.ChildSessionMailbox
open Wanxiangshu.Opencode.SubagentIo
open Wanxiangshu.Tests.TempWorkspace

let private waitForListenerRegistered (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    let rec poll () =
        promise {
            if
                runtime.HasListeners sessionID
                || ChildSessionMailboxRegistry.TryGet sessionID |> Option.isSome
            then
                return ()
            else
                do! yieldMicrotask ()
                return! poll ()
        }

    poll ()

/// prompt rejects immediately; fallback handler sets Consumed on next microtask — without
/// waitForRecovery, GetConsumed would be None and subagent would surface UnknownJsError.
open Wanxiangshu.Kernel.Domain

let runSubagentRecoversWhenFallbackConsumesError () =
    promise {
        let! dir = mkdtempAsync "subagent-fb-recover-"

        try
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
                                                                  parts =
                                                                   [| box {| ``type`` = "text"; text = "done" |} |] |} |] |}
                                        })
                                )
                                "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                      ) ]

            let runP =
                let rscr
                    : FallbackRuntimeState
                          -> ChildAgentRegistry
                          -> obj
                          -> string
                          -> string
                          -> string
                          -> string
                          -> string
                          -> obj
                          -> obj
                          -> bool
                          -> string option
                          -> JS.Promise<Result<string, DomainError>> =
                    runSubagentCoreResult

                rscr rt registry client "coder" "t" "go" dir "parent-1" (box null) (box null) false None

            do! waitForListenerRegistered rt childId
            do! yieldMicrotask ()
            rt.ClearSubsessionPending childId
            rt.SetConsumed childId true
            let s0 = rt.GetOrCreateState childId

            rt.UpdateState
                childId
                { s0 with
                    Lifecycle = FallbackLifecycle.TaskComplete }

            // Post events to child session mailbox for event-driven flow
            match ChildSessionMailboxRegistry.TryGet childId with
            | Some mb ->
                do! mb.Post(Command.TaskComplete "")
                do! mb.Post(Command.SessionIdle)
            | None -> ()

            let! result = runP

            do! rmAsync dir

            match result with
            | Ok text -> check "recovered text present" (text.Contains "done")
            | Error _ -> failwith "expected Ok after fallback consumed"
        with ex ->
            do! rmAsync dir
            return! Promise.reject ex
    }

/// Model outputs malformed XML tool calls as raw text.  The event handler sets
/// phase to ScanningToolCallText.  After promptWithAbort resolves,
/// waitForToolCallTextRecovery blocks the subagent from reading text until the
/// phase clears to Idle — preventing the subagent from returning “empty” to the
/// parent.
let runSubagentWaitsForToolCallTextRecovery () =
    promise {
        let! dir = mkdtempAsync "subagent-tct-wait-"

        try
            let rt = FallbackRuntimeState()
            let registry = ChildAgentRegistry.Create()
            let childId = "child-tct-wait"
            registry.RegisterChildAgent(childId, "investigator", Some "parent-1")

            let messagesCallCount = ref 0

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
                                            messagesCallCount.Value <- messagesCallCount.Value + 1

                                            return
                                                box
                                                    {| data =
                                                        [| box
                                                               {| info = box {| role = "assistant" |}
                                                                  parts =
                                                                   [| box
                                                                          {| ``type`` = "text"
                                                                             text = "recovered" |} |] |} |] |}
                                        })
                                )
                                "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                      ) ]

            let runP =
                let rscr
                    : FallbackRuntimeState
                          -> ChildAgentRegistry
                          -> obj
                          -> string
                          -> string
                          -> string
                          -> string
                          -> string
                          -> obj
                          -> obj
                          -> bool
                          -> string option
                          -> JS.Promise<Result<string, DomainError>> =
                    runSubagentCoreResult

                rscr rt registry client "investigator" "t" "go" dir "parent-1" (box null) (box null) false None

            do! waitForListenerRegistered rt childId
            do! yieldMicrotask ()

            check
                "messages called once for startCount baseline query while recovery in progress"
                (messagesCallCount.Value = 1)

            let s1 = rt.GetOrCreateState childId

            rt.UpdateState
                childId
                { s1 with
                    Phase = FallbackPhase.Idle
                    Lifecycle = FallbackLifecycle.TaskComplete }

            rt.ClearSubsessionPending childId

            // Post events to child session mailbox for event-driven flow
            match ChildSessionMailboxRegistry.TryGet childId with
            | Some mb ->
                do! mb.Post(Command.TaskComplete "")
                do! mb.Post(Command.SessionIdle)
            | None -> ()

            let! result = runP

            check "messages called twice after recovery settled" (messagesCallCount.Value = 2)

            do! rmAsync dir

            match result with
            | Ok text -> check "recovered output present" (text.Contains "recovered")
            | Error _ -> failwith "expected Ok"
        with ex ->
            do! rmAsync dir
            return! Promise.reject ex
    }

let run () =
    promise {
        do! runSubagentRecoversWhenFallbackConsumesError ()
        do! runSubagentWaitsForToolCallTextRecovery ()
    }
