module Wanxiangshu.Runtime.Fallback.ContinuationIntentExecution

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.ContinuationExecutionCore

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
    : JS.Promise<unit> =
    promise {
        match leaseOfIntent intent with
        | Some lease ->
            let isValid =
                verifyLease runtime sessionID lease
                && ensureActiveAndOwner runtime sessionID lease

            if not isValid then
                do!
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
                with ex ->
                    do!
                        finishContinuation
                            runtime
                            workspaceRoot
                            sessionID
                            lease
                            ContinuationOutcome.Failed
                            ex.Message
                    return ()
        | None ->
            try
                match intent with
                | PropagateFailureIntent ->
                    match (runtime.GetSession sessionID).PendingLease with
                    | Some lease ->
                        do! finishContinuation runtime workspaceRoot sessionID lease ContinuationOutcome.Failed "Fallback propagation failure"
                    | None -> ()
                | _ -> ()
                do! executeContinuationIntent runtime executor workspaceRoot sessionID intent
            with ex ->
                JS.console.error ("fallback continuation effect failed for " + sessionID + ": " + ex.Message)
    }
