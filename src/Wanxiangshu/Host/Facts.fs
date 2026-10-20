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
    let key (fence: TaskDeliveryFence) =
        fence.SessionId, fence.ProviderRun, fence.ToolCallId

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

        if not taskValid then
            Error "delivery task or retirement reference is invalid"
        elif
            not (
                nonBlank (SessionId.value fence.SessionId)
                && nonBlank (LogicalRunId.value fence.LogicalRunId)
                && nonBlank (AuthorityRootUserMessageId.value fence.AuthorityRoot)
                && nonBlank (PhysicalUserMessageId.value fence.PhysicalUserMessageId)
                && nonBlank (ProviderRunIdentity.value fence.ProviderRun)
                && nonBlank (ToolCallId.value fence.ToolCallId)
            )
        then
            Error "delivery fence is incomplete"
        elif
            not (
                nonBlank witness.FormalText
                && nonBlank witness.ViewDeclaration
                && nonBlank witness.View
            )
        then
            Error "delivery requires pre-call formal prose and materialized view"
        elif sha256 witness.View <> witness.MaterialDigest then
            Error "delivery material digest mismatch"
        else
            Ok()

    /// 折叠侧按 (SessionId, ProviderRun, ToolCallId) 三元组存见证，所以同一
    /// (SessionId, ProviderRun) 下并存多条见证是正常状态（同轮重试换了 toolCallId）。
    let private underRun
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (witnesses: Map<SessionId * ProviderRunIdentity * ToolCallId, TaskDeliveryWitness>)
        =
        witnesses
        |> Map.toList
        |> List.choose (fun ((session, run, _), witness) ->
            if session = sessionId && run = providerRun then
                Some witness
            else
                None)

    /// 交付材料一致（同一份交付物）。散文属于各次尝试，不参与一致判定。
    let private sameMaterial (a: TaskDeliveryWitness) (b: TaskDeliveryWitness) =
        a.MaterialDigest = b.MaterialDigest && a.View = b.View

    /// 精确围栏读取：先按 (SessionId, ProviderRun, ToolCallId) 精确命中；
    /// 三元组命中而围栏其余字段不一致时拒绝（与折叠侧同键异材料的拒绝一致）。
    /// 未命中时按 (SessionId, ProviderRun) 取全部见证并按完整围栏筛选。
    let tryFindByFence
        (fence: TaskDeliveryFence)
        (witnesses: Map<SessionId * ProviderRunIdentity * ToolCallId, TaskDeliveryWitness>)
        =
        match Map.tryFind (key fence) witnesses with
        | Some witness when witness.Fence = fence -> Some witness
        | Some _ -> None
        | None ->
            underRun fence.SessionId fence.ProviderRun witnesses
            |> List.filter (fun witness -> witness.Fence = fence)
            |> function
                | [ witness ] -> Some witness
                | _ -> None

    /// 围栏子集读取：调用方只掌握部分围栏字段时使用。
    /// 命中一条即返回；命中多条且交付材料一致时返回其中一条；
    /// 材料不一致时拒绝（不同围栏的材料不一致不得被随意挑一条）。
    let tryFindByFenceFields
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId option)
        (authorityRoot: AuthorityRootUserMessageId option)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (logicalRunId: LogicalRunId option)
        (task: TaskDeliveryTask option)
        (witnesses: Map<SessionId * ProviderRunIdentity * ToolCallId, TaskDeliveryWitness>)
        =
        let matches (witness: TaskDeliveryWitness) =
            let fence = witness.Fence

            (toolCallId.IsNone || fence.ToolCallId = toolCallId.Value)
            && (authorityRoot.IsNone || fence.AuthorityRoot = authorityRoot.Value)
            && (physicalUserMessageId.IsNone
                || fence.PhysicalUserMessageId = physicalUserMessageId.Value)
            && (logicalRunId.IsNone || fence.LogicalRunId = logicalRunId.Value)
            && (task.IsNone || fence.Task = task.Value)

        underRun sessionId providerRun witnesses
        |> List.filter matches
        |> function
            | [] -> None
            | [ witness ] -> Some witness
            | many ->
                let first = List.head many

                if List.forall (sameMaterial first) many then
                    Some first
                else
                    None

    /// 粗粒度读取：不再因为同一 (SessionId, ProviderRun) 下"不止一条"就整体丢弃证据。
    /// 需要指定某一次调用时用 tryFindByFence。
    let tryFind
        sessionId
        providerRun
        (witnesses: Map<SessionId * ProviderRunIdentity * ToolCallId, TaskDeliveryWitness>)
        =
        tryFindByFenceFields sessionId providerRun None None None None None witnesses

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
