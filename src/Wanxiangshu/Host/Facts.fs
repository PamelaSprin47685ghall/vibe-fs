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
    let key (fence: TaskDeliveryFence) = fence.SessionId, fence.ProviderRun, fence.ToolCallId

    let validate (sha256: string -> string) (witness: TaskDeliveryWitness) =
        let fence = witness.Fence
        let nonBlank = System.String.IsNullOrWhiteSpace >> not
        let taskValid =
            match fence.Task, witness.RetirementId with
            | TaskDeliveryTask.ChildWork handle, None -> nonBlank handle
            | TaskDeliveryTask.Invocation invocation, None -> nonBlank invocation
            | TaskDeliveryTask.Lifecycle(work, incumbent), Some retirement ->
                nonBlank work && nonBlank incumbent && nonBlank retirement
            | _ -> false
        if not taskValid then Error "delivery task or retirement reference is invalid"
        elif not (nonBlank (SessionId.value fence.SessionId)
                  && nonBlank (LogicalRunId.value fence.LogicalRunId)
                  && nonBlank (AuthorityRootUserMessageId.value fence.AuthorityRoot)
                  && nonBlank (PhysicalUserMessageId.value fence.PhysicalUserMessageId)
                  && nonBlank (ProviderRunIdentity.value fence.ProviderRun)
                  && nonBlank (ToolCallId.value fence.ToolCallId)) then
            Error "delivery fence is incomplete"
        elif not (nonBlank witness.FormalText && nonBlank witness.ViewDeclaration && nonBlank witness.View) then
            Error "delivery requires pre-call formal prose and materialized view"
        elif sha256 witness.View <> witness.MaterialDigest then Error "delivery material digest mismatch"
        else Ok ()

    let tryFind sessionId providerRun (witnesses: Map<SessionId * ProviderRunIdentity * ToolCallId, TaskDeliveryWitness>) =
        witnesses
        |> Map.toList
        |> List.choose (fun ((session, run, _), witness) ->
            if session = sessionId && run = providerRun then Some witness else None)
        |> function
            | [ witness ] -> Some witness
            | _ -> None

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

