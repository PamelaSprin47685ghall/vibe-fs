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

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

/// Format a FallbackModel option into the string option shape expected by
/// createPromptBodyWithModelAndNonce. None means ModelDirective.DelegateToHost:
/// no model field will be sent to the host, letting OpenCode's own
/// agent.<name>.model static config (or currentModel fallback chain) resolve
/// the model — this is the exact mechanism that stops wanxiangshu's parent-
/// session model injection from overriding opencode.jsonc.
let buildDispatchModelString (model: FallbackModel option) : string option =
    model
    |> Option.map (fun m ->
        match m.Variant with
        | Some v -> sprintf "%s/%s:%s" m.ProviderID m.ModelID v
        | None -> sprintf "%s/%s" m.ProviderID m.ModelID)

module PendingTurnReceipt =
    type TransportState =
        | InFlight
        | RejectedBeforeSend of ErrorInput
        | FailedAfterUnknown of ErrorInput

    type Waiter =
        { SessionId: string
          WorkspaceRoot: string
          Resolve: Result<HostStartReceipt, DispatchFailure> -> unit
          Reject: exn -> unit
          mutable Completed: bool
          mutable TransportState: TransportState }

    let mutable private pending = Map.empty<string, Waiter>

    let tryFind (turnId: string) : Waiter option = Map.tryFind turnId pending

    let register
        (workspaceRoot: string)
        (sessionId: string)
        (turnId: string)
        : JS.Promise<Result<HostStartReceipt, DispatchFailure>> =
        Promise.create (fun resolve reject ->
            let w =
                { SessionId = sessionId
                  WorkspaceRoot = workspaceRoot
                  Resolve = resolve
                  Reject = reject
                  Completed = false
                  TransportState = InFlight }

            pending <- Map.add turnId w pending)

    let tryResolve (turnId: string) (receipt: HostStartReceipt) : bool =
        match Map.tryFind turnId pending with
        | Some w ->
            match w.Completed, w.TransportState with
            | true, RejectedBeforeSend _ ->
                pending <- Map.remove turnId pending
                false
            | true, _ ->
                pending <- Map.remove turnId pending

                match SubsessionActorRegistry.TryGet w.WorkspaceRoot w.SessionId with
                | Some actor -> actor.Post(DispatchAccepted(TurnId.create turnId, receipt)) |> ignore
                | None -> ()

                true
            | false, _ ->
                w.Completed <- true
                pending <- Map.remove turnId pending
                w.Resolve(Ok receipt)
                true
        | None -> false

    let markTransportRejected (turnId: string) (err: ErrorInput) : unit =
        match Map.tryFind turnId pending with
        | Some w ->
            w.TransportState <- RejectedBeforeSend err
            w.Completed <- true
            w.Resolve(Error(HostRejected err))
        | None -> ()

    let markTransportFailed (turnId: string) (err: ErrorInput) : unit =
        match Map.tryFind turnId pending with
        | Some w when not w.Completed ->
            w.TransportState <- FailedAfterUnknown err
            w.Completed <- true
            w.Resolve(Error(HostAcceptanceUnknown err))
        | None -> ()
        | _ -> ()

    let tryGetTransportState (turnId: string) : TransportState option =
        match Map.tryFind turnId pending with
        | Some w -> Some w.TransportState
        | None -> None

    let cancel (turnId: string) : unit = ignore turnId

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

                            let foundOpt =
                                msgs
                                |> Array.tryFind (fun msg ->
                                    let id = Dyn.str msg "id"
                                    let props = Dyn.get msg "props"

                                    let propsNonce =
                                        if not (Dyn.isNullish props) then
                                            Dyn.str props "nonce"
                                        else
                                            ""

                                    let info = Dyn.get msg "info"

                                    let infoNonce =
                                        if not (Dyn.isNullish info) then
                                            Dyn.str info "nonce"
                                        else
                                            ""

                                    id = nonce || propsNonce = nonce || infoNonce = nonce)

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

                            let matchingTurns: bool array =
                                msgs
                                |> Array.map (fun msg ->
                                    let id = Dyn.str msg "id"
                                    let props = Dyn.get msg "props"

                                    let propsNonce =
                                        if not (Dyn.isNullish props) then
                                            Dyn.str props "nonce"
                                        else
                                            ""

                                    let info = Dyn.get msg "info"

                                    let infoNonce =
                                        if not (Dyn.isNullish info) then
                                            Dyn.str info "nonce"
                                        else
                                            ""

                                    if id = nonce || propsNonce = nonce || infoNonce = nonce then
                                        let status = Dyn.str msg "status"

                                        let infoStatus =
                                            if not (Dyn.isNullish info) then
                                                Dyn.str info "status"
                                            else
                                                ""

                                        let propsStatus =
                                            if not (Dyn.isNullish props) then
                                                Dyn.str props "status"
                                            else
                                                ""

                                        let state = Dyn.get msg "state"

                                        let stateStatus =
                                            if not (Dyn.isNullish state) then
                                                if Dyn.typeIs state "string" then
                                                    string state
                                                else
                                                    Dyn.str state "status"
                                            else
                                                ""

                                        let infoState =
                                            if not (Dyn.isNullish info) then
                                                Dyn.get info "state"
                                            else
                                                null

                                        let infoStateStatus =
                                            if not (Dyn.isNullish infoState) then
                                                if Dyn.typeIs infoState "string" then
                                                    string infoState
                                                else
                                                    Dyn.str infoState "status"
                                            else
                                                ""

                                        let checkActive (s: string) =
                                            let ls = s.Trim().ToLower()
                                            ls = "busy" || ls = "running" || ls = "pending"

                                        checkActive status
                                        || checkActive infoStatus
                                        || checkActive propsStatus
                                        || checkActive stateStatus
                                        || checkActive infoStateStatus
                                    else
                                        false)

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
