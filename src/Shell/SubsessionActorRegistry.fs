module Wanxiangshu.Shell.SubsessionActorRegistry

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Kernel.Subsession.Fold

module SubsessionActorRegistry =
    let mutable private actors = Map.empty<string, SubsessionActor>
    let mutable private safetyProj = Map.empty<SessionId, SessionSafetyEntry>

    let SetSafetyProjection (proj: SessionSafetyProjection) : unit =
        safetyProj <- proj

    let ClearPoison (sessionId: string) : unit =
        let sid = SessionId.create sessionId
        safetyProj <- Map.remove sid safetyProj
        match Map.tryFind sessionId actors with
        | Some actor ->
            actors <- Map.remove sessionId actors
            actor.Post SessionClosed |> ignore
        | None -> ()

    let TryGet (sessionId: string) : SubsessionActor option = Map.tryFind sessionId actors

    let GetOrCreate
        (sessionId: string)
        (host: ISubsessionHost)
        (eventStore: ISubsessionEventStore)
        : SubsessionActor =
        let sid = SessionId.create sessionId
        match Map.tryFind sessionId actors with
        | Some actor when not actor.IsDisposed -> actor
        | _ ->
            let initialState =
                match Map.tryFind sid safetyProj with
                | Some (PersistentlyPoisoned reason) -> Some (Poisoned reason)
                | _ -> None

            let mutable actorOpt = None
            let actor =
                SubsessionActor(
                    sid,
                    host,
                    eventStore,
                    // Remove registry entry if not already removed by Remove/ClearPoison.
                    // This acts as a double safety net to ensure final cleanup.
                    onDispose = (fun () ->
                        match Map.tryFind sessionId actors with
                        | Some currentActor ->
                            match actorOpt with
                            | Some a when obj.ReferenceEquals(currentActor, a) ->
                                actors <- Map.remove sessionId actors
                            | _ -> ()
                        | None -> ()),
                    ?initialState = initialState
                )
            actorOpt <- Some actor

            actors <- Map.add sessionId actor actors
            actor

    /// Physical session deletion: synchronously remove from the registry map first
    /// to prevent concurrent GetOrCreate from reusing this poisoned actor,
    /// then post SessionClosed to trigger asynchronous disposal.
    let Remove (sessionId: string) : unit =
        let sid = SessionId.create sessionId
        safetyProj <- Map.remove sid safetyProj
        match Map.tryFind sessionId actors with
        | Some actor ->
            actors <- Map.remove sessionId actors
            actor.Post SessionClosed |> ignore
        | None -> ()

    let Clear () : unit =
        actors <- Map.empty
        safetyProj <- Map.empty
