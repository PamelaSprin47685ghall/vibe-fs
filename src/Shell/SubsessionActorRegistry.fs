module Wanxiangshu.Shell.SubsessionActorRegistry

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Kernel.Subsession.Fold

module SubsessionActorRegistry =
    let mutable private actors = Map.empty<string * string, SubsessionActor>
    // Safety projection is keyed by workspace root so multiple plugin instances
    // in the same Node process cannot overwrite each other's durable poison state.
    let mutable private safetyProj = Map.empty<string, SessionSafetyProjection>

    /// Replace the safety projection for the given workspace root.
    let SetSafetyProjection (workspaceRoot: string) (proj: SessionSafetyProjection) : unit =
        safetyProj <- Map.add workspaceRoot proj safetyProj

    /// Find a safety entry for this session id in the specified workspace.
    let private tryFindSafetyEntry (workspaceRoot: string) (sid: SessionId) : SessionSafetyEntry option =
        match Map.tryFind workspaceRoot safetyProj with
        | Some proj -> Map.tryFind sid proj
        | None -> None

    /// Remove the session id from safety projection in the specified workspace.
    let private removeSafetyEntry (workspaceRoot: string) (sid: SessionId) : unit =
        match Map.tryFind workspaceRoot safetyProj with
        | Some proj ->
            let updated = Map.remove sid proj
            safetyProj <- Map.add workspaceRoot updated safetyProj
        | None -> ()

    let ClearPoison (workspaceRoot: string) (sessionId: string) : unit =
        let sid = SessionId.create sessionId
        removeSafetyEntry workspaceRoot sid

        let key = (workspaceRoot, sessionId)

        match Map.tryFind key actors with
        | Some actor ->
            actors <- Map.remove key actors
            actor.Post SessionClosed |> ignore
        | None -> ()

    let TryGet (workspaceRoot: string) (sessionId: string) : SubsessionActor option =
        Map.tryFind (workspaceRoot, sessionId) actors

    let GetOrCreate
        (workspaceRoot: string)
        (sessionId: string)
        (host: ISubsessionHost)
        (eventStore: ISubsessionEventStore)
        : SubsessionActor =
        let sid = SessionId.create sessionId
        let key = (workspaceRoot, sessionId)

        match Map.tryFind key actors with
        | Some actor when not actor.IsDisposed -> actor
        | _ ->
            let initialState =
                match tryFindSafetyEntry workspaceRoot sid with
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
                            match Map.tryFind key actors with
                            | Some currentActor ->
                                match actorOpt with
                                | Some a when obj.ReferenceEquals(currentActor, a) -> actors <- Map.remove key actors
                                | _ -> ()
                            | None -> ()),
                    ?initialState = initialState
                )

            actorOpt <- Some actor
            actors <- Map.add key actor actors
            actor

    let Remove (workspaceRoot: string) (sessionId: string) : unit =
        let sid = SessionId.create sessionId
        removeSafetyEntry workspaceRoot sid

        let key = (workspaceRoot, sessionId)

        match Map.tryFind key actors with
        | Some actor ->
            actors <- Map.remove key actors
            actor.Post SessionClosed |> ignore
        | None -> ()

    let Clear () : unit =
        actors <- Map.empty
        safetyProj <- Map.empty
