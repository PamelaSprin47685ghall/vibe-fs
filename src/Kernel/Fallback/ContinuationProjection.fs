module Wanxiangshu.Kernel.Fallback.ContinuationProjection

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Kernel.FallbackKernel.Types

let private parseInt (s: string) : int option =
    match System.Int32.TryParse s with
    | true, v -> Some v
    | _ -> None

let private parseModelString (s: string) : FallbackModel option =
    if s = "" then
        None
    else
        let colon = s.IndexOf(':')

        let basePart, variantOpt =
            if colon >= 0 then
                s.[0 .. colon - 1].Trim(),
                (if colon < s.Length - 1 then
                     Some(s.[colon + 1 ..].Trim())
                 else
                     None)
            else
                s.Trim(), None

        let slash = basePart.IndexOf('/')

        if slash <= 0 || slash >= basePart.Length - 1 then
            None
        else
            Some
                { ProviderID = basePart.[0 .. slash - 1].Trim()
                  ModelID = basePart.[slash + 1 ..].Trim()
                  Variant = variantOpt
                  Temperature = None
                  TopP = None
                  MaxTokens = None
                  ReasoningEffort = None
                  Thinking = false }

let private parseModelFromPayload (payload: Map<string, string>) : FallbackModel option =
    let provider = Map.tryFind "provider" payload |> Option.defaultValue ""
    let modelId = Map.tryFind "modelId" payload |> Option.defaultValue ""
    let variant = Map.tryFind "variant" payload |> Option.filter (fun s -> s <> "")

    if provider <> "" && modelId <> "" then
        Some
            { ProviderID = provider
              ModelID = modelId
              Variant = variant
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }
    else
        Map.tryFind "model" payload |> Option.bind parseModelString

let private tryParseRequested (e: WanEvent) : ContinuationEvent option =
    let p = e.Payload

    match Map.tryFind "continuationId" p, parseModelFromPayload p with
    | Some cid, Some model ->
        let req =
            { ContinuationId = cid
              ContinuationOrdinal =
                Map.tryFind "continuationOrdinal" p
                |> Option.bind parseInt
                |> Option.defaultValue 0
              Attempt = Map.tryFind "attempt" p |> Option.bind parseInt |> Option.defaultValue 1
              SessionId = e.Session
              HumanTurnId = Map.tryFind "humanTurnId" p |> Option.defaultValue ""
              SourceHumanMessageId = Map.tryFind "sourceHumanMessageId" p
              ContextGeneration =
                Map.tryFind "contextGeneration" p
                |> Option.bind parseInt
                |> Option.defaultValue 0
              CancelGeneration =
                Map.tryFind "cancelGeneration" p
                |> Option.bind parseInt
                |> Option.defaultValue 0
              Model = model
              Agent = Map.tryFind "agent" p |> Option.defaultValue ""
              Mode =
                Map.tryFind "mode" p
                |> Option.bind (fun m ->
                    if m = "ResumeInterruptedTurn" then
                        Some ContinuationMode.ResumeInterruptedTurn
                    else
                        Map.tryFind "prompt" p
                        |> Option.map (fun prompt -> ContinuationMode.RecoverToolCallText prompt))
                |> Option.defaultValue ContinuationMode.ResumeInterruptedTurn }

        Some(ContinuationEvent.Requested req)
    | _ -> None

let private hostIdentityFromPayload (p: Map<string, string>) : ContinuationHostIdentity =
    match Map.tryFind "userMessageId" p with
    | Some uid when uid <> "" -> ContinuationHostIdentity.UserMessageIdentity uid
    | _ ->
        match Map.tryFind "runId" p with
        | Some rid when rid <> "" -> ContinuationHostIdentity.RunIdentity rid
        | _ ->
            match Map.tryFind "receiptId" p with
            | Some rid when rid <> "" -> ContinuationHostIdentity.OpaqueIdentity rid
            | _ -> ContinuationHostIdentity.AwaitingUserMessage

let private parseContinuationId (p: Map<string, string>) : string option =
    Map.tryFind "continuationId" p |> Option.filter (fun s -> s <> "")

