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
open Wanxiangshu.Runtime.MuxLogicalReceipt

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
            let session = runtime.GetSession sessionKey

            if session.AbortUnavailable then
                // No reliable abort → never claim Cancelled.
                do!
                    finishNudge
                        runtime
                        workspaceRoot
                        sessionKey
                        lease
                        NudgeOutcome.AbortUnknown
                        abortUnavailableMessage
                        ""
                        ""
            else
                let! abortResult =
                    promise {
                        try
                            do! abortRun sessionKey
                            return Ok()
                        with ex ->
                            return Error ex
                    }

                match abortResult with
                | Ok() ->
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
                | Error ex when isAbortUnavailableMessage ex.Message ->
                    runtime.Update(sessionKey, setAbortUnavailable true)

                    do!
                        finishNudge
                            runtime
                            workspaceRoot
                            sessionKey
                            lease
                            NudgeOutcome.AbortUnknown
                            ("AbortUnavailable: " + ex.Message)
                            ""
                            ""
                | Error ex -> return! Promise.reject ex
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
        // Keep the pending claim. Do not Failed (would clear and allow resend)
        // and do not Dispatched (would pretend HostAccepted).
        let _ =
            fallbackRuntime.UpdateSessionReturning(
                sessionKey,
                tryTransitionPendingNudgeLeaseReturning
                    lease.NudgeID
                    LeaseStatus.DispatchStarted
                    LeaseStatus.AcceptanceUnknown
            )

        let _ =
            fallbackRuntime.UpdateSessionReturning(
                sessionKey,
                tryTransitionPendingNudgeLeaseReturning
                    lease.NudgeID
                    LeaseStatus.Requested
                    LeaseStatus.AcceptanceUnknown
            )

        Promise.lift ()

let private safeNudgeAbort (abortRun: string -> JS.Promise<unit>) (sessionKey: string) : JS.Promise<Result<unit, exn>> =
    promise {
        try
            do! abortRun sessionKey
            return Ok()
        with ex ->
            return Error ex
    }

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
            let session = fallbackRuntime.GetSession sessionKey

            if session.AbortUnavailable then
                do!
                    finishNudge
                        fallbackRuntime
                        workspaceRoot
                        sessionKey
                        lease
                        NudgeOutcome.AbortUnknown
                        "AbortUnavailable skipped: host cannot confirm cancel"
                        ""
                        ""
            else
                let! abortResult = safeNudgeAbort abortRun sessionKey

                match abortResult with
                | Ok() ->
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
                | Error ex when isAbortUnavailableMessage ex.Message ->
                    fallbackRuntime.Update(sessionKey, setAbortUnavailable true)

                    do!
                        finishNudge
                            fallbackRuntime
                            workspaceRoot
                            sessionKey
                            lease
                            NudgeOutcome.AbortUnknown
                            ("AbortUnavailable: " + ex.Message)
                            ""
                            ""
                | Error ex -> return! Promise.reject ex
        else
            do! handleSendOutcome workspaceRoot fallbackRuntime sessionKey lease action nudgeAnchorKey outcome abortRun
    }
