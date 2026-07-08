module Wanxiangshu.Shell.NudgeRuntime

open Fable.Core
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Shell.NudgeRuntimeTypes
open Wanxiangshu.Shell.NudgeRuntimeMux
open Wanxiangshu.Shell.EventLogRuntime

let private _eventLogNudgeIntegral = tryClaimNudgeDispatch

type NudgeRuntimeState = NudgeRuntimeTypes.NudgeRuntimeState
type NudgeRuntimeEvent = NudgeRuntimeTypes.NudgeRuntimeEvent
let emptyRuntimeState = NudgeRuntimeTypes.emptyRuntimeState
let runNudgeFlowCore = NudgeRuntimeTypes.runNudgeFlowCore

type NudgeRuntime(getChatHistory: (string -> JS.Promise<obj array>) option, workspaceDirectory: string) =

    let mutable runtimeState = emptyRuntimeState

    member _.HandleEvent(parsed: NudgeRuntimeEvent, helpers: obj) : JS.Promise<unit> =
        promise {
            match parsed with
            | Ignore -> return ()
            | StreamEnd(workspaceId, stopReason, lastMsg) ->
                if
                    not (Dyn.isNullish helpers)
                    && stopReason <> "queued-message"
                    && (isTerminalAssistantFinish stopReason || stopReason = "tool_use_error")
                then
                    let! newState =
                        runNudgeFlowWithRetryCheck
                            workspaceDirectory
                            runtimeState
                            workspaceId
                            (collectSnapshotMux getChatHistory workspaceDirectory helpers workspaceId lastMsg)
                            (sendNudgeMux helpers workspaceId)

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
    : NudgeRuntime =
    NudgeRuntime(getChatHistory, workspaceDirectory)