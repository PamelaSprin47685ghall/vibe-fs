module Wanxiangshu.Kernel.Fallback.ContinuationDecision

open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Kernel.Fallback.ContinuationProjection

let private isActiveStatus (status: ContinuationStatus) : bool =
    match status with
    | ContinuationStatus.Committed
    | ContinuationStatus.DispatchClaimed
    | ContinuationStatus.HostMessageAccepted
    | ContinuationStatus.Running -> true
    | _ -> false

let private noChange (projection: ContinuationProjection) : ContinuationDecision =
    { NextProjection = projection
      Events = []
      Effects = [] }

let private withEvents
    (projection: ContinuationProjection)
    (events: ContinuationEvent list)
    (effects: ContinuationEffect list)
    : ContinuationDecision =
    let next = events |> List.fold applyEvent projection

    { NextProjection = next
      Events = events
      Effects = effects }

let private findActiveById (projection: ContinuationProjection) (continuationId: string) : ContinuationState option =
    projection.ActiveBySession
    |> Map.tryPick (fun _ cont ->
        if cont.Request.ContinuationId = continuationId then
            Some cont
        else
            None)

let private dispatchEffectId (req: ContinuationRequest) : string =
    sprintf "continuation:%s:attempt:%d" req.ContinuationId req.Attempt

let private decideRequested (projection: ContinuationProjection) (req: ContinuationRequest) : ContinuationDecision =
    let oldActiveCid =
        projection.ActiveBySession
        |> Map.tryFind req.SessionId
        |> Option.map (fun c -> c.Request.ContinuationId)
        |> Option.filter (fun oldCid -> oldCid <> req.ContinuationId)

    let supersedeEvents =
        oldActiveCid
        |> Option.map (fun oldCid -> [ ContinuationEvent.Superseded(oldCid, "new continuation requested") ])
        |> Option.defaultValue []

    let events = supersedeEvents @ [ ContinuationEvent.Requested req ]
    let effects = [ ContinuationEffect.DispatchContinuation(req, dispatchEffectId req) ]
    withEvents projection events effects

let private decideDispatchClaimed
    (projection: ContinuationProjection)
    (continuationId: string)
    (attempt: int)
    (effectId: string)
    : ContinuationDecision =
    if Set.contains effectId projection.ProcessedEffectIds then
        noChange projection
    else
        match findActiveById projection continuationId with
        | Some cont when cont.Status = ContinuationStatus.Committed ->
            withEvents projection [ ContinuationEvent.DispatchClaimed(continuationId, attempt, effectId) ] []
        | _ -> noChange projection

let private decideHostObserved
    (projection: ContinuationProjection)
    (continuationId: string)
    (userMessageId: string)
    : ContinuationDecision =
    match findActiveById projection continuationId with
    | Some cont when
        cont.Status = ContinuationStatus.DispatchClaimed
        || cont.Status = ContinuationStatus.Committed
        ->
        withEvents
            projection
            [ ContinuationEvent.HostAccepted(continuationId, ContinuationHostIdentity.UserMessageIdentity userMessageId) ]
            []
    | _ -> noChange projection

let private decideRunStarted
    (projection: ContinuationProjection)
    (continuationId: string)
    (runId: string)
    : ContinuationDecision =
    match findActiveById projection continuationId with
    | Some cont when isActiveStatus cont.Status ->
        let acceptEvents =
            match cont.HostIdentity with
            | ContinuationHostIdentity.AwaitingUserMessage ->
                [ ContinuationEvent.HostAccepted(continuationId, ContinuationHostIdentity.RunIdentity runId) ]
            | _ -> []

        let events = acceptEvents @ [ ContinuationEvent.RunStarted continuationId ]
        withEvents projection events []
    | _ -> noChange projection

let private buildAbortEffect
    (projection: ContinuationProjection)
    (cont: ContinuationState)
    (event: ContinuationEvent)
    : ContinuationDecision =
    let effect =
        ContinuationEffect.AbortContinuation(cont.Request, cont.HostIdentity, event)

    withEvents projection [] [ effect ]

