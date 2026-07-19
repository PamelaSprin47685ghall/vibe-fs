module Wanxiangshu.Hosts.Opencode.SessionLifecycleHumanTurn

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.NudgeEventWriter
open Wanxiangshu.Runtime.ToolHookRuntime

/// Cancel an in-flight pending lease for the session.
let private finishPendingLease
    (directory: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        match (fallbackRuntime.GetSession sessionID).PendingLease with
        | Some lease ->
            do!
                finishContinuation
                    fallbackRuntime
                    directory
                    sessionID
                    lease
                    ContinuationOutcome.Cancelled
                    "new_human_turn"
        | None -> ()
    }

/// Cancel any active nudge lease and settle any active compaction for the session.
let private cancelNudgeAndCompaction
    (directory: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        match (fallbackRuntime.GetSession sessionID).PendingNudgeLease with
        | Some nudgeLease ->
            do!
                appendNudgeCancelledOrFail
                    directory
                    sessionID
                    nudgeLease.NudgeID
                    "new_human_turn"
                    nudgeLease.NudgeOrdinal

            let applied =
                fallbackRuntime.UpdateSessionReturning(sessionID, applyCancelNudgeLeaseReturning nudgeLease.NudgeID)

            if applied then
                fallbackRuntime.TriggerStateChanged sessionID
        | None -> ()

        let activeComp = (fallbackRuntime.GetSession sessionID).CompactionActiveId
        let settleInfo = tryGetSettleInfo activeComp (fallbackRuntime.GetSession sessionID)

        match settleInfo with
        | Some(_, ordinal) ->
            do! appendCompactionSettledOrFail directory sessionID activeComp "cancelled" ordinal

            let _ =
                fallbackRuntime.UpdateSessionReturning(sessionID, applySettleReturning activeComp)

            ()
        | None -> ()
    }

/// Decode provider/model/variant from an optional model string.
let private decodeModelInfo (modelOpt: string option) : string * string * string =
    match modelOpt with
    | Some m ->
        match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.decodeModelFromObj (box m) with
        | Some modelObj -> modelObj.ProviderID, modelObj.ModelID, (modelObj.Variant |> Option.defaultValue "")
        | None -> "", m, ""
    | None -> "", "", ""

/// Reset core session state for the new human turn and return the turn identifiers.
let private resetSessionState
    (_directory: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionID: string)
    (agent: string)
    (msgId: string)
    (modelOpt: string option)
    =
    Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sessionID

    let applyTurn (s: FallbackSessionRuntime) =
        let s' = beginHumanTurn msgId s

        let s'' =
            match modelOpt with
            | Some m -> recordLatestHumanModel m s'
            | None -> clearLatestHumanModel s'

        if agent <> "" then recordAgentName agent s'' else s''

    fallbackRuntime.Update(sessionID, applyTurn)

    let s = fallbackRuntime.GetSession sessionID
    s.HumanTurnId, s.HumanTurnOrdinal

/// Initialise the FallbackPhase.Idle state and consumed flags for the session.
let private initializeFallbackState (fallbackRuntime: FallbackRuntimeStore) (sessionID: string) : unit =
    let state = fallbackRuntime.GetOrCreateState sessionID

    let ns =
        { state with
            Phase = FallbackPhase.Idle
            ContinueCount = 0
            FailureCount = 0
            Lifecycle = FallbackLifecycle.Active
            RecoveryCount = 0 }

    fallbackRuntime.Update(sessionID, setCore ns)
    fallbackRuntime.Update(sessionID, recordConsumed false)
    fallbackRuntime.Update(sessionID, clearConsumption)

/// Reset session runtime for a new human turn (or no-op on duplicate message id).
let onNewHumanMessage
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionID: string)
    (agent: string)
    (modelOpt: string option)
    (messageId: string)
    : JS.Promise<unit> =
    promise {
        let directory = if isNullish ctx then "" else pluginDirectoryFromCtx ctx

        let msgId =
            if messageId = "" then
                "human-" + System.Guid.NewGuid().ToString("N")
            else
                messageId

        let lastMsgId = (fallbackRuntime.GetSession sessionID).LastHumanMessageId

        // Dedup before any side-effect that could cancel an active lease.
        // A duplicate chat.message hook must not reset fallback/nudge state.
        if msgId <> lastMsgId then
            do! finishPendingLease directory fallbackRuntime sessionID

            if directory <> "" then
                do! cancelNudgeAndCompaction directory fallbackRuntime sessionID

            let provider, model, variant = decodeModelInfo modelOpt

            let turnId, humanTurnOrdinal =
                resetSessionState directory fallbackRuntime sessionID agent msgId modelOpt

            if directory <> "" then
                do!
                    appendHumanTurnStartedOrFail
                        directory
                        sessionID
                        turnId
                        provider
                        model
                        variant
                        agent
                        humanTurnOrdinal
                        msgId

            initializeFallbackState fallbackRuntime sessionID
        else
            ()
    }
