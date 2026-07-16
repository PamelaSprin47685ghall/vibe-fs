module Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionTranscript
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Hosts.Opencode.SubsessionDispatch

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

module PendingTurnReceipt = Wanxiangshu.Hosts.Opencode.SubsessionDispatch.PendingTurnReceipt

let buildDispatchModelString =
    Wanxiangshu.Hosts.Opencode.SubsessionDispatch.buildDispatchModelString

type OpencodeSubsessionHost(client: obj, agent: string, _directory: string) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            // Parallel wait: fire prompt without blocking receipt.
            promise {
                let modelStr = buildDispatchModelString turn.Model
                let nonce = TurnId.value turn.TurnId

                let body =
                    createPromptBodyWithModelAndNonce (Some agent) modelStr turn.Prompt (Some nonce)

                let arg =
                    box
                        {| path = box {| id = SessionId.value sessionId |}
                           body = body |}

                let receiptPromise =
                    PendingTurnReceipt.register _directory (SessionId.value sessionId) nonce

                let firePrompt () =
                    promise {
                        try
                            match getSessionApiFromClient client with
                            | Ok session ->
                                try
                                    let! _ = invoke1 arg "prompt" session
                                    ()
                                with ex ->
                                    let errInput =
                                        { ErrorName = "DispatchFailed"
                                          DomainError = None
                                          Message = ex.Message
                                          StatusCode = None
                                          IsRetryable = Some true }

                                    PendingTurnReceipt.markTransportFailed nonce errInput
                            | Error derr ->
                                let msg =
                                    match derr with
                                    | InvalidIntent(_, _, m) -> m
                                    | _ -> "session API missing"

                                let errInput =
                                    { ErrorName = "DispatchFailed"
                                      DomainError = Some derr
                                      Message = msg
                                      StatusCode = None
                                      IsRetryable = Some false }

                                PendingTurnReceipt.markTransportRejected nonce errInput
                        with ex ->
                            let errInput =
                                { ErrorName = "DispatchFailed"
                                  DomainError = None
                                  Message = ex.Message
                                  StatusCode = None
                                  IsRetryable = Some true }

                            PendingTurnReceipt.markTransportFailed nonce errInput
                    }

                let _ = firePrompt ()

                return! receiptPromise
            }

        member _.Abort(sessionId, turnId) =
            promise {
                PendingTurnReceipt.cancel (TurnId.value turnId)

                match getSessionApiFromClient client with
                | Ok session ->
                    try
                        let arg = box {| path = box {| id = SessionId.value sessionId |} |}
                        let! _ = invoke1 arg "abort" session
                        // Abort accepted; wait for SessionIdleObserved after barrier.
                        return RequestAcceptedAwaitIdle
                    with ex ->
                        // Abort call failed — not ConfirmedStopped.
                        return AbortUnavailable
                | Error _ ->
                    // Missing session API is NOT stopped.
                    return AbortUnavailable
            }

        member _.CancelPendingDispatch(turnId) =
            PendingTurnReceipt.cancel (TurnId.value turnId)

        member _.QueryDispatchStatus(sessionId, turnId) =
            promise {
                let nonce = TurnId.value turnId

                let checkWaiterState () =
                    match PendingTurnReceipt.tryGetTransportState nonce with
                    | Some(PendingTurnReceipt.RejectedBeforeSend err) -> TransportRejectedBeforeSend err
                    | Some(PendingTurnReceipt.FailedAfterUnknown err) -> TransportFailedAfterUnknownAcceptance err
                    | Some(PendingTurnReceipt.InFlight) -> StillPending
                    | None -> Unknown

                match getSessionApiFromClient client with
                | Ok session ->
                    try
                        let! resp =
                            invoke1 (box {| path = box {| id = SessionId.value sessionId |} |}) "messages" session

                        let data = Dyn.get resp "data"

                        if Dyn.isNullish data || not (Dyn.isArray data) then
                            return checkWaiterState ()
                        else
                            let msgs = unbox<obj array> data
                            let foundOpt = msgs |> Array.tryFind (isMessageMatch nonce)

                            match foundOpt with
                            | Some msg ->
                                let msgId = Dyn.str msg "id"
                                return Accepted(UserMessageObserved msgId)
                            | None -> return checkWaiterState ()
                    with _ ->
                        return checkWaiterState ()
                | Error _ -> return checkWaiterState ()
            }

        member this.QuerySessionQuiescence(sessionId, turnId) =
            promise {
                let nonce = TurnId.value turnId

                match getSessionApiFromClient client with
                | Ok session ->
                    try
                        let! resp =
                            invoke1 (box {| path = box {| id = SessionId.value sessionId |} |}) "messages" session

                        let data = Dyn.get resp "data"

                        if Dyn.isNullish data || not (Dyn.isArray data) then
                            return StopUnknown
                        else
                            let msgs = unbox<obj array> data

                            let matchingTurns =
                                msgs |> Array.map (fun msg -> isMessageMatch nonce msg && isMessageActive msg)

                            if matchingTurns |> Array.exists (fun active -> active) then
                                return StillRunning
                            elif matchingTurns.Length > 0 then
                                return Stopped
                            else
                                return StopUnknown
                    with _ ->
                        return StopUnknown
                | Error _ -> return StopUnknown
            }

        member _.ClosePhysicalSession(sessionId) =
            promise {
                match getSessionApiFromClient client with
                | Ok session ->
                    try
                        let arg = box {| path = box {| id = SessionId.value sessionId |} |}
                        let! _ = invoke1 arg "delete" session
                        return Stopped
                    with _ ->
                        return StopUnknown
                | Error _ -> return StopUnknown
            }

let createHost (client: obj) (agent: string) (directory: string) : ISubsessionHost =
    OpencodeSubsessionHost(client, agent, directory) :> ISubsessionHost
