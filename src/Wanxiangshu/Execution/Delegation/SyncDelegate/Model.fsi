namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type SyncDelegateRole =
    | Inspector
    | Coder

type ReuseScopeId = private ReuseScopeId of string

module ReuseScopeId =
    val create: value: string -> ReuseScopeId
    val value: ReuseScopeId -> string
    val equals: ReuseScopeId -> ReuseScopeId -> bool

type DedicatedDelegateKey =
    { Scope: ReuseScopeId
      Role: SyncDelegateRole }

type SyncDelegateBatch =
    { ProviderRun: ProviderRunIdentity
      CallOrder: ToolCallId list
      CurrentCall: ToolCallId }

[<RequireQualifiedAccess>]
type SyncDelegateInvocationResult =
    | WorkRecord of string
    | MergedInto of ToolCallId

module DedicatedDelegateKey =
    val create: scope: ReuseScopeId -> role: SyncDelegateRole -> DedicatedDelegateKey

module SyncDelegate =
    val tryRoleOfToolName: name: string -> SyncDelegateRole option
    val roleLabel: role: SyncDelegateRole -> string
    val delegateRoleToAttachment: role: SyncDelegateRole -> AttachmentKind
    val agentNameFor: role: SyncDelegateRole -> string
