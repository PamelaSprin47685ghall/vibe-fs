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
            // Not registered as a child and not main → unknown.
            // Could be a transient host session we don't own.
            if isMainSession sessionId then
                MainSession
            else
                UnknownSession

/// Translate a host-level fact into a Command and post it to the child actor.
/// Returns true if the event was routed to a child (caller should NOT also
/// feed it into the main FallbackEventBridge).
let routeToChild (sessionId: string) (cmd: Command) : JS.Promise<bool> =
    promise {
        match SubsessionActorRegistry.TryGet sessionId with
        | Some actor ->
            do! actor.Post cmd
            return true
        | None -> return false
    }

/// Convenience: post SessionIdleObserved to a child if it exists.
let tryIdle (sessionId: string) : JS.Promise<bool> =
    routeToChild sessionId SessionIdleObserved

/// Convenience: post TurnErrorObserved.
let tryError (sessionId: string) (err: Wanxiangshu.Kernel.FallbackKernel.Types.ErrorInput) : JS.Promise<bool> =
    routeToChild sessionId (TurnErrorObserved err)

/// Convenience: post TaskCompleteObserved.
let tryTaskComplete (sessionId: string) (output: string) : JS.Promise<bool> =
    routeToChild sessionId (TaskCompleteObserved output)
