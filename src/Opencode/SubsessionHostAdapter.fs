module Wanxiangshu.Opencode.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionTranscript
open Wanxiangshu.Shell.SubsessionActorRegistry

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method) (arg))

module PendingTurnReceipt =
    type Waiter =
        { SessionId: string
          Resolve: HostStartReceipt -> unit
          Reject: exn -> unit
          mutable Completed: bool }

    let mutable private pending = Map.empty<string, Waiter>

    let tryFindWaiter (turnId: string) : Waiter option =
        Map.tryFind turnId pending

    let tryFind (turnId: string) : Waiter option = Map.tryFind turnId pending

    let register (sessionId: string) (turnId: string) : JS.Promise<HostStartReceipt> =
        Promise.create (fun resolve reject ->
            let w = { SessionId = sessionId; Resolve = resolve; Reject = reject; Completed = false }
            pending <- Map.add turnId w pending)

    let tryResolve (turnId: string) (receipt: HostStartReceipt) : bool =
        match Map.tryFind turnId pending with
        | Some w ->
            if w.Completed then
                pending <- Map.remove turnId pending
                match SubsessionActorRegistry.TryGet w.SessionId with
                | Some actor ->
                    actor.Post(DispatchAccepted(TurnId.create turnId, receipt)) |> ignore
                | None -> ()
            else
                w.Completed <- true
                pending <- Map.remove turnId pending
                w.Resolve receipt
            true
        | None -> false

    let tryReject (turnId: string) (ex: exn) : unit =
        match Map.tryFind turnId pending with
        | Some w ->
            w.Completed <- true
            w.Reject ex
        | None -> ()

    let cancel (turnId: string) : unit =
        match Map.tryFind turnId pending with
        | Some w ->
            pending <- Map.remove turnId pending
            w.Reject (exn "dispatch cancelled")
        | None -> ()

type OpencodeSubsessionHost(client: obj, agent: string, _directory: string) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            // Parallel wait: fire prompt without blocking receipt.
            promise {
                let model = turn.Model

                let modelStr =
                    match model.Variant with
                    | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                    | None -> sprintf "%s/%s" model.ProviderID model.ModelID

                let nonce = TurnId.value turn.TurnId

                let body =
                    createPromptBodyWithModelAndNonce (Some agent) (Some modelStr) turn.Prompt (Some nonce)

                let arg =
                    box
                        {| path = box {| id = SessionId.value sessionId |}
                           body = body |}

                try
                    match getSessionApiFromClient client with
                    | Ok session ->
                        let promptPromise: JS.Promise<obj> = invoke1 arg "prompt" session
                        let! _ = promptPromise
                        return Ok OrderedTurnMarkerObserved
                    | Error derr ->
                        let msg =
                            match derr with
                            | InvalidIntent(_, _, m) -> m
                            | _ -> "session API missing"

                        return
                            Error(
                                DispatchFailure.HostRejected
                                    { ErrorName = "DispatchFailed"
                                      DomainError = Some derr
                                      Message = msg
                                      StatusCode = None
                                      IsRetryable = Some false }
                            )
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
                match getSessionApiFromClient client with
                | Ok session ->
                    try
                        let! resp =
                            invoke1 (box {| path = box {| id = SessionId.value sessionId |} |}) "messages" session

                        let data = Dyn.get resp "data"

                        if Dyn.isNullish data || not (Dyn.isArray data) then
                            return Unknown
                        else
                            let msgs = unbox<obj array> data
                            let foundOpt =
                                msgs
                                |> Array.tryFind (fun msg ->
                                    let id = Dyn.str msg "id"
                                    let props = Dyn.get msg "props"
                                    let propsNonce = if not (Dyn.isNullish props) then Dyn.str props "nonce" else ""
                                    let info = Dyn.get msg "info"
                                    let infoNonce = if not (Dyn.isNullish info) then Dyn.str info "nonce" else ""
                                    id = nonce || propsNonce = nonce || infoNonce = nonce
                                )
                            match foundOpt with
                            | Some msg ->
                                let msgId = Dyn.str msg "id"
                                return Accepted(UserMessageObserved msgId)
                            | None ->
                                match PendingTurnReceipt.tryFind nonce with
                                | Some w when not w.Completed ->
                                    return StillPending
                                | _ ->
                                    return DefinitelyNotAccepted
                    with _ ->
                        return Unknown
                | Error _ ->
                    return Unknown
            }

let createHost (client: obj) (agent: string) (directory: string) : ISubsessionHost =
    OpencodeSubsessionHost(client, agent, directory) :> ISubsessionHost
