namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type DegenerationKind =
    | TooRepetitive
    | TooRandom

[<RequireQualifiedAccess>]
type AbortCause =
    | DegenerationGuard of DegenerationKind
    | External

type LoopSensor =
    new:
        isOwned: (SessionId -> bool) *
        abortSession: (SessionId -> Task<Result<unit, string>>) *
        continueSession: (SessionId -> DegenerationKind -> string option -> Task<Result<unit, string>>) ->
            LoopSensor

    member Observe: raw: obj -> unit
    member ConsumeAbortCause: sessionId: SessionId * directory: string option -> AbortCause
    member DropSession: sessionId: SessionId -> unit
    member ResetDetector: sessionId: SessionId -> unit

module LoopSensor =
    val kindName: kind: DegenerationKind -> string
    val continuationPath: kind: DegenerationKind -> string

    val create:
        ownedSessions: HashSet<string> ->
        sessionParents: Dictionary<string, string> ->
        abortSession: (SessionId -> Task<Result<unit, string>>) ->
        continueSession: (SessionId -> DegenerationKind -> string option -> Task<Result<unit, string>>) ->
            LoopSensor
