module Wanxiangshu.Tests.FallbackRequestedRecoveryTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.EventLogRuntimeRecovery
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps

type ExecutorCall =
    | SendContinueCall of sessionID: string * continuationID: string
    | RecoverWithPromptCall of sessionID: string * continuationID: string * promptText: string

let private fakeExecutor (calls: ResizeArray<ExecutorCall>) : IActionExecutor =
    { new IActionExecutor with
        member _.SendContinue(sessionID, _model, continuationID) =
            promise {
                calls.Add(SendContinueCall(sessionID, continuationID))
                return ()
            }

        member _.RecoverWithPrompt(sessionID, _model, promptText, continuationID) =
            promise {
                calls.Add(RecoverWithPromptCall(sessionID, continuationID, promptText))
                return ()
            }

        member _.FetchMessages _ = Promise.lift [||]
        member _.PropagateFailure _ = Promise.lift ()
        member _.CaptureCurrentModel _ = Promise.lift None
        member _.AbortRun _ = Promise.lift () }

let private defaultModel =
    { ProviderID = "openai"
      ModelID = "gpt-5"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private setupSessionIdentity (rt: FallbackRuntimeStore) (sid: string) =
    rt.Update(sid, fun s ->
        { s with
            HumanTurnId = "turn-1"
            SessionGeneration = 1
            CancelGeneration = 0
            AgentName = "reviewer" })

let private setupRequestedSendContinue (rt: FallbackRuntimeStore) (sid: string) =
    setupSessionIdentity rt sid
    rt.Update(sid, startDispatch defaultModel None)

let private setupRequestedRecoverWithPrompt (rt: FallbackRuntimeStore) (sid: string) (prompt: string) =
    setupSessionIdentity rt sid
    rt.Update(sid, startDispatch defaultModel (Some prompt))

let private markLeaseDispatched (rt: FallbackRuntimeStore) (sid: string) =
    match (rt.GetSession sid).PendingLease with
    | Some lease ->
        rt.Update(sid, fun s -> { s with PendingLease = Some { lease with Status = LeaseStatus.Dispatched } })
    | None -> ()

let requestedSendContinueIsDispatchedOnce () =
    promise {
        let rt = FallbackRuntimeStore()
        let scope = RuntimeScope()
        let calls = ResizeArray<ExecutorCall>()
        scope.Add("fallbackRuntime", box rt)
        registerFallbackExecutor scope (fakeExecutor calls)

        setupRequestedSendContinue rt "s1"
        let leaseBefore = (rt.GetSession "s1").PendingLease.Value
        check "lease starts Requested" (leaseBefore.Status = LeaseStatus.Requested)

        do! recoverRequestedFallbackLeases scope ""

        check "requested send-continue dispatched once" (calls.Count = 1)

        match calls.[0] with
        | SendContinueCall(sid, cid) ->
            check "session id passed" (sid = "s1")
            check "continuation id preserved" (cid = leaseBefore.ContinuationID)
        | _ -> failwith "expected SendContinue call"

        let leaseAfter = (rt.GetSession "s1").PendingLease.Value
        check "lease becomes Dispatched" (leaseAfter.Status = LeaseStatus.Dispatched)
        check "continuation id unchanged" (leaseAfter.ContinuationID = leaseBefore.ContinuationID)
    }

let requestedRecoverWithPromptIsDispatchedOnce () =
    promise {
        let rt = FallbackRuntimeStore()
        let scope = RuntimeScope()
        let calls = ResizeArray<ExecutorCall>()
        scope.Add("fallbackRuntime", box rt)
        registerFallbackExecutor scope (fakeExecutor calls)

        setupRequestedRecoverWithPrompt rt "s2" "recover this"
        let leaseBefore = (rt.GetSession "s2").PendingLease.Value
        check "lease starts Requested" (leaseBefore.Status = LeaseStatus.Requested)

        do! recoverRequestedFallbackLeases scope ""

        check "requested recover-with-prompt dispatched once" (calls.Count = 1)

        match calls.[0] with
        | RecoverWithPromptCall(sid, cid, prompt) ->
            check "session id passed" (sid = "s2")
            check "continuation id preserved" (cid = leaseBefore.ContinuationID)
            check "prompt text preserved" (prompt = "recover this")
        | _ -> failwith "expected RecoverWithPrompt call"

        let leaseAfter = (rt.GetSession "s2").PendingLease.Value
        check "lease becomes Dispatched" (leaseAfter.Status = LeaseStatus.Dispatched)
    }

let dispatchedLeaseIsNotRedispatched () =
    promise {
        let rt = FallbackRuntimeStore()
        let scope = RuntimeScope()
        let calls = ResizeArray<ExecutorCall>()
        scope.Add("fallbackRuntime", box rt)
        registerFallbackExecutor scope (fakeExecutor calls)

        setupRequestedSendContinue rt "s3"
        markLeaseDispatched rt "s3"
        check "lease is Dispatched" ((rt.GetSession "s3").PendingLease.Value.Status = LeaseStatus.Dispatched)

        do! recoverRequestedFallbackLeases scope ""

        check "dispatched lease not redispatched" (calls.Count = 0)
        check "status stays Dispatched" ((rt.GetSession "s3").PendingLease.Value.Status = LeaseStatus.Dispatched)
    }

let cancelledLeaseIsNotRedispatched () =
    promise {
        let rt = FallbackRuntimeStore()
        let scope = RuntimeScope()
        let calls = ResizeArray<ExecutorCall>()
        scope.Add("fallbackRuntime", box rt)
        registerFallbackExecutor scope (fakeExecutor calls)

        setupRequestedSendContinue rt "s4"

        match (rt.GetSession "s4").PendingLease with
        | Some lease ->
            rt.Update("s4", fun s -> { s with PendingLease = Some { lease with Status = LeaseStatus.Cancelled } })
        | None -> ()

        do! recoverRequestedFallbackLeases scope ""

        check "cancelled lease not redispatched" (calls.Count = 0)
    }

let staleGenerationLeaseIsNotDispatched () =
    promise {
        let rt = FallbackRuntimeStore()
        let scope = RuntimeScope()
        let calls = ResizeArray<ExecutorCall>()
        scope.Add("fallbackRuntime", box rt)
        registerFallbackExecutor scope (fakeExecutor calls)

        setupRequestedSendContinue rt "s5"
        rt.Update("s5", fun s -> { s with SessionGeneration = 999 })

        do! recoverRequestedFallbackLeases scope ""

        check "stale generation lease not dispatched" (calls.Count = 0)
    }

let appendFailureDoesNotMutateMemory () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "s-fail"
        rt.Update(sid, fun s ->
            { s with
                HumanTurnId = "turn-1"
                SessionGeneration = 1
                CancelGeneration = 0
                Owner = SessionOwner.NoOwner
                PendingLease = None })

        let mutable caught = false
        try
            // Using a path that will fail file I/O
            let finalState = (rt.GetSession sid).Core
            let! _ = handleContinuationAction rt "/nonexistent/directory/path/here" sid finalState defaultModel None
            ()
        with _ ->
            caught <- true

        check "append failed and caught" caught
        let session = rt.GetSession sid
        check "memory unchanged: PendingLease is None" (session.PendingLease.IsNone)
        check "memory unchanged: Owner is still NoOwner" (session.Owner = SessionOwner.NoOwner)
    }

