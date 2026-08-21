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

/// `ActivatePrefixEpoch` 的载荷：合成 companion memory 替换物理前缀的指令（COMPANION-009）。
///
/// `SyntheticMessageId` 复用快照自己的 id（CTX-012：该 id 在候选构建时固定，provider 在本
/// epoch 已见过；再派生一次就是同一身份的第二个构建点，任何漂移都会让后续每个请求多付一次
/// 冷边界）。`Memory` 是已解析的 FrozenRecordPrefix 经 companion memory preamble 包裹后的
/// 低信任上下文。`CutoffExclusive` 是 canonical XTrace semantic-turn cutoff；
/// effectful Host adapter 通过 stable message identity 执行真实删除，不能把这个数
/// 直接解释成当前 request-local provider 数组下标。
type PrefixActivation =
    { SyntheticMessageId: string
      Memory: string
      CutoffExclusive: int }

/// Domain 镜像的 BlogFrame 种类（Entry/Squash）。定义在 Domain 以避免引用 Journal。
/// 故意不叫 `BlogFrameKind`：Journal 权威 fold 类型同名；`open Wanxiangshu.Domain`
/// 会污染 Journal（如 Fold.fs）的无限定解析。
[<RequireQualifiedAccess>]
type ProjectionBlogFrameKind =
    | Entry
    | Squash

/// 已解析正文的 Y 帧：digest 用 hex string，便于 attempt-local 快照与测试构造。
type ResolvedBlogFrame =
    { Kind: ProjectionBlogFrameKind
      Digest: string
      Body: string }

/// HOST-006：Host compaction reanchor 的观察事实（Domain 形态）。
type HostReanchorFact =
    { PreviousEpochId: string
      NextEpochId: string
      ObservedCompactionRunId: string }

/// `InsertBlogFrames` 载荷：请求种类与重建 Companion 投影所需的旁路输入。
///
/// 帧正文在 `Snapshot.BlogFrames`。Tips / delta / session 身份不能仅从帧列表推出，
/// 故放在意图载荷；渲染委托 `CompanionProjectionBuilder.build`（唯一形状源）。
type BlogFramesIntent =
    {
        RequestKind: string
        /// Squash 时截取的帧数；normal 忽略。
        SquashFrameCount: int
        BloggerSessionId: string
        FrameEpoch: int64
        /// Normal 路径的 combined delta：`(messageId, typed items)`。Squash 为 `None`。
        PhysicalDelta: (string * BloggerDeltaItem list) option
        /// ENFORCER-071：`(tipField, cycleId)`，oldest → newest。
        PreviousTips: (string * string) list
        /// PROMPT-019：已本地化的 Companion normal / squash 指令行。
        NormalInstructionLines: string list
        SquashInstructionLines: string list
    }

/// `InsertRepair` 载荷：InteractionRepair 的幂等键。
type RepairIntent = { RequestKey: string }

/// STRENGTH-009/016: StrengthReplica base selection. `LocalizedMessages` are a
/// semantic-equivalent provider wire representation whose owner tool-call IDs were
/// stripped and deterministically localized for this decision at the Host boundary.
/// The renderer never receives owner-local wire identity and does not invent a
/// general Semantic→Wire conversion (VERIFY-007).
type StrengthMirrorIntent =
    private
        { DecisionId: StrengthDecisionId
          TargetProviderRun: ProviderRunIdentity
          SemanticDigest: string
          LocalizedMessages: ProviderProjection.WireMessage list }

[<RequireQualifiedAccess>]
type StrengthFrameVisibility =
    | Candidate of targetProviderRun: ProviderRunIdentity * currentProviderRun: ProviderRunIdentity
    | Promoted of targetProviderRun: ProviderRunIdentity * isReplicaRequest: bool
    | ReplicaLocal

[<RequireQualifiedAccess>]
type StrengthFrameAnchor =
    | Append
    | BeforeMessageIndex of index: int

