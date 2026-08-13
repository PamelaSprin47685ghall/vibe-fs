namespace Wanxiangshu.Kernel

open Wanxiangshu.Kernel.Identity

/// EXEC-026 / HOST-008: SyncDelegate vocabulary — dedicated Inspector/Coder
/// ownership keys, AttachmentKind mapping, and owner→delegate tier (fast→fast,
/// deep→deep). Runtime/tools are wired later; this module is types + helpers only.

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

    /// HOST-008: SyncDelegateRole → AttachmentKind for Work+Attached registration.
    let delegateRoleToAttachment (role: SyncDelegateRole) : AttachmentKind =
        match role with
        | SyncDelegateRole.Inspector -> AttachmentKind.SyncInspector
        | SyncDelegateRole.Coder -> AttachmentKind.SyncCoder

    /// EXEC-026 nail: owner effective tier maps identically onto the dedicated
    /// delegate (`fast→fast`, `deep→deep`). Call sites must not override tier.
    let tierForOwner (ownerTier: AgentTier) : AgentTier = ownerTier

    /// Wire agent name for a dedicated SyncDelegate (`fast-inspector`, …).
    let agentNameFor (role: SyncDelegateRole) (tier: AgentTier) : string =
        let tierLabel =
            match tier with
            | AgentTier.Fast -> "fast"
            | AgentTier.Deep -> "deep"

        let roleLabel =
            match role with
            | SyncDelegateRole.Inspector -> "inspector"
            | SyncDelegateRole.Coder -> "coder"

        sprintf "%s-%s" tierLabel roleLabel
