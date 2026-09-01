namespace Wanxiangshu.Execution.Session.Attachment

open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ReuseScope =
    val ofSession: sessionId: SessionId -> ReuseScopeId
    val compatible: a: ReuseScopeId -> b: ReuseScopeId -> bool
    val sameScope: a: ReuseScopeId -> b: ReuseScopeId -> bool
