namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
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
    let decide =
        function
        | AttachedChildObservation.Missing -> AttachedChildDecision.Create
        | AttachedChildObservation.Matching childId -> AttachedChildDecision.Adopt childId
        | AttachedChildObservation.Conflicting children -> AttachedChildDecision.RejectConflict children

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

/// EXEC-026 / HOST-008: OwnerReuseScopeId helpers for dedicated SyncDelegate keys.
///
/// G2 compatibility (§11.2): two owners are compatible iff they share the same
/// ReuseScopeId. This wave maps one SessionId → one scope id (SessionId value).
/// CaseFinalize / Bookkeeper synthesis on graceful close is out of scope for G2.
[<RequireQualifiedAccess>]
module ReuseScope =

    /// Owner session → OwnerReuseScopeId. G2: identity wrap of SessionId.value.
    let ofSession (sessionId: SessionId) : ReuseScopeId =
        ReuseScopeId.create (SessionId.value sessionId)

    /// §11.2: compatible work reuses the same dedicated binding when scope ids match.
    let compatible (a: ReuseScopeId) (b: ReuseScopeId) : bool = ReuseScopeId.equals a b

    /// Same as `compatible`, named for call-site clarity at GetOrCreate / gate keys.
    let sameScope (a: ReuseScopeId) (b: ReuseScopeId) : bool = compatible a b
