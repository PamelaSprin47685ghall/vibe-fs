module Wanxiangshu.Shell.NudgeRuntime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel
open Wanxiangshu.Shell.NudgeRuntimeTypes
open Wanxiangshu.Shell.NudgeRuntimeMux
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.FallbackKernel.Types

let private _eventLogNudgeIntegral = tryClaimNudgeDispatch

type NudgeRuntimeState = NudgeRuntimeTypes.NudgeRuntimeState
type NudgeRuntimeEvent = NudgeRuntimeTypes.NudgeRuntimeEvent
let emptyRuntimeState = NudgeRuntimeTypes.emptyRuntimeState
let runNudgeFlowCore = NudgeRuntimeTypes.runNudgeFlowCore

type NudgeRuntime
    (
        getChatHistory: (string -> JS.Promise<obj array>) option,
        workspaceDirectory: string,
        fallbackRuntime: FallbackRuntimeState
    ) =

    let mutable runtimeState = emptyRuntimeState

    member _.HandleEvent(parsed: NudgeRuntimeEvent, helpers: obj) : JS.Promise<unit> =
        promise {
            match parsed with
            | Ignore -> return ()
            | StreamEnd(workspaceId, stopReason, lastMsg) ->
                let reason = FinishReason.fromString stopReason

                if
                    not (Dyn.isNullish helpers)
                    && reason <> FinishReason.QueuedMessage
                    && (isTerminalAssistantFinish stopReason || reason = FinishReason.ToolUseError)
                then
                    if fallbackRuntime.GetSessionOwner workspaceId = "Nudge" then
                        match fallbackRuntime.TryGetPendingNudgeLease workspaceId with
                        | Some lease ->
                            do!
                                finishNudge
                                    fallbackRuntime
                                    workspaceDirectory
                                    workspaceId
                                    lease
                                    "settled"
                                    "completed"
                                    ""
                                    ""
                        | None -> fallbackRuntime.SetSessionOwner workspaceId "None"

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
    (fallbackRuntime: FallbackRuntimeState)
    : NudgeRuntime =
    NudgeRuntime(getChatHistory, workspaceDirectory, fallbackRuntime)
