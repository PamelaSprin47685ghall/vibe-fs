module Wanxiangshu.Runtime.NudgeFlow

open Fable.Core
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Runtime.NudgeOutcomeHandler
open Wanxiangshu.Runtime.NudgeRuntimeState
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

/// Nudge dispatch is lower priority than fallback and compaction.
/// Terminal fallback leases (Settled/Cancelled) must NOT keep nudging
/// blocked: the projection may lag, and a stale lease should not
/// permanently suppress a legitimate nudge request.
let private nudgeBlockedByFallbackState (runtime: FallbackRuntimeStore) (sessionKey: string) : bool =
    let owner = (runtime.GetSession sessionKey).Owner

    let fallbackOwnerActive =
        owner = SessionOwner.Fallback
        || owner = SessionOwner.Compaction
        || owner = SessionOwner.Nudge

    fallbackOwnerActive || (runtime.GetSession sessionKey).CompactionCompacted

let private dispatchNudge
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionKey: string)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (agentFromMessage: string option)
    (modelFromMessage: string option)
    (promptText: string)
    (sendNudge: string -> string option -> string option -> string -> string -> JS.Promise<SendOutcome>)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        match! tryClaimAndRegisterLease workspaceRoot fallbackRuntime sessionKey action nudgeAnchorKey with
        | None -> ()
        | Some lease ->
            let isLifecycleNotCancelled =
                match fallbackRuntime.TryGetState sessionKey with
                | Some state -> state.Lifecycle <> FallbackLifecycle.Cancelled
                | None -> true

            if
                (fallbackRuntime.GetSession sessionKey).CompactionForceStopped
                || not isLifecycleNotCancelled
            then
                let reason =
                    if (fallbackRuntime.GetSession sessionKey).CompactionForceStopped then
                        "Force stopped"
                    else
                        "Session is not active"

                do! finishNudge fallbackRuntime workspaceRoot sessionKey lease NudgeOutcome.Cancelled reason "" ""
            else
                let! outcome = sendNudge promptText agentFromMessage modelFromMessage lease.NudgeID lease.Nonce

                do!
                    validateAndFinalizeOutcome
                        workspaceRoot
                        fallbackRuntime
                        sessionKey
                        lease
                        action
                        nudgeAnchorKey
                        outcome
                        abortRun
    }

let private dispatchSnapshot
    (host: Host)
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionKey: string)
    (sendNudge: string -> string option -> string option -> string -> string -> JS.Promise<SendOutcome>)
    (abortRun: string -> JS.Promise<unit>)
    (runtimeState: NudgeRuntimeState)
    (snapshot: SessionSnapshot)
    : JS.Promise<NudgeRuntimeState> =
    promise {
        let action = deriveAction snapshot

        match action with
        | NudgeNone -> return runtimeState
        | action ->
            match selectNudgePrompt host action snapshot with
            | None -> return runtimeState
            | Some promptText ->
                do!
                    dispatchNudge
                        workspaceRoot
                        fallbackRuntime
                        sessionKey
                        action
                        snapshot.nudgeAnchorKey
                        snapshot.agentFromMessage
                        snapshot.modelFromMessage
                        promptText
                        sendNudge
                        abortRun

                return runtimeState
    }

let runNudgeFlowCore
    (host: Host)
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> string option -> string -> string -> JS.Promise<SendOutcome>)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<NudgeRuntimeState> =
    promise {
        if nudgeBlockedByFallbackState fallbackRuntime sessionKey then
            return runtimeState
        else
            let snapshotResult =
                try
                    Promise.result (takeSnapshot ())
                with ex ->
                    Promise.lift (Result.Error ex)

            match! snapshotResult with
            | Ok None -> return runtimeState
            | Ok(Some snapshot) ->
                return!
                    dispatchSnapshot
                        host
                        workspaceRoot
                        fallbackRuntime
                        sessionKey
                        sendNudge
                        abortRun
                        runtimeState
                        snapshot
            | Error ex ->
                // Infrastructure failure: keep the runtime state but
                // do NOT pretend the nudge was not needed.  The caller
                // (startNudgeFlow / dispatchPostStopFromHistory) can
                // log this and decide whether to retry.
                return runtimeState
    }
