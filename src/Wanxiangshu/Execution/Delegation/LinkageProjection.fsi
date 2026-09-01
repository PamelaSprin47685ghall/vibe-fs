namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type HandleCompletion =
    { Kind: HandleCompletionKind
      CompletionRef: BlobRef option
      CompletionDigest: BlobDigest option }

type HandleLifecycle =
    | Active
    | CompletedAwaitingJoin of HandleCompletion
    | Abandoned of HandleAbandonReason
    | Retired

type HandleRecord =
    { Handle: HandleId
      ChildSessionId: SessionId
      TargetAgent: string
      Byname: string
      CanonicalRole: Role
      Ownership: HandleOwnership
      Lifecycle: HandleLifecycle
      CreationOrder: int
      LastCompletion: HandleCompletion option }

type AgentLinkageProjection =
    { Handles: Map<HandleId, HandleRecord>
      NextCreationOrder: int }

type HandleTransitionRejection =
    | UnknownHandle
    | HandleIdentityConflict
    | HandleIsRetired
    | AlreadyCompleted
    | AlreadyAbandoned
    | NotCompleted

module HandleProjection =
    val empty: AgentLinkageProjection

    val linkNamed:
        handle: HandleId ->
        childSessionId: SessionId ->
        targetAgent: string ->
        byname: string ->
        role: Role ->
        ownership: HandleOwnership ->
        current: AgentLinkageProjection ->
            Result<AgentLinkageProjection, HandleTransitionRejection>

    val link:
        handle: HandleId ->
        childSessionId: SessionId ->
        targetAgent: string ->
        role: Role ->
        ownership: HandleOwnership ->
        current: AgentLinkageProjection ->
            Result<AgentLinkageProjection, HandleTransitionRejection>

    val complete:
        handle: HandleId ->
        completion: HandleCompletion ->
        current: AgentLinkageProjection ->
            Result<AgentLinkageProjection, HandleTransitionRejection>

    val abandon:
        handle: HandleId ->
        reason: HandleAbandonReason ->
        current: AgentLinkageProjection ->
            Result<AgentLinkageProjection, HandleTransitionRejection>

    val retire:
        handle: HandleId ->
        current: AgentLinkageProjection ->
            Result<AgentLinkageProjection, HandleTransitionRejection>

    val rejectFalseCompletion:
        handle: HandleId ->
        expectedRef: BlobRef ->
        expectedDigest: BlobDigest ->
        current: AgentLinkageProjection ->
            Result<AgentLinkageProjection, HandleTransitionRejection>

    val tryFind: handle: HandleId -> current: AgentLinkageProjection -> HandleRecord option
    val isRetired: handle: HandleId -> current: AgentLinkageProjection -> bool
    val isAbandoned: handle: HandleId -> current: AgentLinkageProjection -> bool
    val tryFindByByname: byname: string -> current: AgentLinkageProjection -> HandleRecord option
    val listable: current: AgentLinkageProjection -> HandleRecord list
    val horizonVisible: current: AgentLinkageProjection -> HandleRecord list
    val joinable: current: AgentLinkageProjection -> HandleRecord list
    val reportableAbandoned: current: AgentLinkageProjection -> HandleRecord list
    val activeHandles: current: AgentLinkageProjection -> HandleRecord list
    val tryFindByChildSession: childSessionId: SessionId -> current: AgentLinkageProjection -> HandleRecord option
    val lifecycleSealsBlogger: lifecycle: HandleLifecycle -> bool
    val recordSealsBlogger: record: HandleRecord -> bool
    val linkedChildren: current: AgentLinkageProjection -> HandleRecord list
