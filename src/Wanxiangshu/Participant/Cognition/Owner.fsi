namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module CognitiveOwner =
    type T =
        { SessionId: SessionId
          IncumbencyId: string }

    val create: sessionId: SessionId -> incumbencyId: string -> T
    val key: owner: T -> string
    val keyOfFact: fact: AssumeFactCases.T -> string