type StrengthFrameInsertion =
    private
        { OwnerSessionId: SessionId
          DecisionId: StrengthDecisionId
          FrameDigest: string
          Bundle: StrengthFrameBundle
          Visibility: StrengthFrameVisibility
          Anchor: StrengthFrameAnchor }

type StrengthFramesIntent =
    private
        { Items: StrengthFrameInsertion list }

/// PROJ-002：一次 attempt 的只读投影快照——DSL 核心输入（PROJ-002）。
///
/// attempt-local：字段覆盖一次 provider attempt 的投影输入。PROJ-008 step 3a 在
/// 既有 CurrentProjection / CommittedPrefix 上追加 BlogFrames / TransportMessages /
/// HostReanchor（DSL-003 消费者驱动）。
///
/// `CommittedPrefix` 是 Journal `ActivePrefixEpoch.Snapshot` 的 Domain 形态——
/// `ActivePrefixEpoch` 整体（EpochId / ReanchoredRuns / fold 校验）留在 Journal，
/// Domain 只取可表达的 `PrefixSnapshot`（与 `PrefixProbeSelection` 相同的拆分）。
/// DSL-state-combination: domain — optional committed-prefix/reanchor facets
/// are projection evidence for one provider attempt; no field represents a
/// next-action stage.
type ProjectionSnapshot =
    {
        /// X 当前 provider-visible 语义投影（transform 边界 `decodeMessageView |> toSemantic`）。
        CurrentProjection: ProviderProjection.ProviderSemanticProjection
        /// 已提交前缀快照。`None` = 从未提交，或 reanchor 已退休（HOST-006）——
        /// 两者都是「发送物理历史」，与 `KeepPhysicalPrefix` 同义。
        CommittedPrefix: PrefixSnapshot option
        /// Y 有效帧（已解析正文）。`InsertBlogFrames` 的渲染输入。
        BlogFrames: ResolvedBlogFrame list
        /// transport-only 消息的 host id 集合（COMPANION-012）。`SuppressTransportOnly` 输入。
        TransportMessages: Set<string>
        /// Host compaction reanchor 观察。`ReanchorAfterCompaction` 的事实侧；wire 渲染 no-op。
        HostReanchor: HostReanchorFact option
    }

/// PROJ-005：功能模块对投影的唯一合法表达。
///
/// 功能模块只声明意图，不得直接接收/改写 `Message list`（PROJ-001）。意图交给
/// `ProjectionPlanner` 排序与判冲突，再由 `ProjectionRenderer` 统一渲染（PROJ-004）。
[<RequireQualifiedAccess>]
type ProjectionIntent =
    /// 无 X 恢复时兜底：物理前缀原样。
    | KeepPhysicalPrefix
    /// X probe 已提交并成为 active snapshot：合成 companion memory 替换物理前缀。
    | ActivatePrefixEpoch of PrefixActivation
    /// Y 有效帧（Entry/Squash）插入历史槽；正文取自 Snapshot.BlogFrames。
    | InsertBlogFrames of BlogFramesIntent
    /// Interaction Repair 回合：追加协议修复指令。
    | InsertRepair of RepairIntent
    /// STRENGTH-009: StrengthReplica-only base selection; mutually exclusive
    /// with normal Work prefix selection.
    | UseStrengthMirror of StrengthMirrorIntent
    /// STRENGTH-009: Candidate/Promoted/Replica-local frame insertion. The
    /// visibility/anchor is explicit; renderer never guesses from provenance.
    | InsertStrengthFrames of StrengthFramesIntent
    /// transport-only 消息剔除（COMPANION-012）；目标 id 取自 Snapshot.TransportMessages。
    | SuppressTransportOnly
    /// ContextReanchored → Snapshot=None；wire 字节 no-op。
    | ReanchorAfterCompaction

