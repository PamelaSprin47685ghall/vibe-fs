module Wanxiangshu.Runtime.EventLogRuntimeRecovery

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.ContinuationIntentExecution
open Wanxiangshu.Runtime.RuntimeScope

/// Register the host-specific fallback action executor in the runtime scope so
/// startup recovery can dispatch outstanding requested leases.
let registerFallbackExecutor (scope: RuntimeScope) (executor: IActionExecutor) : unit =
    scope.Add("fallbackExecutor", box executor)

let private intentFromLease (session: FallbackSessionRuntime) (lease: PendingLease) : ContinuationIntent option =
    let agent = session.AgentName

    match lease.PromptText with
    | None ->
        Some(
            SendContinueIntent(
                lease.Model,
                agent,
                lease.HumanTurnID,
                lease.SessionGeneration,
                lease.CancelGeneration,
                lease.ContinuationID,
                lease.ContinuationOrdinal
            )
        )
    | Some promptText ->
        Some(
            RecoverWithPromptIntent(
                lease.Model,
                promptText,
                agent,
                lease.HumanTurnID,
                lease.SessionGeneration,
                lease.CancelGeneration,
                lease.ContinuationID,
                lease.ContinuationOrdinal
            )
        )

let private tryRecoverSession
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        let session = runtime.GetSession sessionID

        match session.PendingLease with
        | Some lease when lease.Status = LeaseStatus.Requested && lease.Owner = SessionOwner.Fallback ->
            match intentFromLease session lease with
            | Some intent ->
                do!
                    runInline runtime executor workspaceRoot sessionID intent
            | None -> ()
        | _ -> ()
    }

/// After event-log state has been restored into FallbackRuntimeStore, scan all
/// sessions for pending leases stuck in LeaseStatus.Requested and run their
/// dispatch effect exactly once.  This path reuses the existing
/// ContinuationIntentExecution runner, so lease validation, generation checks,
/// and terminal handling are preserved; leases already folded to Dispatched or
/// later are ignored and cannot be redispatched.
let recoverRequestedFallbackLeases (scope: RuntimeScope) (workspaceRoot: string) : JS.Promise<unit> =
    promise {
        match scope.TryFindKey("fallbackRuntime"), scope.TryFindKey("fallbackExecutor") with
        | Some rtObj, Some exObj ->
            let runtime = unbox<FallbackRuntimeStore> rtObj
            let executor = unbox<IActionExecutor> exObj
            let sessionIDs = runtime.GetAllSessionIds()

            for sid in sessionIDs do
                do! tryRecoverSession runtime executor workspaceRoot sid
        | _ -> ()
    }
