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
let resolveRoute (sessionId: string) (isMainSession: string -> bool) : SessionRoute =
    if sessionId = "" then
        UnknownSession
    elif isMainSession sessionId then
        MainSession
    else
        match SubsessionActorRegistry.TryGet sessionId with
        | Some actor -> ChildSession actor
        | None ->
            if isMainSession sessionId then
                MainSession
            else
                UnknownSession

/// Resolve the current turn id for a registered child actor, if any.
let private tryGetCurrentTurnId (sessionId: string) : TurnId option =
    match SubsessionActorRegistry.TryGet sessionId with
    | Some actor -> actor.GetCurrentTurn()
    | None -> None

/// EvidenceUpdated with an empty TurnId is a placeholder from a host translator
/// that cannot attribute the observation itself. The router attributes it to
/// the actor's current turn, so the core reducer never sees an unattributed
/// observation. Observations that already carry a TurnId pass through unchanged.
let private attributeObservation (sessionId: string) (cmd: Command) : Command =
    match cmd with
    | EvidenceUpdated obs when obs.TurnId.IsNone ->
        match tryGetCurrentTurnId sessionId with
        | Some turnId -> EvidenceUpdated { obs with TurnId = Some turnId }
        | None -> cmd
    | _ -> cmd

/// Translate a host-level fact into a Command and post it to the child actor.
/// Returns true if the event was routed to a child (caller should NOT also
/// feed it into the main FallbackEventBridge).
let routeToChild (sessionId: string) (cmd: Command) : JS.Promise<bool> =
    promise {
        let cmd = attributeObservation sessionId cmd

        match SubsessionActorRegistry.TryGet sessionId with
        | Some actor ->
            do! actor.Post cmd
            return true
        | None -> return false
    }

/// Convenience: post evidence to the current turn of a child actor.
/// Returns true if the actor exists and has an active turn.
let routeEvidence (sessionId: string) (evidence: CurrentTurnEvidence) : JS.Promise<bool> =
    promise {
        match tryGetCurrentTurnId sessionId with
        | Some turnId ->
            return!
                routeToChild
                    sessionId
                    (EvidenceUpdated
                        { TurnId = Some turnId
                          Evidence = evidence })
        | None -> return false
    }

/// Convenience: post SessionIdleObserved to a child if it exists.
let tryIdle (sessionId: string) : JS.Promise<bool> =
    routeToChild sessionId SessionIdleObserved

/// Convenience: post TurnErrorObserved.
let tryError (sessionId: string) (err: Wanxiangshu.Kernel.FallbackKernel.Types.ErrorInput) : JS.Promise<bool> =
    routeToChild sessionId (TurnErrorObserved err)

/// True when this session is owned by a SubsessionActor (child).
let isChildSession (sessionId: string) : bool =
    match SubsessionActorRegistry.TryGet sessionId with
    | Some _ -> true
    | None -> false
