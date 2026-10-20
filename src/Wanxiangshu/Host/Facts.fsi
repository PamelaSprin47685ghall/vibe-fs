namespace Wanxiangshu.Host

open Wanxiangshu.Foundation.Identity

type ConcernPlacementBatch =
    { AnnouncedGenerations: string list
      DeliveredMessages: string list }

[<RequireQualifiedAccess>]
type TipPresentation =
    | Full
    | IdentityOnly

[<RequireQualifiedAccess>]
type TaskDeliveryTask =
    | ChildWork of handle: string
    | Invocation of invocationId: string
    | Lifecycle of workId: string * incumbencyId: string

type TaskDeliveryFence =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRoot: AuthorityRootUserMessageId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Task: TaskDeliveryTask }

type TaskDeliveryWitness =
    { Fence: TaskDeliveryFence
      FormalText: string
      ViewDeclaration: string
      View: string
      MaterialDigest: string
      RetirementId: string option }

module TaskDelivery =
    val key: fence: TaskDeliveryFence -> SessionId * ProviderRunIdentity * ToolCallId
    val validate: sha256: (string -> string) -> witness: TaskDeliveryWitness -> Result<unit, string>

    /// 精确围栏读取：按 (SessionId, ProviderRun, ToolCallId) 精确命中，
    /// 未命中时按 (SessionId, ProviderRun) 取全部见证并按完整围栏筛选。
    val tryFindByFence:
        fence: TaskDeliveryFence ->
        witnesses: Map<SessionId * ProviderRunIdentity * ToolCallId, TaskDeliveryWitness> ->
            TaskDeliveryWitness option

    /// 围栏子集读取：调用方只掌握部分围栏字段时使用；材料不一致时拒绝。
    val tryFindByFenceFields:
        sessionId: SessionId ->
        providerRun: ProviderRunIdentity ->
        toolCallId: ToolCallId option ->
        authorityRoot: AuthorityRootUserMessageId option ->
        physicalUserMessageId: PhysicalUserMessageId option ->
        logicalRunId: LogicalRunId option ->
        task: TaskDeliveryTask option ->
        witnesses: Map<SessionId * ProviderRunIdentity * ToolCallId, TaskDeliveryWitness> ->
            TaskDeliveryWitness option

    /// 粗粒度读取：同一 (SessionId, ProviderRun) 下多条见证不再整体丢弃证据。
    val tryFind:
        sessionId: SessionId ->
        providerRun: ProviderRunIdentity ->
        witnesses: Map<SessionId * ProviderRunIdentity * ToolCallId, TaskDeliveryWitness> ->
            TaskDeliveryWitness option

type HostFactCases =
    | TaskDeliveryCommitted of TaskDeliveryWitness
    | PairProgrammingGuidelineAnchored of
        {| SessionId: SessionId
           Ordinal: int64
           CallId: ToolCallId
           MarkerText: string
           CallGap: TranscriptGap
           ResultGap: TranscriptGap
           ConcernPlacement: ConcernPlacementBatch option |}
    | SessionStartedAtBound of
        {| SessionId: SessionId
           StartedAt: System.DateTimeOffset |}
