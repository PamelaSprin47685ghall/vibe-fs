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
