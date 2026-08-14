namespace Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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
