module Wanxiangshu.Runtime.Fallback.ContinuationIntentExecution

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.ContinuationExecutionCore
<<<<<<< HEAD
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps
=======
>>>>>>> 11a984b6 (fix: exhaustive LeaseStatus/DispatchTerminal matches and recovery gating)
open Wanxiangshu.Runtime.MuxLogicalReceipt

let private leaseOfIntent (intent: ContinuationIntent) : PendingLease option =
    match intent with
    | SendContinueIntent(model, _, turnId, gen, cancelGen, continuationID, continuationOrdinal) ->
        Some
            { ContinuationID = continuationID
              ContinuationOrdinal = continuationOrdinal
              SessionGeneration = gen
              HumanTurnID = turnId
              HostUserMessageId = ""
              HostRunId = ""
              CancelGeneration = cancelGen
              Owner = SessionOwner.Fallback
              Model = model
              PromptText = None
              Status = LeaseStatus.Requested }
    | RecoverWithPromptIntent(model, promptText, _, turnId, gen, cancelGen, continuationID, continuationOrdinal) ->
        Some
            { ContinuationID = continuationID
              ContinuationOrdinal = continuationOrdinal
              SessionGeneration = gen
              HumanTurnID = turnId
              HostUserMessageId = ""
              HostRunId = ""
              CancelGeneration = cancelGen
              Owner = SessionOwner.Fallback
              Model = model
              PromptText = Some promptText
              Status = LeaseStatus.Requested }
    | PropagateFailureIntent -> None

let run
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (intent: ContinuationIntent)
    (reenter: SessionReenter)
    : JS.Promise<unit> =
    promise {
        match leaseOfIntent intent with
        | Some lease ->
            let! isValid =
                promise {
                    let mutable ok = false

                    do!
                        reenter (fun () ->
                            promise {
                                ok <-
                                    verifyLease runtime sessionID lease
                                    && ensureActiveAndOwner runtime sessionID lease
                            })

                    return ok
                }

            if not isValid then
                do!
<<<<<<< HEAD
            if not isValid then
                do!
                    reenter (fun () ->
                        finishContinuation
                            runtime
                            workspaceRoot
                            sessionID
                            lease
                            ContinuationOutcome.Cancelled
                            "Pre-submission lease validation failed")
            else
                try
                    do! executeContinuationIntent runtime executor workspaceRoot sessionID intent reenter
=======
                    finishContinuation
                        runtime
                        workspaceRoot
                        sessionID
                        lease
                        ContinuationOutcome.Cancelled
                        "Pre-submission lease validation failed"
                return ()
            else
                try
                    do! executeContinuationIntent runtime executor workspaceRoot sessionID intent

                    match (runtime.GetSession sessionID).PendingLease with
                    | Some current when
                        current.ContinuationID = lease.ContinuationID
                        && current.Status = LeaseStatus.Dispatched ->
                        ()
                    | _ -> ()
>>>>>>> 11a984b6 (fix: exhaustive LeaseStatus/DispatchTerminal matches and recovery gating)
                with ex ->
                    let outcome, reason =
                        if isAcceptanceUnknownMessage ex.Message then
                            ContinuationOutcome.AcceptanceUnknown, ex.Message
                        elif isAbortUnavailableMessage ex.Message then
                            ContinuationOutcome.AbortUnknown, ex.Message
                        else
                            ContinuationOutcome.Failed, ex.Message

<<<<<<< HEAD
                    do!
                        reenter (fun () ->
                            finishContinuation runtime workspaceRoot sessionID lease outcome reason)
=======
                    do! finishContinuation runtime workspaceRoot sessionID lease outcome reason
                    return ()
>>>>>>> 11a984b6 (fix: exhaustive LeaseStatus/DispatchTerminal matches and recovery gating)
        | None ->
            try
                match intent with
                | PropagateFailureIntent ->
                    do!
                        reenter (fun () ->
                            promise {
                                match (runtime.GetSession sessionID).PendingLease with
                                | Some lease ->
                                    do!
                                        finishContinuation
                                            runtime
                                            workspaceRoot
                                            sessionID
                                            lease
                                            ContinuationOutcome.Failed
                                            "Fallback propagation failure"
                                | None -> ()

                                do! executor.PropagateFailure sessionID
                            })
                | _ -> do! executeContinuationIntent runtime executor workspaceRoot sessionID intent reenter
            with ex ->
                JS.console.error ("fallback continuation effect failed for " + sessionID + ": " + ex.Message)
    }

/// Compatibility entry for recovery paths that have no session actor queue yet.
let runInline
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (intent: ContinuationIntent)
    : JS.Promise<unit> =
    run runtime executor workspaceRoot sessionID intent inlineReenter
