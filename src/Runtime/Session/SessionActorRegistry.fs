module Wanxiangshu.Runtime.Session.SessionActorRegistry

open Wanxiangshu.Kernel.Session.SessionFact
open Wanxiangshu.Runtime.Session.SessionActor
open Wanxiangshu.Runtime.Session.SessionActorState

/// Process-wide registry: one SessionActor per (workspaceKey, physicalSessionId).
module SessionActorRegistry =
    let mutable private actors = Map.empty<string * string, SessionActor>

    let private key (workspaceKey: string) (sessionId: string) = workspaceKey, sessionId

    let TryGet (workspaceKey: string) (sessionId: string) : SessionActor option =
        Map.tryFind (key workspaceKey sessionId) actors

    let GetOrCreate (workspaceKey: string) (sessionId: string) : SessionActor =
        let k = key workspaceKey sessionId

        match Map.tryFind k actors with
        | Some actor when not actor.IsClosed -> actor
        | Some _
        | None ->
            let actor = SessionActor(workspaceKey, sessionId)
            actors <- Map.add k actor actors
            actor

    /// Drop actor from map. Does not Post — callers that need domain cleanup
    /// must Post SessionClosed first (or call finalizeSessionClosed).
    let Remove (workspaceKey: string) (sessionId: string) : unit =
        actors <- Map.remove (key workspaceKey sessionId) actors

    let NotifyClosed (workspaceKey: string) (sessionId: string) : unit =
        Remove workspaceKey sessionId

    let Clear () : unit = actors <- Map.empty

    let Count = fun () -> Map.count actors
