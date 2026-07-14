module Wanxiangshu.Shell.SubsessionActorRegistry

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.SubsessionActor

/// Registry of SubsessionActors keyed by physical session id.
/// Lifecycle is tied to the physical child session:
///   create physical child → create actor
///   multiple logical runs → reuse same actor
///   delete physical child → remove actor
///
/// Actors are NEVER removed after each run (poison must survive).
module SubsessionActorRegistry =
    let mutable private actors = Map.empty<string, SubsessionActor>

    let TryGet (sessionId: string) : SubsessionActor option = Map.tryFind sessionId actors

    let GetOrCreate (sessionId: string) (host: ISubsessionHost) : SubsessionActor =
        match Map.tryFind sessionId actors with
        | Some actor -> actor
        | None ->
            let sid = SessionId.create sessionId
            let actor = SubsessionActor(sid, host)
            actors <- Map.add sessionId actor actors
            actor

    /// Remove only when the physical session is deleted.
    let Remove (sessionId: string) : unit = actors <- Map.remove sessionId actors

    let Clear () : unit = actors <- Map.empty