let propagateFailureClearsLeaseAndTransfersOwnership () =
    promise {
        let rt = FallbackRuntimeStore()
        let calls = ResizeArray<ExecutorCall>()
        let executor = fakeExecutor calls

        setupRequestedSendContinue rt "s-prop"
        let leaseBefore = (rt.GetSession "s-prop").PendingLease.Value
        rt.Update("s-prop", transferOwnership SessionOwner.Fallback)
        check "starts with Fallback owner" ((rt.GetSession "s-prop").Owner = SessionOwner.Fallback)
        check "starts with requested lease" ((rt.GetSession "s-prop").PendingLease.IsSome)

        do! Wanxiangshu.Runtime.Fallback.ContinuationIntentExecution.runInline rt executor "" "s-prop" PropagateFailureIntent

        let sessionAfter = rt.GetSession "s-prop"
        check "lease is cleared" (sessionAfter.PendingLease.IsNone)
        check "ownership transferred to NoOwner" (sessionAfter.Owner = SessionOwner.NoOwner)
    }

let run () =
    promise {
        do! requestedSendContinueIsDispatchedOnce ()
        do! requestedRecoverWithPromptIsDispatchedOnce ()
        do! dispatchedLeaseIsNotRedispatched ()
        do! cancelledLeaseIsNotRedispatched ()
        do! staleGenerationLeaseIsNotDispatched ()
        do! appendFailureDoesNotMutateMemory ()
        do! propagateFailureClearsLeaseAndTransfersOwnership ()
    }
