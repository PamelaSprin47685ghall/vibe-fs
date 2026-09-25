namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

/// The cognitive owner a canvas belongs to.
///
/// The owner is the physical session that holds the tool. The canvas is keyed by
/// the physical SessionId alone: one physical session keeps exactly one canvas
/// across Incumbencies, so a suicide or a logical Life replacement that reuses the
/// session container inherits the canvas instead of silently starting over, while
/// two physical sessions are always two owners whose canvases never meet.
///
/// IncumbencyId is still carried on the record because committed facts name it, but
/// it no longer takes part in ownership: a Life change is a new authority over the
/// same workspace, not a second workspace. Resolving the owner from the verified
/// tool context is the adapter's job; this module only defines the identity so no
/// caller can invent its own scope.
[<RequireQualifiedAccess>]
module CognitiveOwner =

    type T =
        { SessionId: SessionId
          IncumbencyId: string }

    let create (sessionId: SessionId) (incumbencyId: string) : T =
        { SessionId = sessionId
          IncumbencyId = if isNull incumbencyId then "" else incumbencyId }

    /// Stable key for the serial admission table, the canvas cache and the
    /// projection index. Deliberately the physical session alone: the canvas follows
    /// the session container across incumbencies, so keying on the incumbency would
    /// orphan a committed canvas the moment a Life turns over.
    let key (owner: T) : string = SessionId.value owner.SessionId

    /// The key a committed fact carries. Reading it from the fact rather than
    /// re-deriving the owner keeps the fold independent of how the adapter chose
    /// to scope this particular call.
    let keyOfFact (fact: AssumeFactCases.T) : string =
        match fact with
        | AssumeFactCases.T.AssumePhaseCommitted commit -> commit.OwnerKey
