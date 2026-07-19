module Wanxiangshu.Hosts.Opencode.SubsessionDispatch

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
open Wanxiangshu.Hosts.OpenCode.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionTranscript
open Wanxiangshu.Runtime.SubsessionActorRegistry

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

let isMessageMatch (nonce: string) (msg: obj) : bool =
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

    let parts = Dyn.get msg "parts"

    let partsNonce =
        if not (Dyn.isNullish parts) && Dyn.isArray parts then
            let arr = unbox<obj array> parts

            arr
            |> Array.tryPick (fun part ->
                let metadata = Dyn.get part "metadata"

                if Dyn.isNullish metadata then
                    None
                else
                    let n = Dyn.str metadata "nonce"

                    if n <> "" then Some n else None)
            |> Option.defaultValue ""
        else
            ""

    id = nonce || propsNonce = nonce || infoNonce = nonce || partsNonce = nonce

let private checkActive (s: string) =
    let ls = s.Trim().ToLower()
    ls = "busy" || ls = "running" || ls = "pending"

let private getNestedStateStatus (stateObj: obj) : string =
    if not (Dyn.isNullish stateObj) then
        if Dyn.typeIs stateObj "string" then
            string stateObj
        else
            Dyn.str stateObj "status"
    else
        ""

let private getInfoStateStatus (info: obj) : string =
    if not (Dyn.isNullish info) then
        let infoState = Dyn.get info "state"
        getNestedStateStatus infoState
    else
        ""

let isMessageActive (msg: obj) : bool =
    let status = Dyn.str msg "status"
    let props = Dyn.get msg "props"
    let info = Dyn.get msg "info"

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
    let stateStatus = getNestedStateStatus state
    let infoStateStatus = getInfoStateStatus info

    checkActive status
    || checkActive infoStatus
    || checkActive propsStatus
    || checkActive stateStatus
    || checkActive infoStateStatus

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
          mutable Cancelled: bool
          mutable TransportState: TransportState }

    let mutable private pending = Map.empty<string, Waiter>

    let tryFind (turnId: string) : Waiter option = Map.tryFind turnId pending

    let register
        (workspaceRoot: string)
        (sessionId: string)
        (turnId: string)
        : JS.Promise<Result<HostStartReceipt, DispatchFailure>> =
        Promise.create (fun resolve reject ->
            let existingForSession =
                pending
                |> Map.toSeq
                |> Seq.tryFind (fun (_, w) -> w.SessionId = sessionId && not w.Completed && not w.Cancelled)

            match existingForSession with
            | Some(_, w) ->
                resolve (
                    Error(
                        HostAcceptanceUnknown
                            { ErrorName = "AnotherDispatchInFlight"
                              DomainError = None
                              Message =
                                $"PendingTurnReceipt: session {sessionId} already has an active dispatch (turn={turnId})"
                              StatusCode = None
                              IsRetryable = Some false }
                    )
                )
            | None ->
                let waiter =
                    { SessionId = sessionId
                      WorkspaceRoot = workspaceRoot
                      Resolve = resolve
                      Reject = reject
                      Completed = false
                      Cancelled = false
                      TransportState = InFlight }

                pending <- Map.add turnId waiter pending)

    let tryResolve (turnId: string) (receipt: HostStartReceipt) : bool =
        match Map.tryFind turnId pending with
        | Some w when w.Cancelled ->
            if not w.Completed then
                w.Completed <- true
                w.Resolve(Ok receipt)

            pending <- Map.remove turnId pending
            true
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

    let cancel (turnId: string) : unit =
        match Map.tryFind turnId pending with
        | Some w when not w.Completed && not w.Cancelled -> w.Cancelled <- true
        | _ -> ()