let private tryParseKind (kind: string) (e: WanEvent) : ContinuationEvent option =
    let p = e.Payload

    parseContinuationId p
    |> Option.bind (fun cid ->
        if kind = eventKindContinuationRequested then
            tryParseRequested e
        elif
            kind = eventKindContinuationDispatchStarted
            || kind = eventKindContinuationDispatchClaimed
        then
            let effectId =
                Map.tryFind "effectId" p
                |> Option.defaultValue (sprintf "legacy:%s:dispatch" cid)

            Some(
                ContinuationEvent.DispatchClaimed(
                    cid,
                    Map.tryFind "attempt" p |> Option.bind parseInt |> Option.defaultValue 1,
                    effectId
                )
            )
        elif
            kind = eventKindContinuationDispatched
            || kind = eventKindContinuationHostAccepted
        then
            Some(ContinuationEvent.HostAccepted(cid, hostIdentityFromPayload p))
        elif kind = eventKindContinuationRunStarted then
            Some(ContinuationEvent.RunStarted cid)
        elif kind = eventKindContinuationAssistantObserved then
            Map.tryFind "assistantMessageId" p
            |> Option.map (fun aid -> ContinuationEvent.AssistantMessageObserved(cid, aid))
        elif kind = eventKindContinuationSettled then
            Some(ContinuationEvent.Settled(cid, Map.tryFind "status" p |> Option.defaultValue "completed"))
        elif kind = eventKindContinuationFailed then
            Some(ContinuationEvent.Failed(cid, Map.tryFind "error" p |> Option.defaultValue ""))
        elif kind = eventKindContinuationCancelled then
            Some(ContinuationEvent.Cancelled(cid, Map.tryFind "reason" p |> Option.defaultValue ""))
        elif kind = eventKindContinuationSuperseded then
            Some(ContinuationEvent.Superseded(cid, Map.tryFind "reason" p |> Option.defaultValue ""))
        else
            None)

let tryParseWanEvent (e: WanEvent) : ContinuationEvent option = tryParseKind e.Kind e

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

let applyEvent (projection: ContinuationProjection) (evt: ContinuationEvent) : ContinuationProjection =
    match evt with
    | ContinuationEvent.Requested req ->
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

    | ContinuationEvent.DispatchClaimed(continuationId, attempt, effectId) ->
        let next =
            updateById projection continuationId (fun cont ->
                { cont with
                    Status = ContinuationStatus.DispatchClaimed })

        { next with
            ProcessedEffectIds = Set.add effectId next.ProcessedEffectIds }

    | ContinuationEvent.HostAccepted(continuationId, identity) ->
        updateById projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.HostMessageAccepted
                HostIdentity = identity })

    | ContinuationEvent.RunStarted continuationId ->
        updateById projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.Running })

    | ContinuationEvent.AssistantMessageObserved(continuationId, assistantMessageId) ->
        updateById projection continuationId (fun cont ->
            { cont with
                Status = ContinuationStatus.Running
                HostAssistantMessageId = Some assistantMessageId })

    | ContinuationEvent.Settled(continuationId, _reason) ->
        let next =
            updateById projection continuationId (fun cont ->
                { cont with
                    Status = ContinuationStatus.Settled })

        removeActive next continuationId

    | ContinuationEvent.Failed(continuationId, reason) ->
        let next =
            updateById projection continuationId (fun cont ->
                { cont with
                    Status = ContinuationStatus.Failed
                    Failure = Some reason })

        removeActive next continuationId

    | ContinuationEvent.Cancelled(continuationId, _reason) ->
        let next =
            updateById projection continuationId (fun cont ->
                { cont with
                    Status = ContinuationStatus.Cancelled })

        removeActive next continuationId

    | ContinuationEvent.Superseded(continuationId, _reason) ->
        let next =
            updateById projection continuationId (fun cont ->
                { cont with
                    Status = ContinuationStatus.Superseded })

        removeActive next continuationId

let foldEvents (events: ContinuationEvent list) : ContinuationProjection =
    events |> List.fold applyEvent emptyProjection

let fromWanEvents (events: WanEvent list) : ContinuationProjection =
    events |> List.choose tryParseWanEvent |> foldEvents