let private decideLifecycle (projection: ContinuationProjection) (cmd: ContinuationCommand) : ContinuationDecision =
    match cmd with
    | ContinuationCommand.AssistantMessageObserved(continuationId, assistantMessageId) ->
        match findActiveById projection continuationId with
        | Some cont when isActiveStatus cont.Status ->
            withEvents projection [ ContinuationEvent.AssistantMessageObserved(continuationId, assistantMessageId) ] []
        | _ -> noChange projection
    | ContinuationCommand.Settle(continuationId, reason) ->
        match Map.tryFind continuationId projection.ByContinuationId with
        | Some cont when isActiveStatus cont.Status ->
            withEvents projection [ ContinuationEvent.Settled(continuationId, reason) ] []
        | _ -> noChange projection
    | ContinuationCommand.Fail(continuationId, reason) ->
        match Map.tryFind continuationId projection.ByContinuationId with
        | Some cont when isActiveStatus cont.Status ->
            withEvents projection [ ContinuationEvent.Failed(continuationId, reason) ] []
        | _ -> noChange projection
    | ContinuationCommand.Cancel(continuationId, reason) ->
        match Map.tryFind continuationId projection.ByContinuationId with
        | Some cont when isActiveStatus cont.Status ->
            buildAbortEffect projection cont (ContinuationEvent.Cancelled(continuationId, reason))
        | _ -> noChange projection
    | ContinuationCommand.Supersede(continuationId, reason) ->
        match Map.tryFind continuationId projection.ByContinuationId with
        | Some cont when isActiveStatus cont.Status ->
            buildAbortEffect projection cont (ContinuationEvent.Superseded(continuationId, reason))
        | _ -> noChange projection
    | ContinuationCommand.HostAbortConfirmed(continuationId, terminalEvent) ->
        match Map.tryFind continuationId projection.ByContinuationId with
        | Some cont when isActiveStatus cont.Status -> withEvents projection [ terminalEvent ] []
        | _ -> noChange projection
    | ContinuationCommand.HumanTurnStarted(sessionId, humanTurnId, _messageId) ->
        match Map.tryFind sessionId projection.ActiveBySession with
        | Some cont when isActiveStatus cont.Status && cont.Request.HumanTurnId <> humanTurnId ->
            let terminalEvent =
                if cont.Status = ContinuationStatus.Committed then
                    ContinuationEvent.Cancelled(
                        cont.Request.ContinuationId,
                        sprintf "human turn %s started" humanTurnId
                    )
                else
                    ContinuationEvent.Superseded(
                        cont.Request.ContinuationId,
                        sprintf "human turn %s started" humanTurnId
                    )

            withEvents projection [ terminalEvent ] []
        | _ -> noChange projection
    | _ -> noChange projection

let private decideNoOp (projection: ContinuationProjection) (_cmd: ContinuationCommand) : ContinuationDecision =
    noChange projection

let decide (projection: ContinuationProjection) (cmd: ContinuationCommand) : ContinuationDecision =
    match cmd with
    | ContinuationCommand.Request req -> decideRequested projection req
    | ContinuationCommand.DispatchClaimed(continuationId, attempt, effectId) ->
        decideDispatchClaimed projection continuationId attempt effectId
    | ContinuationCommand.HostUserMessageObserved(continuationId, userMessageId) ->
        decideHostObserved projection continuationId userMessageId
    | ContinuationCommand.RunStarted(continuationId, runId) -> decideRunStarted projection continuationId runId
    | ContinuationCommand.AssistantMessageObserved _
    | ContinuationCommand.Settle _
    | ContinuationCommand.Fail _
    | ContinuationCommand.Cancel _
    | ContinuationCommand.Supersede _
    | ContinuationCommand.HostAbortConfirmed _
    | ContinuationCommand.HumanTurnStarted _ -> decideLifecycle projection cmd
    | ContinuationCommand.Reconcile -> decideNoOp projection cmd
