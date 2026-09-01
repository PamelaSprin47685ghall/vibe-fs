namespace Wanxiangshu.Execution.Fission

open System
open System.Collections.Generic
open Wanxiangshu.Foundation.Identity

[<CLIMutable>]
type FissionLaneBinding =
    { GroupId: string
      OwnerSessionId: SessionId
      LaneIndex: int
      LaneCount: int }

/// Process-local resource indexes for Fission. Durable truth lives in
/// Journal.FissionProjection; these indexes are disposable accelerators / abort
/// causes and may be rebuilt after restart.
module FissionRuntime =

    let private gate = obj ()
    // DSL-MUTABLE: resource — lane binding registry by session id
    let private lanes = Dictionary<string, FissionLaneBinding>()
    // DSL-MUTABLE: resource — silent interrupt owner set
    let private silentInterrupts = HashSet<string>()
    // DSL-MUTABLE: resource — handle affinity map by owner+handle key
    let private handleAffinities = Dictionary<string, int>()

    // DSL-MUTABLE: resource — child observer callback registry by group id
    let private childObservers =
        Dictionary<string, int -> string -> SessionId -> unit>()

    // DSL-MUTABLE: resource — group disposable resource registry
    let private groupResources = Dictionary<string, ResizeArray<IDisposable>>()
    // DSL-MUTABLE: single-flight — delivery claim latch per group+completion+lane
    let private deliveryClaims = HashSet<string>()
    // DSL-MUTABLE: single-flight — takeover claim latch per group
    let private takeoverClaims = HashSet<string>()

    let private handleKey ownerSessionId handleId =
        SessionId.value ownerSessionId + "\u001f" + handleId

    let private deliveryKey groupId completionId (laneIndex: int) =
        groupId + "\u001f" + completionId + "\u001f" + string laneIndex

    let private disposeSafely (resource: IDisposable) =
        try
            resource.Dispose()
        with _ ->
            ()

    let private disposeGroupResources
        (groupResources: Dictionary<string, ResizeArray<IDisposable>>)
        (groupId: string)
        : unit =
        match groupResources.TryGetValue groupId with
        | true, resources ->
            resources |> Seq.iter disposeSafely
            groupResources.Remove groupId |> ignore
        | false, _ -> ()

    let bindLane groupId ownerSessionId laneIndex laneCount laneSessionId =
        let binding =
            { GroupId = groupId
              OwnerSessionId = ownerSessionId
              LaneIndex = laneIndex
              LaneCount = laneCount }

        lock gate (fun () -> lanes.[SessionId.value laneSessionId] <- binding)

    let unbindLane laneSessionId =
        lock gate (fun () -> lanes.Remove(SessionId.value laneSessionId) |> ignore)

    let tryLane laneSessionId =
        lock gate (fun () ->
            match lanes.TryGetValue(SessionId.value laneSessionId) with
            | true, binding -> Some binding
            | false, _ -> None)

    let tryOwner laneSessionId =
        tryLane laneSessionId |> Option.map (fun binding -> binding.OwnerSessionId)

    let logicalOwner sessionId =
        tryOwner sessionId |> Option.defaultValue sessionId

    let markSilentInterrupt ownerSessionId =
        lock gate (fun () -> silentInterrupts.Add(SessionId.value ownerSessionId) |> ignore)

    let clearSilentInterrupt ownerSessionId =
        lock gate (fun () -> silentInterrupts.Remove(SessionId.value ownerSessionId) |> ignore)

    let isSilentInterrupt ownerSessionId =
        lock gate (fun () -> silentInterrupts.Contains(SessionId.value ownerSessionId))

    let tryConsumeSilentInterrupt ownerSessionId =
        lock gate (fun () -> silentInterrupts.Contains(SessionId.value ownerSessionId))

    let bindHandleAffinity ownerSessionId handleId laneIndex =
        lock gate (fun () -> handleAffinities.[handleKey ownerSessionId handleId] <- laneIndex)

    let trackGroupResource groupId (resource: IDisposable) =
        lock gate (fun () ->
            let resources =
                match groupResources.TryGetValue groupId with
                | true, current -> current
                | false, _ ->
                    // DSL-MUTABLE: algorithm-scratch — new resource list for dictionary insert
                    let created = ResizeArray<IDisposable>()
                    groupResources.[groupId] <- created
                    created

            resources.Add resource)

    let tryBeginDelivery groupId completionId (laneIndex: int) =
        lock gate (fun () -> deliveryClaims.Add(deliveryKey groupId completionId laneIndex))

    let endDelivery groupId completionId (laneIndex: int) =
        lock gate (fun () -> deliveryClaims.Remove(deliveryKey groupId completionId laneIndex) |> ignore)

    let tryBeginTakeover groupId =
        lock gate (fun () -> takeoverClaims.Add groupId)

    let endTakeover groupId =
        lock gate (fun () -> takeoverClaims.Remove groupId |> ignore)

    let private observerForGroup groupId =
        lock gate (fun () ->
            match childObservers.TryGetValue groupId with
            | true, callback -> Some callback
            | false, _ -> None)

    let notifyChildCreated laneSessionId handleId childSessionId =
        match tryLane laneSessionId with
        | None -> ()
        | Some binding ->
            bindHandleAffinity binding.OwnerSessionId handleId binding.LaneIndex

            observerForGroup binding.GroupId
            |> Option.iter (fun callback -> callback binding.LaneIndex handleId childSessionId)

    let clearOwner ownerSessionId =
        let ownerKey = SessionId.value ownerSessionId

        lock gate (fun () ->
            let groupIds =
                lanes
                |> Seq.filter (fun kv -> kv.Value.OwnerSessionId = ownerSessionId)
                |> Seq.map (fun kv -> kv.Value.GroupId)
                |> Seq.distinct
                |> Seq.toArray

            lanes
            |> Seq.filter (fun kv -> kv.Value.OwnerSessionId = ownerSessionId)
            |> Seq.map (fun kv -> kv.Key)
            |> Seq.toArray
            |> Array.iter (fun key -> lanes.Remove key |> ignore)

            groupIds |> Array.iter (fun groupId -> childObservers.Remove groupId |> ignore)

            groupIds |> Array.iter (fun groupId -> takeoverClaims.Remove groupId |> ignore)

            groupIds |> Array.iter (disposeGroupResources groupResources)

            handleAffinities.Keys
            |> Seq.filter (fun key -> key.StartsWith(ownerKey + "\u001f"))
            |> Seq.toArray
            |> Array.iter (fun key -> handleAffinities.Remove key |> ignore)

            silentInterrupts.Remove ownerKey |> ignore)
