namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type LoopSensor =
    new:
        isOwned: (SessionId -> bool) *
        abortSession: (SessionId -> Task<Result<unit, string>>) *
        continueSession: (SessionId -> DegenerationKind -> string option -> Task<Result<unit, string>>) *
        emitDiagnostic: (string -> (string * string) list -> unit) *
        ?runOwnedWork: ((unit -> Task) -> Task) ->
            LoopSensor

    member Observe: raw: obj -> unit

    member ConsumeAbortCause:
        sessionId: SessionId * expectedRun: ProviderRunIdentity * directory: string option -> AbortCause

    member DropSession: sessionId: SessionId -> unit
    member ResetDetector: sessionId: SessionId -> unit
    member ActiveInterruptTask: sessionId: SessionId * expectedRun: ProviderRunIdentity -> Task option

    interface ILoopSensor

module LoopSensor =
    val kindName: kind: DegenerationKind -> string
    val continuationPath: kind: DegenerationKind -> string

    val create:
        ownedSessions: HashSet<string> ->
        sessionParents: Dictionary<string, string> ->
        abortSession: (SessionId -> Task<Result<unit, string>>) ->
        continueSession: (SessionId -> DegenerationKind -> string option -> Task<Result<unit, string>>) ->
        emitDiagnostic: (string -> (string * string) list -> unit) ->
            LoopSensor
