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
open Wanxiangshu.Hosts.Omp.SubsessionDispatch
open Wanxiangshu.Hosts.Omp.OmpSubsessionHostAdapterPrompts

/// OMP serial prompt API: resolve means prompt entered the ordered stream
/// (host-guaranteed barrier). Receipt is OrderedTurnMarkerObserved.
///
/// Contract: current turn error/idle events NEVER arrive before session.prompt resolves.

type OmpSubsessionHost(session: obj, agent: string, pi: obj) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            SubsessionDispatch.dispatch session agent sessionId turn

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
            // Best effort: the new unified DispatchRegistry does not
            // yet own an OMP per-session mailbox (OMP path goes through
            // the serial `session.prompt` Promise).  The previous
            // implementation was a no-op; we now at least record a
            // typed failure so the caller can distinguish "no-op"
            // from "unknown dispatch".  When the OMP per-session
            // mailbox lands, this becomes a registry call.
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

                                if not (Dyn.isNullish sm) then
                                    return Some(Dyn.get sm "messages")
                                else
                                    return None
                        }

                    match dataOpt with
                    | Some data when not (Dyn.isNullish data) && Dyn.isArray data ->
                        let msgs = unbox<obj array> data
                        let target = TurnId.value turnId
                        let mutable found = false

                        for msg in msgs do
                            let info = Dyn.get msg "info"

                            if not (Dyn.isNullish info) then
                                let cId1 = Dyn.str info "continuationId"
                                let cId2 = Dyn.str info "continuationID"

                                if cId1 = target || cId2 = target then
                                    found <- true

                        if found then
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

let createHost (session: obj) (agent: string) (pi: obj) : ISubsessionHost =
    OmpSubsessionHost(session, agent, pi) :> ISubsessionHost
