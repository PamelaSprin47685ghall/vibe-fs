module Wanxiangshu.Runtime.NudgeOutcomeHandler

open Fable.Core
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

let processDeliveredOutcome
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionKey: string)
    (lease: NudgeLease)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        let dispatchedLease =
            { lease with
                Status = LeaseStatus.Dispatched }

        do!
            finishNudge
                runtime
                workspaceRoot
                sessionKey
                dispatchedLease
                NudgeOutcome.Dispatched
                ""
                (toString action)
                nudgeAnchorKey

        if
            not (
                runtime.UpdateSessionReturning(
                    sessionKey,
                    tryTransitionPendingNudgeLeaseReturning
                        lease.NudgeID
                        LeaseStatus.DispatchStarted
                        LeaseStatus.Dispatched
                )
            )
        then
            do! abortRun sessionKey

            do!
                finishNudge
                    runtime
                    workspaceRoot
                    sessionKey
                    lease
                    NudgeOutcome.Cancelled
                    "Cancelled after dispatch"
                    ""
                    ""
    }

let private handleSendOutcome
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionKey: string)
    (lease: NudgeLease)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (outcome: SendOutcome)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<unit> =
    match outcome with
    | SendOutcome.Delivered ->
        processDeliveredOutcome fallbackRuntime workspaceRoot sessionKey lease action nudgeAnchorKey abortRun
    | SendOutcome.Busy ->
        finishNudge fallbackRuntime workspaceRoot sessionKey lease NudgeOutcome.Failed "Session busy" "" ""
    | SendOutcome.Aborted ->
        finishNudge fallbackRuntime workspaceRoot sessionKey lease NudgeOutcome.Cancelled "Aborted by client" "" ""
    | SendOutcome.Failed msg -> finishNudge fallbackRuntime workspaceRoot sessionKey lease NudgeOutcome.Failed msg "" ""
    | SendOutcome.TransportUnavailable msg ->
        finishNudge
            fallbackRuntime
            workspaceRoot
            sessionKey
            lease
            NudgeOutcome.Failed
            ("TransportUnavailable: " + msg)
            ""
            ""
    | SendOutcome.NotNeeded -> Promise.lift ()
    | SendOutcome.SnapshotUnavailable msg ->
        finishNudge
            fallbackRuntime
            workspaceRoot
            sessionKey
            lease
            NudgeOutcome.Failed
            ("SnapshotUnavailable: " + msg)
            ""
            ""
    | SendOutcome.ClaimConflict -> Promise.lift ()
    | SendOutcome.EventStoreFailure msg ->
        finishNudge
            fallbackRuntime
            workspaceRoot
            sessionKey
            lease
            NudgeOutcome.Failed
            ("EventStoreFailure: " + msg)
            ""
            ""
    | SendOutcome.AcceptanceUnknown msg ->
        finishNudge
            fallbackRuntime
            workspaceRoot
            sessionKey
            lease
            NudgeOutcome.Failed
            ("AcceptanceUnknown: " + msg)
            ""
            ""

let validateAndFinalizeOutcome
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionKey: string)
    (lease: NudgeLease)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (outcome: SendOutcome)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        if not (isLeaseValid fallbackRuntime sessionKey lease) then
            do! abortRun sessionKey

            do!
                finishNudge
                    fallbackRuntime
                    workspaceRoot
                    sessionKey
                    lease
                    NudgeOutcome.Cancelled
                    "Cancelled after dispatch"
                    ""
                    ""
        else
            do! handleSendOutcome workspaceRoot fallbackRuntime sessionKey lease action nudgeAnchorKey outcome abortRun
    }
