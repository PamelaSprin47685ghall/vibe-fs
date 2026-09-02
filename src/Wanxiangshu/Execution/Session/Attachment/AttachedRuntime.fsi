namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity

type AttachedSessionRuntime =
    new: ?registerParent: (SessionId -> SessionId -> unit) * ?isUsable: (SessionId -> bool) -> AttachedSessionRuntime

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
        observeChild:
            (SessionId -> ReuseScopeId -> SyncDelegateRole -> string -> Task<Result<AttachedChildObservation, string>>) *
        createChild:
            (SessionId -> ReuseScopeId -> SyncDelegateRole -> string -> string option -> Task<Result<SessionId, string>>) *
        bindChild: (SessionId -> SessionId -> string -> unit) *
        onReady: (SessionId -> string -> unit) ->
            Task<Result<SessionId * string, string>>

    member Clear: unit -> unit
