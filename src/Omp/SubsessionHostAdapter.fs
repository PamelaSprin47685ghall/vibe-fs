module Wanxiangshu.Omp.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SubsessionActor

/// OMP serial prompt API: resolve means prompt entered the ordered stream
/// (host-guaranteed barrier). Receipt is OrderedTurnMarkerObserved.
///
/// Contract: current turn error/idle events NEVER arrive before session.prompt resolves.
type OmpSubsessionHost(session: obj, agent: string, pi: obj) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            promise {
                let model = turn.Model

                let modelStr =
                    match model.Variant with
                    | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                    | None -> sprintf "%s/%s" model.ProviderID model.ModelID

                let turnIdStr = TurnId.value turn.TurnId

                let pObj =
                    let p =
                        {| text = turn.Prompt
                           model = modelStr
                           continuationID = turnIdStr |}

                    if agent <> "" then Dyn.withKey p "agent" agent else box p

                let body = box {| prompt = pObj |}

                let arg =
                    box
                        {| sessionId = SessionId.value sessionId
                           body = body |}

                try
                    let! resp = unbox<JS.Promise<obj>> (session?prompt (arg))

                    let msgId =
                        let id1 = Dyn.str resp "id"
                        let id2 = Dyn.str (Dyn.get resp "data") "id"

                        if id1 <> "" then id1
                        elif id2 <> "" then id2
                        else ""

                    if msgId <> "" then
                        return Ok(UserMessageObserved msgId)
                    else
                        return Ok OrderedTurnMarkerObserved
                with ex ->
                    return
                        Error(
                            DispatchFailure.HostAcceptanceUnknown
                                { ErrorName = "DispatchFailed"
                                  DomainError = None
                                  Message = ex.Message
                                  StatusCode = None
                                  IsRetryable = Some true }
                        )
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

                if requested then
                    return RequestAcceptedAwaitIdle
                elif sawApi then
                    // API existed but call failed
                    return AbortUnavailable
                else
                    // No abort API — NEVER ConfirmedStopped.
                    return AbortUnavailable
            }

        member _.CancelPendingDispatch(_turnId) = ()

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
                            return DispatchStatus.DefinitelyNotAccepted
                    | _ -> return DispatchStatus.Unknown
                with _ ->
                    return DispatchStatus.Unknown
            }

let createHost (session: obj) (agent: string) (pi: obj) : ISubsessionHost =
    OmpSubsessionHost(session, agent, pi) :> ISubsessionHost
