module Wanxiangshu.Shell.SubsessionActorRegistry

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Kernel.Subsession.Fold

module SubsessionActorRegistry =
    let mutable private actors = Map.empty<string, SubsessionActor>
    // Safety projection is keyed by workspace root so multiple plugin instances
    // in the same Node process cannot overwrite each other's durable poison state.
    let mutable private safetyProj = Map.empty<string, SessionSafetyProjection>

    /// Replace the safety projection for the given workspace root.
    let SetSafetyProjection (workspaceRoot: string) (proj: SessionSafetyProjection) : unit =
        safetyProj <- Map.add workspaceRoot proj safetyProj

    /// Find a safety entry for this session id across all workspace projections.
    /// If any workspace has marked the session as persistently poisoned, that entry
    /// wins over an active-run entry in another workspace.
    let private tryFindSafetyEntry (sid: SessionId) : SessionSafetyEntry option =
        safetyProj
        |> Map.tryPick (fun _ proj ->
            match Map.tryFind sid proj with
            | Some(PersistentlyPoisoned _ as entry) -> Some entry
            | _ -> None)

    /// Remove the session id from every workspace projection.
    let private removeFromAllSafetyProjections (sid: SessionId) : unit =
        safetyProj <- safetyProj |> Map.map (fun _ proj -> Map.remove sid proj)

    let ClearPoison (sessionId: string) : unit =
        let sid = SessionId.create sessionId
        removeFromAllSafetyProjections sid

        match Map.tryFind sessionId actors with
        | Some actor ->
            actors <- Map.remove sessionId actors
            actor.Post SessionClosed |> ignore
        | None -> ()

    let TryGet (sessionId: string) : SubsessionActor option = Map.tryFind sessionId actors

    let GetOrCreate (sessionId: string) (host: ISubsessionHost) (eventStore: ISubsessionEventStore) : SubsessionActor =
        let sid = SessionId.create sessionId

        match Map.tryFind sessionId actors with
        | Some actor when not actor.IsDisposed -> actor
        | _ ->
            let initialState =
                match tryFindSafetyEntry sid with
                | Some(PersistentlyPoisoned reason) -> Some(Poisoned reason)
                | _ -> None

            let mutable actorOpt = None

            let actor =
                SubsessionActor(
                    sid,
                    host,
                    eventStore,
                    onDispose =
                        (fun () ->
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

    let Remove (sessionId: string) : unit =
        let sid = SessionId.create sessionId
        removeFromAllSafetyProjections sid

        match Map.tryFind sessionId actors with
        | Some actor ->
            actors <- Map.remove sessionId actors
            actor.Post SessionClosed |> ignore
        | None -> ()

    let Clear () : unit =
        actors <- Map.empty
        safetyProj <- Map.empty
