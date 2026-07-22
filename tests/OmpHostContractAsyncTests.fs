module Wanxiangshu.Tests.OmpHostContractAsyncTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Hosts.Omp.OmpSubsessionHostHelper
open Wanxiangshu.Hosts.Omp.Fallback.ActionExecutor
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Tests.OmpHostContractCoreTests

module Dyn = Wanxiangshu.Runtime.Dyn
module OmpHost = Wanxiangshu.Hosts.Omp.SubsessionHostAdapter

let private fail = sharedFail
let private containsKey = sharedContainsKey
let private makeTurn = sharedMakeTurn
let private sampleModel = sharedSampleModel
let private emptyModel = sharedEmptyModel

let cancelPendingDispatchIsReal () =
    promise {
        let resolveRef = ref (fun (_: obj) -> ())
        let promptP = Promise.create (fun resolve _ -> resolveRef.Value <- resolve)
        let abortCalled = ref false

        let session =
            createObj
                [ "prompt", box (fun (_: obj) -> promptP)
                  "abort",
                  box (fun () ->
                      abortCalled.Value <- true
                      Promise.lift (box null)) ]

        let pi = createObj [ "session", box (createObj []) ]
        let host = OmpHost.createHost session "" pi "omp-contract-cancel"
        let sid = SessionId.create "child-cancel"
        let turnId = TurnId.create "run-cancel-t0"

        let plan = { makeTurn (TurnId.value turnId) "x" None with TurnId = turnId }

        let dispatchP = host.Dispatch(sid, plan)
        do! sleep 5
        host.CancelPendingDispatch turnId

        check "cancel triggers physical abort" abortCalled.Value

        let resolveResult =
            HostReceiptWaiterRegistry.tryResolve
                (Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick "omp:omp-contract-cancel")
                (SessionId.value sid)
                (TurnId.value turnId)
                (UserMessageObserved "late")

        check "late receipt ignored after cancel" (resolveResult <> ResolveAttemptResult.ResolvedNow)

        let! result = dispatchP

        match result with
        | Error(HostRejected e) when e.Message = HostReceiptWaiter.cancelError.Message ->
            check "dispatch rejected by cancel" true
        | other -> fail ("expected cancel reject, got " + string other)

        resolveRef.Value (box {| id = "ignored" |})
    }

let actionExecutorOmitsEmptyModelAndRequiresReceipt () =
    promise {
        let captured = ref (box null)

        let sessionApi =
            createObj
                [ "sessionPrompt",
                  box (fun (arg: obj) ->
                      captured.Value <- arg
                      Promise.lift (box {| id = "cont-msg-1" |}))
                  "sessionMessages", box (fun (_: obj) -> Promise.lift (box {| data = [||] |})) ]

        let runtime = FallbackRuntimeStore()
        let exec = ompActionExecutor runtime sessionApi

        do! exec.SendContinue("sess-1", sampleModel, "cid-1")

        // HTTP request envelope field is host `body`; nested `prompt` is structured payload.
        let promptPayload = Dyn.get (Dyn.get captured.Value "body") "prompt"
        check "continuation model present" (containsKey promptPayload "model")
        check "continuationID present" (containsKey promptPayload "continuationID")

        do! exec.SendContinue("sess-1", emptyModel, "cid-2")
        let promptPayload2 = Dyn.get (Dyn.get captured.Value "body") "prompt"
        check "empty model omitted not empty string" (not (containsKey promptPayload2 "model"))

        let noIdApi =
            createObj
                [ "sessionPrompt", box (fun (_: obj) -> Promise.lift (box null))
                  "sessionMessages", box (fun (_: obj) -> Promise.lift (box {| data = [||] |})) ]

        let exec2 = ompActionExecutor runtime noIdApi
        let mutable failed = false

        try
            do! exec2.SendContinue("sess-1", sampleModel, "cid-3")
        with ex ->
            failed <- ex.Message.Contains "AcceptanceUnknown"

        check "prompt without id is AcceptanceUnknown" failed
    }

let waitForIdleAfterBaselineRequiresGrowth () =
    promise {
        let entries = ResizeArray<obj>()
        entries.Add(box {| role = "user"; id = "old" |})

        let session =
            createObj
                [ "waitForIdle", box (fun () -> Promise.lift ())
                  "sessionManager",
                  box (createObj [ "getEntries", box (fun () -> entries.ToArray()) ]) ]

        let baseline = entryCountOfSession session
        let! grewStale = waitForIdleAfterBaseline session baseline 2
        check "stale idle without growth → false" (not grewStale)

        entries.Add(box {| role = "assistant"; id = "new" |})
        let! grew = waitForIdleAfterBaseline session baseline 2
        check "growth past baseline → true" grew
    }
