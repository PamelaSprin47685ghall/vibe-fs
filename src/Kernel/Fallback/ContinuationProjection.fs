module Wanxiangshu.Kernel.Fallback.ContinuationProjection

open Wanxiangshu.Kernel.Fallback.ContinuationEventParse
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope

let private isActiveStatus (status: ContinuationStatus) : bool =
    match status with
    | ContinuationStatus.Committed
    | ContinuationStatus.DispatchClaimed
    | ContinuationStatus.HostMessageAccepted
    | ContinuationStatus.Running -> true
    | _ -> false

let private updateById
    (projection: ContinuationProjection)
    (continuationId: string)
    (update: ContinuationState -> ContinuationState)
    : ContinuationProjection =
    let byId =
        projection.ByContinuationId
        |> Map.change continuationId (function
            | Some cont -> Some(update cont)
            | None -> None)

    let sessions =
        projection.ActiveBySession
        |> Map.map (fun _ cont ->
            if cont.Request.ContinuationId = continuationId then
                match Map.tryFind continuationId byId with
                | Some next -> next
                | None -> cont
            else
                cont)

    { projection with
        ActiveBySession = sessions
        ByContinuationId = byId }

let private removeActive (projection: ContinuationProjection) (continuationId: string) : ContinuationProjection =
    { projection with
        ActiveBySession =
            projection.ActiveBySession
            |> Map.filter (fun _ cont -> cont.Request.ContinuationId <> continuationId) }

let private applyRequestedEvent
    (projection: ContinuationProjection)
    (req: ContinuationRequest)
    : ContinuationProjection =
    let cont =
        { Request = req
          Status = ContinuationStatus.Committed
          HostIdentity = ContinuationHostIdentity.AwaitingUserMessage
          HostAssistantMessageId = None
          Failure = None }

    let priorActiveCid =
        projection.ActiveBySession
        |> Map.tryFind req.SessionId
        |> Option.map (fun c -> c.Request.ContinuationId)

    let withSuperseded =
        match priorActiveCid with
        | Some oldCid when oldCid <> req.ContinuationId ->
            updateById projection oldCid (fun c ->
                { c with
                    Status = ContinuationStatus.Superseded })
        | _ -> projection

    { withSuperseded with
        ActiveBySession = Map.add req.SessionId cont withSuperseded.ActiveBySession
        ByContinuationId = Map.add req.ContinuationId cont withSuperseded.ByContinuationId }

let private applyDispatchEvent
    (projection: ContinuationProjection)
    (continuationId: string)
    (f: ContinuationState -> ContinuationState)
    : ContinuationProjection =
    updateById projection continuationId f

let private applyTerminalEvent
    (projection: ContinuationProjection)
    (continuationId: string)
    (finalise: ContinuationState -> ContinuationState)
    : ContinuationProjection =
    let next = updateById projection continuationId finalise
    removeActive next continuationId

let private applySettledEvent
    (projection: ContinuationProjection)
    (continuationId: string)
    (reason: string)
    : ContinuationProjection =
    applyTerminalEvent projection continuationId (fun cont ->
        { cont with
            Status = ContinuationStatus.Settled })

let applyEvent (projection: ContinuationProjection) (evt: ContinuationEvent) : ContinuationProjection =
    match evt with
    | ContinuationEvent.Requested req -> applyRequestedEvent projection req
    | ContinuationEvent.DispatchClaimed(continuationId, attempt, effectId) ->
        let next =
            applyDispatchEvent projection continuationId (fun cont ->
                { cont with
                    Status = ContinuationStatus.DispatchClaimed })

        { next with
            ProcessedEffectIds = Set.add effectId next.ProcessedEffectIds }
    | ContinuationEvent.HostAccepted(continuationId, identity) ->
        applyDispatchEvent projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.HostMessageAccepted
                HostIdentity = identity })
    | ContinuationEvent.RunStarted continuationId ->
        applyDispatchEvent projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.Running })
    | ContinuationEvent.AssistantMessageObserved(continuationId, assistantMessageId) ->
        applyDispatchEvent projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.Running
                HostAssistantMessageId = Some assistantMessageId })
    | ContinuationEvent.Settled(continuationId, reason) -> applySettledEvent projection continuationId reason
    | ContinuationEvent.Failed(continuationId, reason) ->
        applyTerminalEvent projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.Failed
                Failure = Some reason })
    | ContinuationEvent.Cancelled(continuationId, _reason) ->
        applyTerminalEvent projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.Cancelled })
    | ContinuationEvent.Superseded(continuationId, _reason) ->
        applyTerminalEvent projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.Superseded })

let foldEvents (events: ContinuationEvent list) : ContinuationProjection =
    events |> List.fold applyEvent emptyProjection

let fromWanEvents (events: WanEvent list) : ContinuationProjection =
    events |> List.choose tryParseWanEvent |> foldEvents
