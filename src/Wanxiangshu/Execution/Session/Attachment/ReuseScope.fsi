namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type AttachedChildObservation =
    | Missing
    | Matching of SessionId
    | Conflicting of SessionId list

[<RequireQualifiedAccess>]
type AttachedChildDecision =
    | Create
    | Adopt of SessionId
    | RejectConflict of SessionId list

[<RequireQualifiedAccess>]
module AttachedChildObservation =
    val decide: AttachedChildObservation -> AttachedChildDecision

type IAttachedSessionPort =
    abstract TryFind: ownerSessionId: SessionId * role: SyncDelegateRole -> SessionId option
    abstract TryFindByScope: scope: ReuseScopeId * role: SyncDelegateRole -> SessionId option
    abstract TryFindOwner: delegateSessionId: SessionId * role: SyncDelegateRole -> SessionId option
    abstract Remove: ownerSessionId: SessionId * role: SyncDelegateRole -> bool
    abstract RemoveByDelegateSession: delegateSessionId: SessionId -> bool

    abstract GetOrCreate:
        ownerSessionId: SessionId *
        role: SyncDelegateRole *
        agentName: string *
        directory: string option *
        observeChild:
            (SessionId -> ReuseScopeId -> SyncDelegateRole -> string -> Task<Result<AttachedChildObservation, string>>) *
        createChild:
            (SessionId -> ReuseScopeId -> SyncDelegateRole -> string -> string option -> Task<Result<SessionId, string>>) *
        bindChild: (SessionId -> SessionId -> string -> unit) *
        onReady: (SessionId -> string -> unit) ->
            Task<Result<SessionId * string, string>>

    abstract Clear: unit -> unit

[<RequireQualifiedAccess>]
module ReuseScope =
    val ofSession: sessionId: SessionId -> ReuseScopeId
    val compatible: a: ReuseScopeId -> b: ReuseScopeId -> bool
    val sameScope: a: ReuseScopeId -> b: ReuseScopeId -> bool
