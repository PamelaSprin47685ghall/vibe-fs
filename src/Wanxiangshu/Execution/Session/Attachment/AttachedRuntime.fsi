namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity

/// The SyncDelegate kind's observation of its owner's children. The kind decides
/// exactness and ambiguity; the core only ever sees a decided list.
type ObserveAttachedChild =
    SessionId -> ReuseScopeId -> SyncDelegateRole -> string -> Task<Result<AttachedChildObservation, string>>

/// The SyncDelegate kind's child factory, in this kind's own vocabulary.
type CreateAttachedChild =
    SessionId -> ReuseScopeId -> SyncDelegateRole -> string -> string option -> Task<Result<SessionId, string>>

/// EXEC-026 / HOST-008: in-process bindings for Work+Attached SyncDelegate sessions.
/// Keyed by `(OwnerReuseScopeId, SyncDelegateRole)`; supplies only this kind's
/// parameters to `AttachmentLeaseCore`, which owns creation, recovery, registration,
/// single-flight and release for every AttachmentKind (managed-session-lifecycle [001]).
type AttachedSessionRuntime =
    new:
        ?registry: AttachmentLeaseRegistry *
        ?registerParent: (SessionId -> SessionId -> unit) *
        ?isUsable: (SessionId -> bool) ->
            AttachedSessionRuntime

    interface IAttachedSessionPort

    member TryFind: ownerSessionId: SessionId * role: SyncDelegateRole -> SessionId option
    member TryFindByScope: scope: ReuseScopeId * role: SyncDelegateRole -> SessionId option
    member TryFindOwner: delegateSessionId: SessionId * role: SyncDelegateRole -> SessionId option
    member Remove: ownerSessionId: SessionId * role: SyncDelegateRole -> bool
    member RemoveByDelegateSession: delegateSessionId: SessionId -> bool

    member GetOrCreate:
        ownerSessionId: SessionId *
        role: SyncDelegateRole *
        agentName: string *
        directory: string option *
        observeChild: ObserveAttachedChild *
        createChild: CreateAttachedChild *
        bindChild: (SessionId -> SessionId -> string -> unit) *
        onReady: (SessionId -> string -> unit) ->
            Task<Result<SessionId * string, string>>

    member Clear: unit -> unit
