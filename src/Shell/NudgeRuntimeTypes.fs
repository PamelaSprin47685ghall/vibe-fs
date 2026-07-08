module Wanxiangshu.Shell.NudgeRuntimeTypes

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.EventLogRuntime

type NudgeRuntimeState =
    { retryPendingSessions: Set<string>
      forceStoppedSessions: Set<string> }

let emptyRuntimeState =
    { retryPendingSessions = Set.empty
      forceStoppedSessions = Set.empty }

let runNudgeFlowCore
    (host: Host)
    (workspaceRoot: string)
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> string option -> JS.Promise<SendOutcome>)
    : JS.Promise<NudgeRuntimeState> =
    promise {
        match! takeSnapshot () with
        | None -> return runtimeState
        | Some snapshot ->
            match deriveAction snapshot with
            | NudgeNone -> return runtimeState
            | action ->
                match selectNudgePrompt host action snapshot with
                | None -> return runtimeState
                | Some promptText ->
                    let! claimed =
                        promise {
                            try
                                return! tryClaimNudgeDispatch workspaceRoot sessionKey action snapshot.nudgeAnchorKey
                            with _ ->
                                return false
                        }

                    if not claimed then
                        return runtimeState
                    else
                        let! _ = sendNudge promptText snapshot.agentFromMessage snapshot.modelFromMessage
                        return runtimeState
    }

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string
    | StepFailed of workspaceId: string
    | Prompted of workspaceId: string