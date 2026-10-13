namespace Wanxiangshu.Mission.Lifecycle

open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module LifecycleIntegrationRules =

    /// The one lifecycle vocabulary's integration rule.
    let rule: IntegrationRule =
        { Name = "Lifecycle"
          Initial = box Map.empty<string, LifecycleWork>
          FaultScope = fun envelope -> EventStreamId.value envelope.StreamId
          Accepts = fun envelope -> LifecycleEventTypes.isLifecycleEvent envelope.EventType
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
                        let workId =
                            EventStreamId.value envelope.StreamId
                            |> fun stream -> stream.Replace("lifecycle/", "")

                        match Map.tryFind workId works with
                        | None -> Error "lifecycle event before WorkOpened"
                        | Some work ->
                            LifecycleFold.apply event work
                            |> Result.map (fun next -> box (Map.add workId next works))
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let rules: IntegrationRule list = [ rule ]
