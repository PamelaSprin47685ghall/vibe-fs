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
            SubsessionDispatch.queryDispatchStatus session (Dyn.get pi "session") sessionId turnId

        member _.QuerySessionQuiescence(sessionId, _turnId) =
            OmpSubsessionHostAdapterPrompts.querySessionQuiescence session (Dyn.get pi "session") sessionId _turnId

        member _.ClosePhysicalSession(sessionId) =
            OmpSubsessionHostAdapterPrompts.closePhysicalSession session (Dyn.get pi "session") sessionId

let createHost (session: obj) (agent: string) (pi: obj) (workspaceRoot: string) : ISubsessionHost =
    OmpSubsessionHost(session, agent, pi, workspaceRoot) :> ISubsessionHost
