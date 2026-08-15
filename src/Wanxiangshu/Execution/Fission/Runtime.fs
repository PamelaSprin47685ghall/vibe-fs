namespace Wanxiangshu.Execution.Fission

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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
    let private lanes = Dictionary<string, FissionLaneBinding>()
    let private silentInterrupts = HashSet<string>()
    let private handleAffinities = Dictionary<string, int>()

    let private childObservers =
        Dictionary<string, int -> string -> SessionId -> unit>()

    let private groupResources = Dictionary<string, ResizeArray<IDisposable>>()
    let private deliveryClaims = HashSet<string>()

    let private handleKey ownerSessionId handleId =
        SessionId.value ownerSessionId + "\u001f" + handleId

    let private deliveryKey groupId completionId laneIndex =
        groupId + "\u001f" + completionId + "\u001f" + string laneIndex

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

    let registerChildObserver groupId observer =
        lock gate (fun () -> childObservers.[groupId] <- observer)

    let trackGroupResource groupId (resource: IDisposable) =
        lock gate (fun () ->
            let resources =
                match groupResources.TryGetValue groupId with
                | true, current -> current
                | false, _ ->
                    let created = ResizeArray<IDisposable>()
                    groupResources.[groupId] <- created
                    created

            resources.Add resource)

    let tryBeginDelivery groupId completionId laneIndex =
        lock gate (fun () -> deliveryClaims.Add(deliveryKey groupId completionId laneIndex))

    let endDelivery groupId completionId laneIndex =
        lock gate (fun () -> deliveryClaims.Remove(deliveryKey groupId completionId laneIndex) |> ignore)

    let clearGroup groupId =
        let resources =
            lock gate (fun () ->
                childObservers.Remove groupId |> ignore

                deliveryClaims
                |> Seq.filter (fun key -> key.StartsWith(groupId + "\u001f"))
                |> Seq.toArray
                |> Array.iter (fun key -> deliveryClaims.Remove key |> ignore)

                match groupResources.TryGetValue groupId with
                | true, current ->
                    groupResources.Remove groupId |> ignore
                    current |> Seq.toList
                | false, _ -> [])

        for resource in resources do
            try
                resource.Dispose()
            with _ ->
                ()

    let notifyChildCreated laneSessionId handleId childSessionId =
        match tryLane laneSessionId with
        | None -> ()
        | Some binding ->
            bindHandleAffinity binding.OwnerSessionId handleId binding.LaneIndex

            let observer =
                lock gate (fun () ->
                    match childObservers.TryGetValue binding.GroupId with
                    | true, callback -> Some callback
                    | false, _ -> None)

            observer
            |> Option.iter (fun callback -> callback binding.LaneIndex handleId childSessionId)

    let tryHandleAffinity ownerSessionId handleId =
        lock gate (fun () ->
            match handleAffinities.TryGetValue(handleKey ownerSessionId handleId) with
            | true, laneIndex -> Some laneIndex
            | false, _ -> None)

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

            groupIds
            |> Array.iter (fun groupId ->
                match groupResources.TryGetValue groupId with
                | true, resources ->
                    for resource in resources do
                        try
                            resource.Dispose()
                        with _ ->
                            ()

                    groupResources.Remove groupId |> ignore
                | false, _ -> ())

            handleAffinities.Keys
            |> Seq.filter (fun key -> key.StartsWith(ownerKey + "\u001f"))
            |> Seq.toArray
            |> Array.iter (fun key -> handleAffinities.Remove key |> ignore)

            silentInterrupts.Remove ownerKey |> ignore)
