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

    let Remove (workspaceKey: string) (sessionId: string) : unit =
        let k = key workspaceKey sessionId
        actors <- Map.remove k actors

    /// Drop the actor from the registry, then enqueue SessionClosed so domain
    /// finally-cleanup still runs. Removal is first so a concurrent GetOrCreate
    /// cannot observe a half-closed mailbox and drop live facts.
    let NotifyClosed (workspaceKey: string) (sessionId: string) : unit =
        let k = key workspaceKey sessionId

        match Map.tryFind k actors with
        | Some actor ->
            actors <- Map.remove k actors
            actor.Post SessionFact.SessionClosed |> ignore
        | None -> ()

    let Clear () : unit =
        let current = actors
        actors <- Map.empty

        for KeyValue(_, actor) in current do
            if not actor.IsClosed then
                actor.Post SessionFact.SessionClosed |> ignore

    let Count = fun () -> Map.count actors
