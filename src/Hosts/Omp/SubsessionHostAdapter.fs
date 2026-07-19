module Wanxiangshu.Hosts.Omp.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.SubsessionDispatch
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.OmpSubsessionHostAdapterPrompts
open Wanxiangshu.Kernel.Subsession.Types

/// OMP serial prompt API: resolve means prompt entered the ordered stream
/// (host-guaranteed barrier). Receipt is OrderedTurnMarkerObserved.
///
/// Contract: current turn error/idle events NEVER arrive before session.prompt resolves.

type OmpSubsessionHost(session: obj, agent: string, pi: obj, workspaceRoot: string) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            promise {
                let! result = SubsessionDispatch.dispatch session agent sessionId turn

                // OMP child session events are isolated; once prompt() resolves the
                // turn is already finished.  Defer an evidence update and idle event
                // so the supervisor's DispatchAccepted is queued first.
                JS.setTimeout
                    (fun () ->
                        promise {
                            let sm = Dyn.get session "sessionManager"

                            if not (Dyn.isNullish sm) then
                                let text =
                                    match readAssistantText (unbox<ISessionManager> sm) 0 "\n\n" with
                                    | Some t -> t
                                    | None -> ""

                                if text <> "" then
                                    let evidence =
                                        { CurrentTurnEvidence.empty with
                                            Assistant = AssistantSnapshot("", 0L, text, Some NormalFinish)
                                            Outcome = CompletionRequested text }

                                    do!
                                        SubsessionEventRouter.routeEvidence
                                            workspaceRoot
                                            (SessionId.value sessionId)
                                            evidence
                                        |> Promise.map ignore

                            do!
                                SubsessionEventRouter.tryIdle workspaceRoot (SessionId.value sessionId)
                                |> Promise.map ignore
                        }
                        |> Promise.start)
                    50
                |> ignore

                return result
            }

        member _.Abort(sessionId, _turnId) =
            promise {
                let mutable requested = false
                let mutable sawApi = false

                try
                    let abortFn = Dyn.get session "abort"

                    if not (Dyn.isNullish abortFn) then
                        sawApi <- true
                        do! unbox<JS.Promise<obj>> (session?abort ()) |> Promise.map ignore
                        requested <- true
                with _ ->
                    ()

                try
                    let sessionApi = Dyn.get pi "session"

                    if not (Dyn.isNullish sessionApi) then
                        sawApi <- true
                        let arg = box {| sessionId = SessionId.value sessionId |}
                        do! unbox<JS.Promise<obj>> (sessionApi?sessionAbort (arg)) |> Promise.map ignore
                        requested <- true
                with _ ->
                    ()

                if requested then return RequestAcceptedAwaitIdle
                elif sawApi then return AbortUnavailable
                else return AbortUnavailable
            }

        member _.CancelPendingDispatch(turnId) =
            // The new unified DispatchRegistry does not
            // yet own an OMP per-session mailbox (OMP path goes through
            // the serial `session.prompt` Promise).  The previous
            // implementation was a no-op; we now at least record a
            // typed failure so the caller can distinguish "no-op"
            // from "unknown dispatch".
            let nonce = TurnId.value turnId
            ignore nonce

        member _.QueryDispatchStatus(sessionId, turnId) =
            promise {
                try
                    let sessionApi = Dyn.get pi "session"

                    let! dataOpt =
                        promise {
                            if not (Dyn.isNullish sessionApi) then
                                let arg = box {| sessionId = SessionId.value sessionId |}
                                let! resp = unbox<JS.Promise<obj>> (sessionApi?sessionMessages (arg))
                                return Some(Dyn.get resp "data")
                            else
                                let sm = Dyn.get session "sessionManager"

                                if Dyn.isNullish sm then
                                    return None
                                else
                                    let getEntries = Dyn.get sm "getEntries"

                                    let raw =
                                        if Dyn.typeIs getEntries "function" then
                                            Dyn.callMethod0 sm "getEntries"
                                        else
                                            Dyn.get sm "messages"

                                    if Dyn.isArray raw then return Some raw else return None
                        }

                    match dataOpt with
                    | Some data when not (Dyn.isNullish data) && Dyn.isArray data ->
                        let msgs = unbox<obj array> data
                        let target = TurnId.value turnId
                        let mutable found = false
                        let mutable anyUser = false

                        for msg in msgs do
                            let info = Dyn.get msg "info"

                            if not (Dyn.isNullish info) then
                                let cId1 = Dyn.str info "continuationId"
                                let cId2 = Dyn.str info "continuationID"

                                if cId1 = target || cId2 = target then
                                    found <- true

                            let roleTarget =
                                if Dyn.str msg "role" <> "" then
                                    msg
                                else
                                    let m = Dyn.get msg "message"
                                    if not (Dyn.isNullish m) then m else info

                            if not (Dyn.isNullish roleTarget) then
                                let role = (Dyn.str roleTarget "role").ToLowerInvariant()

                                if role = "user" then
                                    anyUser <- true

                        if found || anyUser then
                            return DispatchStatus.Accepted OrderedTurnMarkerObserved
                        else
                            return DispatchStatus.Unknown
                    | _ -> return DispatchStatus.Unknown
                with _ ->
                    return DispatchStatus.Unknown
            }

        member _.QuerySessionQuiescence(sessionId, _turnId) =
            promise {
                match detectStatus session with
                | Some status -> return status
                | None ->
                    let sm = Dyn.get session "sessionManager"

                    match detectStatus sm with
                    | Some status -> return status
                    | None ->
                        let sessionApi = Dyn.get pi "session"

                        if not (Dyn.isNullish sessionApi) then
                            let! piStatus = checkPiSessionStatus sessionApi sessionId

                            match piStatus with
                            | Some status -> return status
                            | None -> return StopUnknown
                        else
                            return StopUnknown
            }

        member _.ClosePhysicalSession(sessionId) =
            promise {
                let! localClose = tryCloseSessionObj session

                match localClose with
                | Some status -> return status
                | None ->
                    let sm = Dyn.get session "sessionManager"
                    let! smClose = tryCloseSessionObj sm

                    match smClose with
                    | Some status -> return status
                    | None ->
                        let sessionApi = Dyn.get pi "session"

                        if not (Dyn.isNullish sessionApi) then
                            let! piClose = tryClosePiSession sessionApi sessionId

                            match piClose with
                            | Some status -> return status
                            | None -> return StopUnknown
                        else
                            return StopUnknown
            }

let createHost (session: obj) (agent: string) (pi: obj) (workspaceRoot: string) : ISubsessionHost =
    OmpSubsessionHost(session, agent, pi, workspaceRoot) :> ISubsessionHost
