module Wanxiangshu.Runtime.Fallback.ContinuationCommandProcessor

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Kernel.Fallback.ContinuationProjection
open Wanxiangshu.Kernel.Fallback.ContinuationDecision
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.Fallback.ContinuationEventCodec
open Wanxiangshu.Runtime.PromiseQueue

type ContinuationCommandProcessor
    (workspaceRoot: string, onEffect: ContinuationEffect -> unit, ?initialEvents: WanEvent list) =
    let mutable projection: ContinuationProjection =
        match initialEvents with
        | Some events -> fromWanEvents events
        | None -> emptyProjection

    let queue = SerialQueue()

    let sessionForEvent (evt: ContinuationEvent) : string =
        match evt with
        | ContinuationEvent.Requested req -> req.SessionId
        | ContinuationEvent.DispatchClaimed(continuationId, _, _)
        | ContinuationEvent.HostAccepted(continuationId, _)
        | ContinuationEvent.RunStarted continuationId
        | ContinuationEvent.AssistantMessageObserved(continuationId, _)
        | ContinuationEvent.Settled(continuationId, _)
        | ContinuationEvent.Failed(continuationId, _)
        | ContinuationEvent.Cancelled(continuationId, _)
        | ContinuationEvent.Superseded(continuationId, _) ->
            match Map.tryFind continuationId projection.ByContinuationId with
            | Some cont -> cont.Request.SessionId
            | None -> ""

    let appendEvents (events: ContinuationEvent list) : JS.Promise<unit> =
        promise {
            if List.isEmpty events then
                return ()
            else
                let at = getTimestampMs().ToString()

                let wanEvents =
                    events |> List.map (fun e -> encodeContinuationEvent (sessionForEvent e) at e)

                do! appendEventsAndCacheOrFail workspaceRoot wanEvents
        }

    member _.Post(cmd: ContinuationCommand) : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            promise {
                let decision = decide projection cmd
                projection <- decision.NextProjection
                do! appendEvents decision.Events

                for effect in decision.Effects do
                    onEffect effect
            })

    member _.GetProjection() : ContinuationProjection = projection
