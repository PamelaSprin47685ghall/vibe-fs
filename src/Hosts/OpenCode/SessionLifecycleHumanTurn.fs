module Wanxiangshu.Hosts.Opencode.SessionLifecycleHumanTurn

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection
open Wanxiangshu.Runtime.Fallback.FallbackBridgeContinuation
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ToolHookRuntime

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

        match fallbackRuntime.TryGetPendingLease sessionID with
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

        let msgId =
            if messageId = "" then
                "human-" + System.Guid.NewGuid().ToString("N")
            else
                messageId

        let lastMsgId = fallbackRuntime.GetLastHumanMessageId sessionID

        if msgId <> lastMsgId then
            if directory <> "" then
                match fallbackRuntime.TryGetPendingNudgeLease sessionID with
                | Some nudgeLease ->
                    do!
                        appendNudgeCancelledOrFail
                            directory
                            sessionID
                            nudgeLease.NudgeID
                            "new_human_turn"
                            nudgeLease.NudgeOrdinal

                    let _ = fallbackRuntime.ApplyCancelNudgeLease(sessionID, nudgeLease.NudgeID)
                    ()
                | None -> ()

                let activeComp = fallbackRuntime.GetActiveCompactionId sessionID
                let settleInfo = fallbackRuntime.TryGetSettleInfo(sessionID, activeComp)

                match settleInfo with
                | Some(_, ordinal) ->
                    do! appendCompactionSettledOrFail directory sessionID activeComp "cancelled" ordinal
                    let _ = fallbackRuntime.ApplySettle(sessionID, activeComp)
                    ()
                | None -> ()

            fallbackRuntime.SetChain sessionID []
            fallbackRuntime.ClearModel sessionID
            fallbackRuntime.ClearInjected sessionID
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sessionID
            fallbackRuntime.SetSessionOwner sessionID SessionOwner.Human
            let turnId = fallbackRuntime.IncrementHumanTurnId sessionID
            let humanTurnOrdinal = fallbackRuntime.GetHumanTurnOrdinal sessionID
            fallbackRuntime.SetLastHumanMessageId sessionID msgId
            fallbackRuntime.RemoveForceStopped sessionID

            let currentGen = fallbackRuntime.GetSessionGeneration sessionID
            let currentCancelGen = fallbackRuntime.GetCancelGeneration sessionID
            fallbackRuntime.SetActiveContinuationGeneration sessionID currentGen
            fallbackRuntime.SetActiveContinuationCancelGeneration sessionID currentCancelGen

            match modelOpt with
            | Some m -> fallbackRuntime.SetLatestHumanModel sessionID m
            | None -> fallbackRuntime.ClearLatestHumanModel sessionID

            if agent <> "" then
                fallbackRuntime.SetAgentName sessionID agent

            let provider, model, variant =
                match modelOpt with
                | Some m ->
                    match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.decodeModelFromObj (box m) with
                    | Some modelObj ->
                        modelObj.ProviderID, modelObj.ModelID, (modelObj.Variant |> Option.defaultValue "")
                    | None -> "", m, ""
                | None -> "", "", ""

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

            let state = fallbackRuntime.GetOrCreateState sessionID

            let ns =
                { state with
                    Phase = FallbackPhase.Idle
                    ContinueCount = 0
                    FailureCount = 0
                    Lifecycle = FallbackLifecycle.Active
                    RecoveryCount = 0 }

            fallbackRuntime.UpdateState sessionID ns
            fallbackRuntime.SetConsumed sessionID false
            fallbackRuntime.ClearConsumed sessionID
        else
            ()
    }
