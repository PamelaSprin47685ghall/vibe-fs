namespace Wanxiangshu.Session

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
