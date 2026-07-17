module Wanxiangshu.Runtime.NudgeRuntime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.NudgeRuntimeMux
open Wanxiangshu.Runtime.NudgeRuntimeState
open Wanxiangshu.Runtime.NudgeRuntimeEvent
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Kernel.FallbackKernel.Types

let private _eventLogNudgeIntegral = tryClaimNudgeDispatch

type NudgeRuntime
    (
        getChatHistory: (string -> JS.Promise<obj array>) option,
        workspaceDirectory: string,
        fallbackRuntime: FallbackRuntimeStore,
        isReviewLoopActive: string -> bool
    ) =

    let mutable runtimeState = emptyRuntimeState

    member _.HandleEvent(parsed: NudgeRuntimeEvent, helpers: obj) : JS.Promise<unit> =
        promise {
            match parsed with
            | Ignore -> return ()
            | StreamEnd(workspaceId, stopReason, lastMsg) ->
                let reason = FinishReason.fromString stopReason
                let isNudgeOwner = fallbackRuntime.GetSessionOwner workspaceId = SessionOwner.Nudge

                if
                    not (Dyn.isNullish helpers)
                    && reason <> FinishReason.QueuedMessage
                    && (isTerminalAssistantFinish stopReason || reason = FinishReason.ToolUseError)
                then
                    if isNudgeOwner then
                        match fallbackRuntime.TryGetPendingNudgeLease workspaceId with
                        | Some lease ->
                            do!
                                finishNudge
                                    fallbackRuntime
                                    workspaceDirectory
                                    workspaceId
                                    lease
                                    NudgeOutcome.Settled
                                    "completed"
                                    ""
                                    ""
                        | None -> fallbackRuntime.SetSessionOwner workspaceId SessionOwner.NoOwner

                    if not isNudgeOwner || isReviewLoopActive workspaceId then
                        let! newState =
                            runNudgeFlowWithRetryCheck
                                fallbackRuntime
                                workspaceDirectory
                                runtimeState
                                workspaceId
                                (collectSnapshotMux
                                    fallbackRuntime
                                    getChatHistory
                                    workspaceDirectory
                                    helpers
                                    workspaceId
                                    lastMsg)
                                (sendNudgeMux fallbackRuntime helpers workspaceId)

                        runtimeState <- newState

                return ()
            | StreamAbort workspaceId
            | AbortedError workspaceId ->
                runtimeState <-
                    { runtimeState with
                        forceStoppedSessions = Set.add workspaceId runtimeState.forceStoppedSessions }

                return ()
            | StepFailed workspaceId ->
                runtimeState <-
                    { runtimeState with
                        retryPendingSessions = Set.add workspaceId runtimeState.retryPendingSessions }

                return ()
            | Prompted workspaceId ->
                runtimeState <-
                    { runtimeState with
                        retryPendingSessions = Set.remove workspaceId runtimeState.retryPendingSessions
                        forceStoppedSessions = Set.remove workspaceId runtimeState.forceStoppedSessions }

                return ()
        }

let createNudgeRuntime
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (workspaceDirectory: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (isReviewLoopActive: string -> bool)
    : NudgeRuntime =
    NudgeRuntime(getChatHistory, workspaceDirectory, fallbackRuntime, isReviewLoopActive)
