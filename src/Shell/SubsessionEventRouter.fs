module Wanxiangshu.Shell.SubsessionEventRouter

open Fable.Core
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionActorRegistry

/// Routing decision for a raw host event.
type SessionRoute =
    | MainSession
    | ChildSession of SubsessionActor
    | UnknownSession

/// Resolve whether a session id is a child actor or a main session.
/// `isMainSession` is host-provided (e.g. "no parent id" or "not in registry").
let resolveRoute (workspaceRoot: string) (sessionId: string) (isMainSession: string -> bool) : SessionRoute =
    if sessionId = "" then
        UnknownSession
    elif isMainSession sessionId then
        MainSession
    else
        match SubsessionActorRegistry.TryGet workspaceRoot sessionId with
        | Some actor -> ChildSession actor
        | None ->
            if isMainSession sessionId then
                MainSession
            else
                UnknownSession

/// Resolve the current turn id for a registered child actor, if any.
let private tryGetCurrentTurnId (workspaceRoot: string) (sessionId: string) : TurnId option =
    match SubsessionActorRegistry.TryGet workspaceRoot sessionId with
    | Some actor -> actor.GetCurrentTurn()
    | None -> None

/// Translate a host-level fact into a Command and post it to the child actor.
/// Returns true if the event was routed to a child (caller should NOT also
/// feed it into the main FallbackEventBridge).
/// If the command is an EvidenceUpdated observation with TurnId of None, we do
/// not post it to the actor to avoid cross-turn contamination, but we still
/// consider it routed if the child exists in the registry.
let routeToChild (workspaceRoot: string) (sessionId: string) (cmd: Command) : JS.Promise<bool> =
    promise {
        match SubsessionActorRegistry.TryGet workspaceRoot sessionId with
        | Some actor ->
            match cmd with
            | EvidenceUpdated obs when obs.TurnId.IsNone ->
                match actor.GetCurrentTurn() with
                | Some turnId -> do! actor.Post(EvidenceUpdated { obs with TurnId = Some turnId })
                | None -> ()

                return true
            | _ ->
                do! actor.Post cmd
                return true
        | None -> return false
    }

/// Convenience: post evidence to the current turn of a child actor.
/// Returns true if the actor exists and has an active turn.
let routeEvidence (workspaceRoot: string) (sessionId: string) (evidence: CurrentTurnEvidence) : JS.Promise<bool> =
    promise {
        match tryGetCurrentTurnId workspaceRoot sessionId with
        | Some turnId ->
            return!
                routeToChild
                    workspaceRoot
                    sessionId
                    (EvidenceUpdated
                        { TurnId = Some turnId
                          Evidence = evidence })
        | None -> return false
    }

/// Convenience: post SessionIdleObserved to a child if it exists.
let tryIdle (workspaceRoot: string) (sessionId: string) : JS.Promise<bool> =
    routeToChild workspaceRoot sessionId SessionIdleObserved

/// Convenience: post TurnErrorObserved.
let tryError
    (workspaceRoot: string)
    (sessionId: string)
    (err: Wanxiangshu.Kernel.FallbackKernel.Types.ErrorInput)
    : JS.Promise<bool> =
    routeToChild workspaceRoot sessionId (TurnErrorObserved err)

/// True when this session is owned by a SubsessionActor (child).
let isChildSession (workspaceRoot: string) (sessionId: string) : bool =
    match SubsessionActorRegistry.TryGet workspaceRoot sessionId with
    | Some _ -> true
    | None -> false
