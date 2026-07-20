module Wanxiangshu.Runtime.Fallback.FallbackIdleSettlement

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.LeaseValidationRules
open Wanxiangshu.Runtime.ContinuationEventWriter

let private clearPendingLeaseAfterTerminal
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    : JS.Promise<unit> =
    promise {
        if lease.Status <> LeaseStatus.Cancelled then
            do!
                appendContinuationSettledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    lease.HumanTurnID
                    lease.SessionGeneration
                    "completed"
                    lease.ContinuationOrdinal

        if runtime.UpdateSessionReturning(sessionID, tryClearPendingLeaseReturning lease.ContinuationID) then
            if (runtime.GetSession sessionID).Owner = SessionOwner.Fallback then
                runtime.UpdateSession(sessionID, transferOwnership SessionOwner.NoOwner)

            runtime.Update(sessionID, setMainContinuationAwaitingStart false)
    }

/// Gate lease terminalisation by idle disposition (SPEC §七 step 6).
let handleTerminalPostSettlement
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (evt: FallbackEvent)
    (finalState2: SessionFallbackState)
    (intentOpt: 'Intent option)
    : JS.Promise<unit> =
    promise {
        let isPostTerminal =
            evt <> FallbackEvent.SessionBusy
            && (finalState2.Lifecycle = FallbackLifecycle.TaskComplete
                || finalState2.Lifecycle = FallbackLifecycle.Cancelled
                || finalState2.Phase = FallbackPhase.Exhausted
                || (finalState2.Phase = FallbackPhase.Idle && intentOpt.IsNone))

        if isPostTerminal then
            match (runtime.GetSession sessionID).PendingLease with
            | Some lease ->
                let session = runtime.GetSession sessionID

                let strongTerminal =
                    finalState2.Lifecycle = FallbackLifecycle.Cancelled
                    || finalState2.Lifecycle = FallbackLifecycle.TaskComplete
                    || finalState2.Phase = FallbackPhase.Exhausted

                let isIdleEvt = evt = FallbackEvent.SessionIdle

                if strongTerminal then
                    do! clearPendingLeaseAfterTerminal runtime workspaceRoot sessionID lease
                elif isIdleEvt then
                    match classifyIdleDisposition session false false with
                    | MaySettle _ -> do! clearPendingLeaseAfterTerminal runtime workspaceRoot sessionID lease
                    | NeedsReconciliation(cid, hostMsgId) when workspaceRoot <> "" ->
                        do!
                            appendContinuationIdleReconciliationOrFail
                                workspaceRoot
                                sessionID
                                cid
                                hostMsgId
                                lease.ContinuationOrdinal
                    | NeedsReconciliation _
                    | RejectNotHostAccepted _
                    | SessionHintOnly
                    | IdempotentIgnore -> ()
                elif lease.HostUserMessageId <> "" then
                    do! clearPendingLeaseAfterTerminal runtime workspaceRoot sessionID lease
                else
                    ()
            | None -> ()
    }

/// Filter idle events: only SessionHintOnly / attributed MaySettle pass through.
/// NeedsReconciliation emits a stub event and drops the idle from the state machine.
let filterIdleEvent
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (session: FallbackSessionRuntime)
    (eventOpt: FallbackEvent option)
    (isMatchedContinuation: bool)
    (continuationId: string)
    : JS.Promise<FallbackEvent option> =
    match eventOpt with
    | Some FallbackEvent.NewUserMessage -> promise { return eventOpt }
    | Some FallbackEvent.SessionIdle ->
        match classifyIdleDisposition session isMatchedContinuation false with
        | SessionHintOnly -> promise { return eventOpt }
        | MaySettle _ when isMatchedContinuation -> promise { return eventOpt }
        | NeedsReconciliation(cid, hostMsgId) ->
            promise {
                if workspaceRoot <> "" then
                    let ord =
                        session.PendingLease
                        |> Option.map (fun l -> l.ContinuationOrdinal)
                        |> Option.defaultValue 0

                    do!
                        appendContinuationIdleReconciliationOrFail
                            workspaceRoot
                            sessionID
                            cid
                            hostMsgId
                            ord

                return None
            }
        | MaySettle _
        | RejectNotHostAccepted _
        | IdempotentIgnore -> promise { return None }
    | _ ->
        promise {
            let hasPending = session.PendingLease.IsSome

            if hasPending && continuationId = "" && not isMatchedContinuation then
                return None
            else
                return eventOpt
        }
