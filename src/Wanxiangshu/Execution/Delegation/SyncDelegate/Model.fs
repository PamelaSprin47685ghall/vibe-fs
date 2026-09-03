namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation

open Wanxiangshu.Foundation.Identity

/// EXEC-026 / HOST-008: SyncDelegate vocabulary — dedicated Inspector/Coder
/// ownership keys and AttachmentKind mapping. Runtime/tools are wired later; this module is types + helpers only.

[<RequireQualifiedAccess>]
type SyncDelegateRole =
    | Inspector
    | Coder

/// EXEC-026: reuse-scope half of the dedicated SyncDelegate key.
/// Prefer a local wrapper here over churning Identity.fs.
type ReuseScopeId = private ReuseScopeId of string

module ReuseScopeId =
    let create (value: string) = ReuseScopeId value
    let value (ReuseScopeId v) = v
    /// Structural equality on the underlying string (Identity-style wrapper).
    let equals (ReuseScopeId a) (ReuseScopeId b) = a = b

/// EXEC-026: `(OwnerReuseScopeId, SyncDelegateRole)` — at most one live dedicated Session.
type DedicatedDelegateKey =
    { Scope: ReuseScopeId
      Role: SyncDelegateRole }

/// EXEC-026/031: one semantic sync batch = all calls to one dedicated role
/// emitted by one assistant ProviderRun, in provider tool-call order.
type SyncDelegateBatch =
    { ProviderRun: ProviderRunIdentity
      CallOrder: ToolCallId list
      CurrentCall: ToolCallId }

[<RequireQualifiedAccess>]
type SyncDelegateInvocationResult =
    | WorkRecord of string
    | MergedInto of ToolCallId

module DedicatedDelegateKey =
    let create (scope: ReuseScopeId) (role: SyncDelegateRole) = { Scope = scope; Role = role }

module SyncDelegate =

    let tryRoleOfToolName (name: string) =
        match name.Trim().ToLowerInvariant() with
        | "inspect" -> Some SyncDelegateRole.Inspector
        | "establish-behavior"
        | "repair-behavior" -> Some SyncDelegateRole.Coder
        | _ -> None

    /// EXEC-026: canonical wire role label for a dedicated SyncDelegate
    /// (`inspector` / `coder`). Sole definition — Session/ layer references this.
    let roleLabel (role: SyncDelegateRole) : string =
        match role with
        | SyncDelegateRole.Inspector -> "inspector"
        | SyncDelegateRole.Coder -> "coder"

    /// HOST-008: SyncDelegateRole → AttachmentKind for Work+Attached registration.
    let delegateRoleToAttachment (role: SyncDelegateRole) : AttachmentKind =
        match role with
        | SyncDelegateRole.Inspector -> AttachmentKind.SyncInspector
        | SyncDelegateRole.Coder -> AttachmentKind.SyncCoder

    /// Canonical wire agent name for a dedicated SyncDelegate (`inspector`, `coder`).
    let agentNameFor (role: SyncDelegateRole) : string = roleLabel role