/// STRENGTH-009/016: public factories for private Strength intent payloads.
/// Fable exports as `ProjectionIntentModule_*`.
[<RequireQualifiedAccess>]
module ProjectionIntent =

    let useStrengthMirror
        (decisionId: StrengthDecisionId)
        (targetRun: ProviderRunIdentity)
        (semanticDigest: string)
        (localizedMessages: ProviderProjection.WireMessage list)
        : ProjectionIntent =
        ProjectionIntent.UseStrengthMirror
            { DecisionId = decisionId
              TargetProviderRun = targetRun
              SemanticDigest = semanticDigest
              LocalizedMessages = localizedMessages }

    let private framesIntent (insertion: StrengthFrameInsertion) : ProjectionIntent =
        ProjectionIntent.InsertStrengthFrames { Items = [ insertion ] }

    let strengthCandidate
        (ownerSession: SessionId)
        (decisionId: StrengthDecisionId)
        (targetRun: ProviderRunIdentity)
        (currentRun: ProviderRunIdentity)
        (bundle: StrengthFrameBundle)
        : ProjectionIntent =
        framesIntent
            { OwnerSessionId = ownerSession
              DecisionId = decisionId
              FrameDigest = bundle.Digest
              Bundle = bundle
              Visibility = StrengthFrameVisibility.Candidate(targetRun, currentRun)
              Anchor = StrengthFrameAnchor.Append }

    let strengthPromoted
        (ownerSession: SessionId)
        (decisionId: StrengthDecisionId)
        (targetRun: ProviderRunIdentity)
        (beforeIndex: int)
        (isReplicaRequest: bool)
        (bundle: StrengthFrameBundle)
        : ProjectionIntent =
        framesIntent
            { OwnerSessionId = ownerSession
              DecisionId = decisionId
              FrameDigest = bundle.Digest
              Bundle = bundle
              Visibility = StrengthFrameVisibility.Promoted(targetRun, isReplicaRequest)
              Anchor = StrengthFrameAnchor.BeforeMessageIndex beforeIndex }

    let strengthReplicaLocal
        (ownerSession: SessionId)
        (decisionId: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        : ProjectionIntent =
        framesIntent
            { OwnerSessionId = ownerSession
              DecisionId = decisionId
              FrameDigest = bundle.Digest
              Bundle = bundle
              Visibility = StrengthFrameVisibility.ReplicaLocal
              Anchor = StrengthFrameAnchor.Append }

/// PROJ-006：同锚意图冲突。fail-closed——禁止依赖注册顺序隐式选边。
/// DSL-class: Decision — planner refusal taxonomy for conflicting ProjectionIntent sets.
[<RequireQualifiedAccess>]
type ProjectionConflict =
    /// 前缀锚同时被两个互斥意图选择（Keep vs Activate，或载荷不等的两次 Activate）。
    | ConflictingPrefixSelection of ProjectionIntent * ProjectionIntent
    /// 两条 `InsertBlogFrames` 载荷不等。
    | ConflictingBlogFrames
    /// 两条 `InsertRepair` 的 RequestKey 不等。
    | ConflictingRepair
    /// `ActivatePrefixEpoch` 与 `ReanchorAfterCompaction` 同批出现。
    | ConflictingPrefixLifecycle
    /// Same Strength decision appeared with non-identical frame material/anchor.
    | ConflictingStrengthFrames of StrengthDecisionId
    /// Candidate may only render into the ProviderRun it was Prepared for.
    | StrengthCandidateWrongTarget of StrengthDecisionId
    /// Promoted replay is owner history; explicit replay into Replica is reflection.
    | StrengthPromotedReplicaReflection of StrengthDecisionId
    /// Visibility and anchor disagree (for example Candidate BeforeIndex).
    | InvalidStrengthAnchor of StrengthDecisionId
    /// Intent frame digest must be the semantic bundle digest.
    | StrengthFrameDigestMismatch of StrengthDecisionId
