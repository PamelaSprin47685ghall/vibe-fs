namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation.Identity

type SyncDelegateWait =
    | DelegateCompletion of owner: SessionId * delegateSession: SessionId * role: SyncDelegateRole
    | InvocationJoin of owner: SessionId * role: SyncDelegateRole

module SyncDelegateWait =
    val describe: wait: SyncDelegateWait -> DiagnosticWait
