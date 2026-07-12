module Wanxiangshu.Shell.NudgeRuntimeTypes

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Nudge.TodoStatus

type NudgeRuntimeState =
    { retryPendingSessions: Set<string>
      forceStoppedSessions: Set<string> }

let emptyRuntimeState =
    { retryPendingSessions = Set.empty
      forceStoppedSessions = Set.empty }

let isNudgePromptText (text: string) : bool =
    let t = text.Trim()

    t.Contains("There are still incomplete todos")
    || t.Contains("You are in loop mode. You must call the submit_review")
    || t.Contains("A background runner task is still active")
    || t.Contains("the system context is about to be suspended")
    || t.Contains("You must immediately force an emergency stop")

let private messageTexts (message: obj) : string list =
    let parts = Dyn.get message "parts"

    if not (Dyn.isArray parts) then
        []
    else
        (parts :?> obj array)
        |> Array.toList
        |> List.choose (fun part ->
            match Dyn.str part "type" with
            | "text" ->
                let t = Dyn.get part "text"
                if Dyn.isNullish t then None else Some(string t)
            | "tool"
            | "dynamic-tool" ->
                let output =
                    let direct = Dyn.get part "output"

                    if not (Dyn.isNullish direct) then
                        string direct
                    else
                        let state = Dyn.get part "state"

                        if Dyn.isNullish state then
                            ""
                        else
                            string (Dyn.get state "output")

                if output = "" then None else Some output
            | _ -> None)

let classifyUserMessage (msg: obj) : string =
    let text = messageTexts msg |> String.concat "\n"

    if isNudgePromptText text then "nudge" else "user"

let tryGetModelStringFromMessage (msg: obj) : string option =
    let info = Dyn.get msg "info"

    if isNull info || Dyn.isNullish info then
        None
    else
        let modelVal = Dyn.get info "model"

        if isNull modelVal || Dyn.isNullish modelVal then
            None
        else if Dyn.typeIs modelVal "string" then
            let s = string modelVal
            if s = "" then None else Some s
        else
            let providerID = Dyn.str modelVal "providerID"
            let modelID = Dyn.str modelVal "modelID"
            let variant = Dyn.str modelVal "variant"
            let suffix = if variant <> "" then ":" + variant else ""

            if providerID = "" || modelID = "" then
                let idVal = Dyn.str modelVal "id"
                if idVal <> "" then Some(idVal + suffix) else None
            else
                Some(sprintf "%s/%s%s" providerID modelID suffix)

let private modelWithVariantString (m: FallbackModel) : string =
    match m.Variant with
    | Some v -> sprintf "%s/%s:%s" m.ProviderID m.ModelID v
    | None -> sprintf "%s/%s" m.ProviderID m.ModelID

let resolveNudgeModel
    (msgs: obj array)
    (fallbackRuntime: FallbackRuntimeState)
    (sessionID: string)
    (lastAssistantModel: string option)
    : string option =
    match fallbackRuntime.GetLatestHumanModel sessionID with
    | Some m -> Some m
    | None ->
        match fallbackRuntime.GetInjectedModel sessionID with
        | Some m -> Some(modelWithVariantString m)
        | None ->
            match fallbackRuntime.GetModel sessionID with
            | Some m -> Some(modelWithVariantString m)
            | None ->
                if isNull msgs || msgs.Length = 0 then
                    lastAssistantModel
                else
                    let lastUserMsgOpt =
                        msgs
                        |> Array.tryFindBack (fun msg ->
                            let role = Dyn.str msg "role"
                            let info = Dyn.get msg "info"
                            let msgRole = if not (Dyn.isNullish info) then Dyn.str info "role" else ""
                            role = "user" || msgRole = "user")

                    match lastUserMsgOpt |> Option.bind tryGetModelStringFromMessage with
                    | Some m -> Some m
                    | None ->
                        let lastAssistantModelFromMsg =
                            msgs
                            |> Array.tryFindBack (fun msg ->
                                let role = Dyn.str msg "role"
                                let info = Dyn.get msg "info"
                                let msgRole = if not (Dyn.isNullish info) then Dyn.str info "role" else ""

                                (role = "assistant" || msgRole = "assistant")
                                && not (isSyntheticAssistantAgent (Dyn.str info "agent")))
                            |> Option.bind tryGetModelStringFromMessage

                        match lastAssistantModelFromMsg with
                        | Some m -> Some m
                        | None -> lastAssistantModel

let finishNudge
    (runtime: FallbackRuntimeState)
    (workspaceRoot: string)
    (sessionKey: string)
    (lease: NudgeLease)
    (outcome: string) // "failed", "cancelled", "dispatched", "settled"
    (errorOrReason: string)
    (actionStr: string)
    (anchor: string)
    : JS.Promise<unit> =
    promise {
        match runtime.TryGetPendingNudgeLease sessionKey with
        | Some nl when nl.NudgeID = lease.NudgeID ->
            if outcome = "failed" then
                do! appendNudgeFailedOrFail workspaceRoot sessionKey lease.NudgeID errorOrReason lease.NudgeOrdinal
            elif outcome = "cancelled" then
                do! appendNudgeCancelledOrFail workspaceRoot sessionKey lease.NudgeID errorOrReason lease.NudgeOrdinal
            elif outcome = "dispatched" then
                do!
                    appendNudgeDispatchedOrFail
                        workspaceRoot
                        sessionKey
                        lease.NudgeID
                        actionStr
                        anchor
                        lease.NudgeOrdinal
            elif outcome = "settled" then
                do! appendNudgeSettledOrFail workspaceRoot sessionKey lease.NudgeID errorOrReason lease.NudgeOrdinal

            if outcome <> "dispatched" then
                if runtime.TryClearPendingNudgeLease(sessionKey, lease.NudgeID) then
                    runtime.ClearActiveNudgeNonce sessionKey

                    if runtime.GetSessionOwner sessionKey = "Nudge" then
                        runtime.SetSessionOwner sessionKey "None"

                    runtime.SetNudgeActive sessionKey false
        | _ -> ()
    }

