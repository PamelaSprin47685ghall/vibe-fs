namespace Wanxiangshu.Mission.Planning

open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module PlanIntegrationRules =

    let planRule: IntegrationRule =
        { Name = "Plan"
          Initial = box PlanFold.initialState
          FaultScope = fun envelope -> EventStreamId.value envelope.StreamId
          Accepts = fun envelope -> PlanEventTypes.isPlanEvent envelope.EventType
          Integrate =
            fun current envelope ->
                match PlanEventCodec.tryDecodeEnvelope envelope with
                | Error error -> Error error
                | Ok event ->
                    let currentState = unbox<PlanState> current
                    PlanFold.applyEvent event currentState |> Result.map box
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let lifecycleRule: IntegrationRule =
        { Name = "Lifecycle"
          Initial = box Map.empty<string, LifecycleWork>
          FaultScope = fun envelope -> EventStreamId.value envelope.StreamId
          Accepts = fun envelope -> Wanxiangshu.Mission.Lifecycle.LifecycleEventTypes.isLifecycleEvent envelope.EventType
          Integrate =
            fun current envelope ->
                match LifecycleCodec.decodePayload envelope.EventType envelope.Payload with
                | Error error -> Error error
                | Ok event ->
                    let works = unbox<Map<string, LifecycleWork>> current
                    match event with
                    | LifecycleEvent.WorkOpened(workId, root, sequence, authority) ->
                        if Map.containsKey workId works then
                            Error "second WorkOpened"
                        elif authority = "" then
                            Error "WorkOpened is missing the authority initial"
                        else
                            Ok(box (Map.add workId (LifecycleFold.fresh workId root sequence authority) works))
                    | _ ->
                        let workId = EventStreamId.value envelope.StreamId |> fun stream -> stream.Replace("lifecycle/", "")
                        match Map.tryFind workId works with
                        | None -> Error "lifecycle event before WorkOpened"
                        | Some work ->
                            LifecycleFold.apply event work
                            |> Result.map (fun next -> box (Map.add workId next works))
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let rules: IntegrationRule list = [ planRule; lifecycleRule ]
