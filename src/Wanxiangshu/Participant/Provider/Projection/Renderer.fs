namespace Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// PROJ-008：Domain 侧冻结常量。
///
/// `EnforcerHost.RepairInstruction` 仍是本模块英文单源。HOST-013 pair guideline 与
/// REVIEW-003 challenge 的 prose 在 `resources/provider/`；生产路径按 session
/// 语言经 ProviderProse 装载，禁止第二处字面量。
[<RequireQualifiedAccess>]
module ProjectionConstants =
    /// InteractionRepair 协议修复指令（ENFORCER-060/061）。Domain 单源。
    let RepairInstruction =
        "# Protocol repair\n\nCall the chronicle tool exactly once with a non-empty entry. Do not answer in prose."

    /// HOST-013 pair guideline semantic path (PROMPT-019). Prose lives in
    /// `resources/provider/host/pair-programming-guideline/{en,zh-CN}.md`.
    [<Literal>]
    let PairProgrammingGuidelinePath = "host/pair-programming-guideline"

/// PROJ-004：渲染结果——写回 Host 的指令形态。
[<RequireQualifiedAccess>]
type RenderedPrefix =
    /// 物理前缀原样（无替换）。
    | PhysicalPrefix
    /// 合成前缀：`PrefixActivation` 头部替换前 `DropLeading` 条。
    | SyntheticPrefix of PrefixActivation

/// PROJ-004：Canonical Renderer 一次产出——wire 正文 + Host 写回侧信道。
/// `HostMessageIds` / `HostIsPhysical` 与 `Messages` 等长；None = 本条无代数合成 id。
type RenderedMessages =
    { Messages: ProviderProjection.WireMessage list
      HostMessageIds: string option list
      HostIsPhysical: bool list }