let runNudgeFlowCore
    (host: Host)
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeState)
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> string option -> string -> string -> JS.Promise<SendOutcome>)
    (abortRun: string -> JS.Promise<unit>)
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
                    let sessionGen = fallbackRuntime.GetSessionGeneration sessionKey
                    let cancelGen = fallbackRuntime.GetCancelGeneration sessionKey
                    let humanTurnId = fallbackRuntime.GetHumanTurnId sessionKey
                    let nudgeOrdinal = fallbackRuntime.IncrementNudgeOrdinal sessionKey
                    let nudgeId = "nudge-" + System.Guid.NewGuid().ToString("N")
                    let nonce = "nudge_" + System.Guid.NewGuid().ToString("N")

                    let! claimed =
                        promise {
                            try
                                return!
                                    tryClaimNudgeDispatch
                                        workspaceRoot
                                        sessionKey
                                        action
                                        snapshot.nudgeAnchorKey
                                        nudgeId
                                        nonce
                                        sessionGen
                                        cancelGen
                                        humanTurnId
                                        nudgeOrdinal
                            with _ ->
                                return false
                        }

                    if not claimed then
                        return runtimeState
                    else
                        let lease: NudgeLease =
                            { NudgeID = nudgeId
                              NudgeOrdinal = nudgeOrdinal
                              Nonce = nonce
                              HumanTurnID = humanTurnId
                              SessionGeneration = sessionGen
                              CancelGeneration = cancelGen
                              Owner = "Nudge"
                              Status = "dispatch_started" }

                        fallbackRuntime.SetPendingNudgeLease(sessionKey, lease)
                        fallbackRuntime.SetSessionOwner sessionKey "Nudge"
                        fallbackRuntime.SetActiveNudgeNonce sessionKey nonce

                        if fallbackRuntime.IsForceStopped sessionKey then
                            do!
                                finishNudge
                                    fallbackRuntime
                                    workspaceRoot
                                    sessionKey
                                    lease
                                    "cancelled"
                                    "Force stopped"
                                    ""
                                    ""

                            return runtimeState

                        else
                            let! outcome =
                                sendNudge promptText snapshot.agentFromMessage snapshot.modelFromMessage nudgeId nonce

                            let currentGen = fallbackRuntime.GetSessionGeneration sessionKey
                            let currentCancelGen = fallbackRuntime.GetCancelGeneration sessionKey
                            let currentTurnId = fallbackRuntime.GetHumanTurnId sessionKey
                            let currentOwner = fallbackRuntime.GetSessionOwner sessionKey

                            let isValid =
                                lease.SessionGeneration = currentGen
                                && lease.HumanTurnID = currentTurnId
                                && lease.CancelGeneration = currentCancelGen
                                && currentOwner = "Nudge"
                                && not (fallbackRuntime.IsForceStopped sessionKey)

                            if not isValid then
                                do! abortRun sessionKey

                                do!
                                    finishNudge
                                        fallbackRuntime
                                        workspaceRoot
                                        sessionKey
                                        lease
                                        "cancelled"
                                        "Cancelled after dispatch"
                                        ""
                                        ""
                            else
                                match outcome with
                                | Delivered ->
                                    if
                                        not (
                                            fallbackRuntime.TryTransitionPendingNudgeLease(
                                                sessionKey,
                                                lease.NudgeID,
                                                "dispatch_started",
                                                "dispatched"
                                            )
                                        )
                                    then
                                        do! abortRun sessionKey

                                        do!
                                            finishNudge
                                                fallbackRuntime
                                                workspaceRoot
                                                sessionKey
                                                lease
                                                "cancelled"
                                                "Cancelled after dispatch"
                                                ""
                                                ""
                                    else
                                        let dispatchedLease = { lease with Status = "dispatched" }

                                        do!
                                            finishNudge
                                                fallbackRuntime
                                                workspaceRoot
                                                sessionKey
                                                dispatchedLease
                                                "dispatched"
                                                ""
                                                (Wanxiangshu.Kernel.Nudge.toString action)
                                                snapshot.nudgeAnchorKey
                                | Busy ->
                                    do!
                                        finishNudge
                                            fallbackRuntime
                                            workspaceRoot
                                            sessionKey
                                            lease
                                            "failed"
                                            "Session busy"
                                            ""
                                            ""
                                | Aborted ->
                                    do!
                                        finishNudge
                                            fallbackRuntime
                                            workspaceRoot
                                            sessionKey
                                            lease
                                            "cancelled"
                                            "Aborted by client"
                                            ""
                                            ""
                                | Failed ->
                                    do!
                                        finishNudge
                                            fallbackRuntime
                                            workspaceRoot
                                            sessionKey
                                            lease
                                            "failed"
                                            "Send failed"
                                            ""
                                            ""

                            return runtimeState
    }

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string
    | StepFailed of workspaceId: string
    | Prompted of workspaceId: string
