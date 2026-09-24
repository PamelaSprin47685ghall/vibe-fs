namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

/// The cognitive owner a canvas belongs to.
///
/// Deliberately NOT the physical Host session id and NOT a process-global singleton:
/// two logical incumbencies over the same physical session are two owners, and two
/// physical sessions for one Manager are two owners. Resolving the owner from the
/// verified tool context plus the current authority is the adapter's job; this module
/// only defines the identity so no caller can invent its own scope.
[<RequireQualifiedAccess>]
module CognitiveOwner =

    type T =
        { SessionId: SessionId
          IncumbencyId: string }

    let create (sessionId: SessionId) (incumbencyId: string) : T =
        { SessionId = sessionId
          IncumbencyId = if isNull incumbencyId then "" else incumbencyId }

    /// Stable key for the serial admission table and the projection index. The
    /// physical session alone would collide across incumbencies; the pair does not.
    let key (owner: T) : string =
        SessionId.value owner.SessionId + "\u001f" + owner.IncumbencyId

    /// The key a committed fact carries. Reading it from the fact rather than
    /// re-deriving the owner keeps the fold independent of how the adapter chose
    /// to scope this particular call.
    let keyOfFact (fact: AssumeFactCases.T) : string =
        match fact with
        | AssumeFactCases.T.AssumePhaseCommitted commit -> commit.OwnerKey