[<RequireQualifiedAccess>]
module ProjectionRenderer =

    let private isPrefixIntent (intent: ProjectionIntent) =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix
        | ProjectionIntent.ActivatePrefixEpoch _
        | ProjectionIntent.UseStrengthMirror _ -> true
        | _ -> false

    /// PROJ-004：把已排序意图渲染成写回指令。
    ///
    /// Planner 保证至多一个前缀意图；多意图列表时只读取前缀槽。
    /// UseStrengthMirror is a base selection for StrengthReplica but does not
    /// produce a Host prefix-writeback instruction — Host keeps PhysicalPrefix.
    let renderPrefix (intents: ProjectionIntent list) : RenderedPrefix =
        match intents |> List.tryFind isPrefixIntent with
        | None
        | Some ProjectionIntent.KeepPhysicalPrefix
        | Some(ProjectionIntent.UseStrengthMirror _) -> RenderedPrefix.PhysicalPrefix
        | Some(ProjectionIntent.ActivatePrefixEpoch activation) -> RenderedPrefix.SyntheticPrefix activation
        | Some _ -> invalidOp "unreachable: prefix filter admits only Keep/Activate/Mirror"

    /// wire 层视图：合成头部 + 保留尾部。
    ///
    /// 与「写回 Host 后再 `decodeMessageView`」的视图一致（bookkeeping part 两侧都被
    /// 丢弃），因此 digest/seal 与测试可以在这个纯函数上断言，无需触碰 Host obj。
    ///
    /// `ProviderProjection.WireMessage.Role` 是 **string**（provider wire 角色：
    /// `"user"` / `"assistant"`），不是 `Kernel.Role` 代理角色枚举。构造时写明
    /// 目标类型，避免与 `AgentRunResult.Role: Role` 等同名字段记录类型混淆。
    let renderMessages
        (messages: ProviderProjection.WireMessage list)
        (rendered: RenderedPrefix)
        : ProviderProjection.WireMessage list =
        match rendered with
        | RenderedPrefix.PhysicalPrefix -> messages
        | RenderedPrefix.SyntheticPrefix activation ->
            if activation.DropLeading > List.length messages then
                invalidArg "DropLeading" "prefix cutoff exceeds the current message view"

            let head: ProviderProjection.WireMessage =
                { Role = "user"
                  Parts = [ ProviderProjection.WireText activation.Memory ] }

            head :: List.skip activation.DropLeading messages

    let private textMessage (role: string) (text: string) : ProviderProjection.WireMessage =
        let message: ProviderProjection.WireMessage =
            { Role = role
              Parts = [ ProviderProjection.WireText text ] }

        message

    /// COMPANION-005：`InsertBlogFrames` 的唯一形状源是 `CompanionProjectionBuilder`。
    /// Builder 只在此调用一次；真实 `sha256` 产出 MessageId，经 Host 侧信道写回（PROJ-004）。
    ///
    /// - 完整 Companion 重建（delta / tips / squash）：用 Builder 计划整体替换 base
    ///   （生产 rebuild 的 base 为空）。
    /// - 仅帧 smoke（无 delta/tips/squash）：无帧则 no-op；有帧时在前缀头之后插入
    ///   包裹后的帧（兼容 Activate → BlogFrames 的 fold）。
    let private applyBlogFrames
        (sha256: string -> string)
        (snapshot: ProjectionSnapshot)
        (intent: BlogFramesIntent)
        (acc: RenderedMessages)
        : RenderedMessages =
        let hasDelta = Option.isSome intent.PhysicalDelta
        let hasTips = not (List.isEmpty intent.PreviousTips)

        let isSquash =
            intent.RequestKind.Equals("squash", System.StringComparison.OrdinalIgnoreCase)

        let fullCompanionRebuild = hasDelta || hasTips || isSquash

        match snapshot.BlogFrames, fullCompanionRebuild with
        | [], false -> acc
        | frames, _ ->
            let kind =
                if isSquash then
                    CompanionRequestKind.Squash intent.SquashFrameCount
                else
                    CompanionRequestKind.Normal

            let frameBodies =
                frames |> List.map (fun frame -> BlobDigest.create frame.Digest, frame.Body)

            let bloggerSessionId = SessionId.create intent.BloggerSessionId
            let frameEpoch = FrameEpochId.create intent.FrameEpoch

            let plan =
                CompanionProjectionBuilder.build
                    sha256
                    bloggerSessionId
                    frameEpoch
                    kind
                    frameBodies
                    intent.PhysicalDelta
                    intent.PreviousTips
                    intent.NormalInstructionLines
                    intent.SquashInstructionLines

            let companionMessages = plan.Messages

            let companionWires: ProviderProjection.WireMessage list =
                companionMessages |> List.map (fun msg -> textMessage msg.Role msg.Text)

            let companionIds = companionMessages |> List.map (fun msg -> Some msg.MessageId)
            let companionPhysical = companionMessages |> List.map (fun msg -> msg.IsPhysical)

            if fullCompanionRebuild then
                { Messages = companionWires
                  HostMessageIds = companionIds
                  HostIsPhysical = companionPhysical }
            else
                match acc.Messages with
                | [] ->
                    { Messages = companionWires
                      HostMessageIds = companionIds
                      HostIsPhysical = companionPhysical }
                | head :: tail ->
                    { Messages = head :: companionWires @ tail
                      HostMessageIds = List.head acc.HostMessageIds :: companionIds @ List.tail acc.HostMessageIds
                      HostIsPhysical = List.head acc.HostIsPhysical :: companionPhysical @ List.tail acc.HostIsPhysical }

    let private appendSynthetic (role: string) (text: string) (acc: RenderedMessages) : RenderedMessages =
        { Messages = acc.Messages @ [ textMessage role text ]
          HostMessageIds = acc.HostMessageIds @ [ None ]
          HostIsPhysical = acc.HostIsPhysical @ [ false ] }

    /// Activate 合成前缀：头部无代数 MessageId；尾部侧信道按 DropLeading 截齐。
    let private applyActivate (activation: PrefixActivation) (acc: RenderedMessages) : RenderedMessages =
        let rendered =
            renderMessages acc.Messages (RenderedPrefix.SyntheticPrefix activation)

        let drop = activation.DropLeading
        let headId: string option = None
        let headPhysical = false

        { Messages = rendered
          HostMessageIds = headId :: List.skip drop acc.HostMessageIds
          HostIsPhysical = headPhysical :: List.skip drop acc.HostIsPhysical }

    /// Suppress：与 wire 同步裁剪侧信道（按同样 assistant 丢弃规则）。
    let private applySuppressWithIds (snapshot: ProjectionSnapshot) (acc: RenderedMessages) : RenderedMessages =
        if Set.isEmpty snapshot.TransportMessages then
            acc
        else
            let budget = Set.count snapshot.TransportMessages

            let rec loop
                (remaining: (ProviderProjection.WireMessage * string option * bool) list)
                (toDrop: int)
                (accMsgs: ProviderProjection.WireMessage list)
                (accIds: string option list)
                (accPhys: bool list)
                =
                match remaining, toDrop with
                | [], _ ->
                    { Messages = List.rev accMsgs
                      HostMessageIds = List.rev accIds
                      HostIsPhysical = List.rev accPhys }
                | _, 0 ->
                    let restMsgs, restIds, restPhys =
                        remaining
                        |> List.fold (fun (ms, is, ps) (m, i, p) -> m :: ms, i :: is, p :: ps) ([], [], [])

                    { Messages = List.rev accMsgs @ List.rev restMsgs
                      HostMessageIds = List.rev accIds @ List.rev restIds
                      HostIsPhysical = List.rev accPhys @ List.rev restPhys }
                | (msg, _, _) :: tail, n when msg.Role = "assistant" -> loop tail (n - 1) accMsgs accIds accPhys
                | (msg, id, phys) :: tail, n -> loop tail n (msg :: accMsgs) (id :: accIds) (phys :: accPhys)

            let zipped = List.zip3 acc.Messages acc.HostMessageIds acc.HostIsPhysical

            loop zipped budget [] [] []

    /// STRENGTH-009: replace base messages with frozen owner wire messages.
    /// Host identity/physical channels reset — mirror bytes are not Host-owned.
    let private applyStrengthMirror (mirror: StrengthMirrorIntent) (_acc: RenderedMessages) : RenderedMessages =
        { Messages = mirror.LocalizedMessages
          HostMessageIds = mirror.LocalizedMessages |> List.map (fun _ -> None)
          HostIsPhysical = mirror.LocalizedMessages |> List.map (fun _ -> false) }

    /// Expand one StrengthFrameBundle into concurrent tool-call + tool-result message pairs.
    /// Each batch → one assistant (all WireToolCall) then one tool (all WireToolResult).
    let private expandStrengthBundle
        (sha256: string -> string)
        (insertion: StrengthFrameInsertion)
        : ProviderProjection.WireMessage list * string option list * bool list =
        let owner = insertion.OwnerSessionId
        let decisionId = insertion.DecisionId
        let digest = insertion.Bundle.Digest

        let renderedBatches =
            insertion.Bundle.Batches
            |> List.map (fun batch ->
                let callParts, resultParts =
                    batch.Exchanges
                    |> List.mapi (fun exchangeIndex exchange ->
                        let exchangeOrdinal = exchangeIndex + 1

                        let callIdText =
                            StrengthFrame.wireToolCallId
                                sha256
                                owner
                                decisionId
                                batch.RequestOrdinal
                                exchangeOrdinal
                                digest

                        let callId = ToolCallId.create callIdText

                        let callPart =
                            ProviderProjection.WireToolCall(callId, exchange.ToolName, exchange.CanonicalArguments)

                        let resultPart = ProviderProjection.WireToolResult(callId, exchange.CanonicalResult)

                        callPart, resultPart)
                    |> List.unzip

                let assistant: ProviderProjection.WireMessage =
                    { Role = "assistant"
                      Parts = callParts }

                let tool: ProviderProjection.WireMessage = { Role = "tool"; Parts = resultParts }

                let callMessageId =
                    StrengthFrame.hostMessageId sha256 owner decisionId batch.RequestOrdinal "call" digest

                let resultMessageId =
                    StrengthFrame.hostMessageId sha256 owner decisionId batch.RequestOrdinal "result" digest

                [ assistant; tool ], [ Some callMessageId; Some resultMessageId ])

        let batchMessages = renderedBatches |> List.collect fst
        let ids = renderedBatches |> List.collect snd
        let physical = batchMessages |> List.map (fun _ -> false)
        batchMessages, ids, physical

    let private spliceBefore
        (index: int)
        (extraMsgs: ProviderProjection.WireMessage list)
        (extraIds: string option list)
        (extraPhys: bool list)
        (acc: RenderedMessages)
        : RenderedMessages =
        let idx =
            if index < 0 then
                0
            elif index > List.length acc.Messages then
                List.length acc.Messages
            else
                index

        let beforeMsgs, afterMsgs = List.splitAt idx acc.Messages
        let beforeIds, afterIds = List.splitAt idx acc.HostMessageIds
        let beforePhys, afterPhys = List.splitAt idx acc.HostIsPhysical

        { Messages = beforeMsgs @ extraMsgs @ afterMsgs
          HostMessageIds = beforeIds @ extraIds @ afterIds
          HostIsPhysical = beforePhys @ extraPhys @ afterPhys }

    let private applyStrengthFrames
        (sha256: string -> string)
        (intent: StrengthFramesIntent)
        (acc: RenderedMessages)
        : RenderedMessages =
        let before, append =
            intent.Items
            |> List.partition (fun insertion ->
                match insertion.Anchor with
                | StrengthFrameAnchor.BeforeMessageIndex _ -> true
                | StrengthFrameAnchor.Append -> false)

        let beforeInApplicationOrder =
            before
            |> List.sortByDescending (fun insertion ->
                match insertion.Anchor with
                | StrengthFrameAnchor.BeforeMessageIndex index -> index, StrengthDecisionId.value insertion.DecisionId
                | StrengthFrameAnchor.Append -> -1, "")

        let appendInApplicationOrder =
            append
            |> List.sortBy (fun insertion -> StrengthDecisionId.value insertion.DecisionId)

        (acc, beforeInApplicationOrder @ appendInApplicationOrder)
        ||> List.fold (fun state insertion ->
            let msgs, ids, phys = expandStrengthBundle sha256 insertion

            match insertion.Anchor with
            | StrengthFrameAnchor.Append ->
                { Messages = state.Messages @ msgs
                  HostMessageIds = state.HostMessageIds @ ids
                  HostIsPhysical = state.HostIsPhysical @ phys }
            | StrengthFrameAnchor.BeforeMessageIndex index -> spliceBefore index msgs ids phys state)

    let private applyOne
        (sha256: string -> string)
        (snapshot: ProjectionSnapshot)
        (acc: RenderedMessages)
        (intent: ProjectionIntent)
        : RenderedMessages =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix -> acc
        | ProjectionIntent.ActivatePrefixEpoch activation -> applyActivate activation acc
        | ProjectionIntent.UseStrengthMirror mirror -> applyStrengthMirror mirror acc
        | ProjectionIntent.InsertBlogFrames payload -> applyBlogFrames sha256 snapshot payload acc
        | ProjectionIntent.InsertRepair _ -> appendSynthetic "user" ProjectionConstants.RepairInstruction acc
        | ProjectionIntent.InsertStrengthFrames payload -> applyStrengthFrames sha256 payload acc
        | ProjectionIntent.SuppressTransportOnly -> applySuppressWithIds snapshot acc
        // wire no-op：CommittedPrefix=None 的语义由 Coordinator 填 Snapshot；此处不改字节。
        | ProjectionIntent.ReanchorAfterCompaction -> acc

    let private emptyRendered (baseMessages: ProviderProjection.WireMessage list) : RenderedMessages =
        { Messages = baseMessages
          HostMessageIds = baseMessages |> List.map (fun _ -> None)
          HostIsPhysical = baseMessages |> List.map (fun _ -> false) }

    /// PROJ-004：注入 sha256，一次 fold 产出 wire + Host MessageId / IsPhysical 侧信道。
    /// Builder 仅在 `InsertBlogFrames` 路径调用一次。plan 冲突 fail-closed。
    let renderMessagesWithHostIds
        (sha256: string -> string)
        (snapshot: ProjectionSnapshot)
        (baseMessages: ProviderProjection.WireMessage list)
        (intents: ProjectionIntent list)
        : RenderedMessages =
        match ProjectionPlanner.plan intents with
        | Error _ -> invalidOp "ProjectionRenderer.renderMessagesWithHostIds requires a conflict-free intent set"
        | Ok ordered ->
            (emptyRendered baseMessages, ordered)
            ||> List.fold (fun acc intent -> applyOne sha256 snapshot acc intent)

    /// PROJ-008 step 3a：兼容入口——返回 wire list（默认恒等 sha256；测试不要求 MessageId）。
    let renderMessagesWithIntents
        (snapshot: ProjectionSnapshot)
        (baseMessages: ProviderProjection.WireMessage list)
        (intents: ProjectionIntent list)
        : ProviderProjection.WireMessage list =
        (renderMessagesWithHostIds id snapshot baseMessages intents).Messages

    /// CTX-011 step 5：候选 cutoff 处 X 当前前缀的 digest 证明。
    ///
    /// attempt-local（PROJ-008 迁移顺序第 2 步）：只对本次 attempt 的当前投影做
    /// cutoff 截断后计算语义 digest。这是「功能模块声明意图、渲染器负责投影」的
    /// 边界落点——调用方不再直接 `List.truncate` 消息列表（PROJ-001）。
    ///
    /// `List.truncate` 语义：cutoff 越界时返回全量消息（不报错），与
    /// `PrefixProbeSelection` 的 `candidateCutoff = min coverableCutoff requestStartCutoff`
    /// 组合后不会产生非法 cutoff。
    let cutoffDigest (sha256: string -> string) (snapshot: ProjectionSnapshot) (cutoff: int) : string =
        let truncated =
            { snapshot.CurrentProjection with
                Messages = snapshot.CurrentProjection.Messages |> List.truncate cutoff }

        sha256 (ProviderProjection.renderSemantic truncated)
