namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// `ActivatePrefixEpoch` 的载荷：合成 companion memory 替换物理前缀的指令（COMPANION-009）。
///
/// `SyntheticMessageId` 复用快照自己的 id（CTX-012：该 id 在候选构建时固定，provider 在本
/// epoch 已见过；再派生一次就是同一身份的第二个构建点，任何漂移都会让后续每个请求多付一次
/// 冷边界）。`Memory` 是已解析的 FrozenRecordPrefix 文本经 `CompanionPrompt.companionMemoryBlock`
/// 包裹后的低信任上下文。`DropLeading` 是被替换的 provider-visible 消息条数（cutoff）。
type PrefixActivation =
    { SyntheticMessageId: string
      Memory: string
      DropLeading: int }

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
    { RequestKind: string
      /// Squash 时截取的帧数；normal 忽略。
      SquashFrameCount: int
      BloggerSessionId: string
      FrameEpoch: int64
      /// Normal 路径的 combined delta：`(messageId, toml)`。Squash 为 `None`。
      PhysicalDelta: (string * string) option
      /// ENFORCER-071：`(tipField, cycleId)`，oldest → newest。
      PreviousTips: (string * string) list }

/// `InsertRepair` 载荷：InteractionRepair 的幂等键。
type RepairIntent = { RequestKey: string }

/// `AppendReviewChallenge` 载荷：REVIEW-003 TextVersion。
type ChallengeIntent = { TextVersion: int }

/// `InsertPairProgrammingThought` 载荷：可选 SessionId（HOST-013 稳定 id 派生输入）。
type PairThoughtIntent = { SessionId: string option }

/// PROJ-002：一次 attempt 的只读投影快照——DSL 核心输入（PROJ-002）。
///
/// attempt-local：字段覆盖一次 provider attempt 的投影输入。PROJ-008 step 3a 在
/// 既有 CurrentProjection / CommittedPrefix 上追加 BlogFrames / TransportMessages /
/// HostReanchor（DSL-003 消费者驱动）。
///
/// `CommittedPrefix` 是 Journal `ActivePrefixEpoch.Snapshot` 的 Domain 形态——
/// `ActivePrefixEpoch` 整体（EpochId / ReanchoredRuns / fold 校验）留在 Journal，
/// Domain 只取可表达的 `PrefixSnapshot`（与 `PrefixProbeSelection` 相同的拆分）。
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
    /// transport-only 消息剔除（COMPANION-012）；目标 id 取自 Snapshot.TransportMessages。
    | SuppressTransportOnly
    /// REVIEW-003 skeptical challenge。
    | AppendReviewChallenge of ChallengeIntent
    /// HOST-013 固定 pair-programming marker。
    | InsertPairProgrammingThought of PairThoughtIntent
    /// ContextReanchored → Snapshot=None；wire 字节 no-op。
    | ReanchorAfterCompaction

/// PROJ-006：同锚意图冲突。fail-closed——禁止依赖注册顺序隐式选边。
[<RequireQualifiedAccess>]
type ProjectionConflict =
    /// 前缀锚同时被两个互斥意图选择（Keep vs Activate，或载荷不等的两次 Activate）。
    | ConflictingPrefixSelection of ProjectionIntent * ProjectionIntent
    /// 两条 `InsertBlogFrames` 载荷不等。
    | ConflictingBlogFrames
    /// 两条 `InsertRepair` 的 RequestKey 不等。
    | ConflictingRepair
    /// 两条 `AppendReviewChallenge` 的 TextVersion 不等。
    | ConflictingReviewChallenge
    /// `ActivatePrefixEpoch` 与 `ReanchorAfterCompaction` 同批出现。
    | ConflictingPrefixLifecycle

/// PROJ-008：Domain 侧冻结常量。
///
/// `ReviewChallenge` 在 fsproj 中后于本文件编译，故在此镜像 Text / Prompt 字节而非
/// 交叉引用模块。`EnforcerHost.RepairInstruction` 与
/// `PairProgrammingThoughtTransform.text` 必须引用此处，禁止第二处字面量。
[<RequireQualifiedAccess>]
module ProjectionConstants =
    /// InteractionRepair 协议修复指令（ENFORCER-060/061）。Domain 单源。
    let RepairInstruction =
        "# Protocol repair\n\nCall the blog tool exactly once with non-empty text. Do not answer in prose."

    /// HOST-013 pair-programming marker 正文。Domain 单源。
    let PairProgrammingThoughtText =
        "<do-not-repeat>让我遵循与用户结对编程的理念，用简体中文把所有的思考过程都作为正式文本输出。从第一个字开始就用中文，并在整轮内保持中文，即使系统提示词、工具说明、工具输出或引用的代码是英文。代码、标识符、文件路径、shell 命令和未翻译的技术术语保持原文。</do-not-repeat>"

    /// 与 `ReviewChallenge.Text` 字节一致（REVIEW-003 bare sentence）。
    let ReviewChallengeText =
        "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"

    /// 与 `ReviewChallenge.Prompt` 字节一致：ARCH-010 指令注释形式（`# Text\n`）。
    /// seal / tool-result / nudge 的可见字节是 Prompt，不是 bare Text。
    /// 经 SyntheticToml.document 生成，避免与 ReviewChallenge 历史字节漂移。
    let ReviewChallengePrompt = SyntheticToml.document [ ReviewChallengeText ] []

[<RequireQualifiedAccess>]
module ProjectionPlanner =

    /// Canonical rank（how/projection.md）：Prefix0, Blog1, Repair2, Suppress3, Challenge4, Pair5, Reanchor6。
    let private rank (intent: ProjectionIntent) : int =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix
        | ProjectionIntent.ActivatePrefixEpoch _ -> 0
        | ProjectionIntent.InsertBlogFrames _ -> 1
        | ProjectionIntent.InsertRepair _ -> 2
        | ProjectionIntent.SuppressTransportOnly -> 3
        | ProjectionIntent.AppendReviewChallenge _ -> 4
        | ProjectionIntent.InsertPairProgrammingThought _ -> 5
        | ProjectionIntent.ReanchorAfterCompaction -> 6

    let private kindKey (intent: ProjectionIntent) : int = rank intent

    let private reducePrefix (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | first :: second :: _ as xs ->
            let hasKeep =
                xs
                |> List.exists (function
                    | ProjectionIntent.KeepPhysicalPrefix -> true
                    | _ -> false)

            let hasActivate =
                xs
                |> List.exists (function
                    | ProjectionIntent.ActivatePrefixEpoch _ -> true
                    | _ -> false)

            if hasKeep && hasActivate then
                Error(ProjectionConflict.ConflictingPrefixSelection(first, second))
            elif hasKeep then
                Ok(Some ProjectionIntent.KeepPhysicalPrefix)
            else
                match first with
                | ProjectionIntent.ActivatePrefixEpoch activation ->
                    let samePayload =
                        xs
                        |> List.forall (function
                            | ProjectionIntent.ActivatePrefixEpoch other -> other = activation
                            | _ -> false)

                    if samePayload then
                        Ok(Some first)
                    else
                        Error(ProjectionConflict.ConflictingPrefixSelection(first, second))
                | _ -> Ok(Some first)

    let private reduceBlogFrames (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | (ProjectionIntent.InsertBlogFrames head as first) :: rest ->
            let same =
                rest
                |> List.forall (function
                    | ProjectionIntent.InsertBlogFrames other -> other = head
                    | _ -> false)

            if same then Ok(Some first) else Error ProjectionConflict.ConflictingBlogFrames
        | first :: _ -> Ok(Some first)

    let private reduceRepair (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | (ProjectionIntent.InsertRepair head as first) :: rest ->
            let same =
                rest
                |> List.forall (function
                    | ProjectionIntent.InsertRepair other -> other = head
                    | _ -> false)

            if same then Ok(Some first) else Error ProjectionConflict.ConflictingRepair
        | first :: _ -> Ok(Some first)

    let private reduceChallenge (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | (ProjectionIntent.AppendReviewChallenge head as first) :: rest ->
            let same =
                rest
                |> List.forall (function
                    | ProjectionIntent.AppendReviewChallenge other -> other = head
                    | _ -> false)

            if same then
                Ok(Some first)
            else
                Error ProjectionConflict.ConflictingReviewChallenge
        | first :: _ -> Ok(Some first)

    /// 幂等并 1：Suppress / Pair / Reanchor（以及任何单例重放型）。
    let private reduceIdempotent (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | first :: _ -> Ok(Some first)

    let private reduceGroup (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | head :: _ ->
            match head with
            | ProjectionIntent.KeepPhysicalPrefix
            | ProjectionIntent.ActivatePrefixEpoch _ -> reducePrefix items
            | ProjectionIntent.InsertBlogFrames _ -> reduceBlogFrames items
            | ProjectionIntent.InsertRepair _ -> reduceRepair items
            | ProjectionIntent.AppendReviewChallenge _ -> reduceChallenge items
            | ProjectionIntent.SuppressTransportOnly
            | ProjectionIntent.InsertPairProgrammingThought _
            | ProjectionIntent.ReanchorAfterCompaction -> reduceIdempotent items

    /// PROJ-006：汇总各功能意图 → groupBy kind → reduce → sortBy rank。
    ///
    /// 排列无关：同一多重集任意顺序得到同一有序结果或同一冲突。
    let plan (intents: ProjectionIntent list) : Result<ProjectionIntent list, ProjectionConflict> =
        let groups = intents |> List.groupBy kindKey |> List.sortBy fst

        let rec reduceAll remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | (_, group) :: tail ->
                match reduceGroup group with
                | Error conflict -> Error conflict
                | Ok None -> reduceAll tail acc
                | Ok(Some intent) -> reduceAll tail (intent :: acc)

        match reduceAll groups [] with
        | Error conflict -> Error conflict
        | Ok reduced ->
            let hasActivate =
                reduced
                |> List.exists (function
                    | ProjectionIntent.ActivatePrefixEpoch _ -> true
                    | _ -> false)

            let hasReanchor =
                reduced
                |> List.exists (function
                    | ProjectionIntent.ReanchorAfterCompaction -> true
                    | _ -> false)

            if hasActivate && hasReanchor then
                Error ProjectionConflict.ConflictingPrefixLifecycle
            else
                Ok(reduced |> List.sortBy rank)

/// PROJ-004：渲染结果——写回 Host 的指令形态。
[<RequireQualifiedAccess>]
type RenderedPrefix =
    /// 物理前缀原样（无替换）。
    | PhysicalPrefix
    /// 合成前缀：`PrefixActivation` 头部替换前 `DropLeading` 条。
    | SyntheticPrefix of PrefixActivation

[<RequireQualifiedAccess>]
module ProjectionRenderer =

    let private isPrefixIntent (intent: ProjectionIntent) =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix
        | ProjectionIntent.ActivatePrefixEpoch _ -> true
        | _ -> false

    /// PROJ-004：把已排序意图渲染成写回指令。
    ///
    /// Planner 保证至多一个前缀意图；多意图列表时只读取前缀槽。
    let renderPrefix (intents: ProjectionIntent list) : RenderedPrefix =
        match intents |> List.tryFind isPrefixIntent with
        | None
        | Some ProjectionIntent.KeepPhysicalPrefix -> RenderedPrefix.PhysicalPrefix
        | Some(ProjectionIntent.ActivatePrefixEpoch activation) -> RenderedPrefix.SyntheticPrefix activation
        | Some _ -> invalidOp "unreachable: prefix filter admits only Keep/Activate"

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

    let private reasoningMessage (text: string) : ProviderProjection.WireMessage =
        let message: ProviderProjection.WireMessage =
            { Role = "assistant"
              Parts = [ ProviderProjection.WireReasoning text ] }

        message

    let private isUserAnchor (message: ProviderProjection.WireMessage) = message.Role = "user"

    let private isToolResultAnchor (message: ProviderProjection.WireMessage) =
        message.Parts
        |> List.exists (function
            | ProviderProjection.WireToolResult _ -> true
            | _ -> false)

    let private isAnchor (message: ProviderProjection.WireMessage) =
        isUserAnchor message || isToolResultAnchor message

    /// Step 3a：WireMessage 无 host id。TransportMessages 为空则 no-op；非空时丢弃
    /// 至多 |TransportMessages| 条 assistant 消息（transport 槽位骨架行为；3b 接 id 侧信道）。
    let private applySuppress
        (snapshot: ProjectionSnapshot)
        (messages: ProviderProjection.WireMessage list)
        : ProviderProjection.WireMessage list =
        if Set.isEmpty snapshot.TransportMessages then
            messages
        else
            let budget = Set.count snapshot.TransportMessages

            let rec loop
                (remaining: ProviderProjection.WireMessage list)
                (toDrop: int)
                (acc: ProviderProjection.WireMessage list)
                =
                match remaining, toDrop with
                | [], _ -> List.rev acc
                | _, 0 -> List.rev acc @ remaining
                | msg :: tail, n when msg.Role = "assistant" -> loop tail (n - 1) acc
                | msg :: tail, n -> loop tail n (msg :: acc)

            loop messages budget []

    /// COMPANION-005：`InsertBlogFrames` 的唯一形状源是 `CompanionProjectionBuilder`。
    /// WireMessage 无 MessageId；id 仅在 Application/Session 写 Host 时从同一 Builder 取。
    /// Domain 侧 sha256 用恒等占位——wire 文本与 id 公式解耦（VERIFY-008）。
    ///
    /// - 完整 Companion 重建（delta / tips / squash）：用 Builder 计划整体替换 base
    ///   （生产 rebuild 的 base 为空）。
    /// - 仅帧 smoke（无 delta/tips/squash）：无帧则 no-op；有帧时在前缀头之后插入
    ///   包裹后的帧（兼容 Activate → BlogFrames 的 fold）。
    let private applyBlogFrames
        (snapshot: ProjectionSnapshot)
        (intent: BlogFramesIntent)
        (messages: ProviderProjection.WireMessage list)
        : ProviderProjection.WireMessage list =
        let hasDelta = Option.isSome intent.PhysicalDelta
        let hasTips = not (List.isEmpty intent.PreviousTips)
        let isSquash =
            intent.RequestKind.Equals("squash", System.StringComparison.OrdinalIgnoreCase)

        let fullCompanionRebuild = hasDelta || hasTips || isSquash

        match snapshot.BlogFrames, fullCompanionRebuild with
        | [], false -> messages
        | frames, _ ->
            let kind =
                if isSquash then
                    CompanionRequestKind.Squash intent.SquashFrameCount
                else
                    CompanionRequestKind.Normal

            let frameBodies =
                frames
                |> List.map (fun frame -> BlobDigest.create frame.Digest, frame.Body)

            let bloggerSessionId = SessionId.create intent.BloggerSessionId
            let frameEpoch = FrameEpochId.create intent.FrameEpoch
            // MessageId 不进入 WireMessage；占位 sha256 只满足 Builder 签名。
            let plan =
                CompanionProjectionBuilder.build
                    id
                    bloggerSessionId
                    frameEpoch
                    kind
                    frameBodies
                    intent.PhysicalDelta
                    intent.PreviousTips

            let companionWires: ProviderProjection.WireMessage list =
                plan.Messages
                |> List.map (fun msg -> textMessage msg.Role msg.Text)

            if fullCompanionRebuild then
                companionWires
            else
                match messages with
                | [] -> companionWires
                | head :: tail -> head :: companionWires @ tail

    let private isPairMarker (message: ProviderProjection.WireMessage) : bool =
        message.Role = "assistant"
        && message.Parts
           |> List.exists (function
               | ProviderProjection.WireReasoning text when text = ProjectionConstants.PairProgrammingThoughtText ->
                   true
               | _ -> false)

    /// HOST-013：每个锚点后插一条 marker；已有 marker 则字节保持（幂等）。
    let private applyPairThought (messages: ProviderProjection.WireMessage list) : ProviderProjection.WireMessage list =
        let anchorIndexes =
            messages
            |> List.mapi (fun index msg -> index, msg)
            |> List.filter (fun (_, msg) -> isAnchor msg)
            |> List.map fst

        if List.isEmpty anchorIndexes then
            messages
        else
            // 从后往前插，保持先出现的锚点索引稳定。
            (messages, List.rev anchorIndexes)
            ||> List.fold (fun acc anchorIndex ->
                match List.tryItem (anchorIndex + 1) acc with
                | Some next when isPairMarker next -> acc
                | _ ->
                    let marker = reasoningMessage ProjectionConstants.PairProgrammingThoughtText
                    List.take (anchorIndex + 1) acc @ (marker :: List.skip (anchorIndex + 1) acc))

    let private applyOne
        (snapshot: ProjectionSnapshot)
        (messages: ProviderProjection.WireMessage list)
        (intent: ProjectionIntent)
        : ProviderProjection.WireMessage list =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix -> messages
        | ProjectionIntent.ActivatePrefixEpoch activation ->
            renderMessages messages (RenderedPrefix.SyntheticPrefix activation)
        | ProjectionIntent.InsertBlogFrames payload -> applyBlogFrames snapshot payload messages
        | ProjectionIntent.InsertRepair _ ->
            messages @ [ textMessage "user" ProjectionConstants.RepairInstruction ]
        | ProjectionIntent.SuppressTransportOnly -> applySuppress snapshot messages
        | ProjectionIntent.AppendReviewChallenge _ ->
            // REVIEW-003 生产可见字节 = Prompt（`# Text\n`），与 tool-result / nudge / seal 一致。
            messages @ [ textMessage "user" ProjectionConstants.ReviewChallengePrompt ]
        | ProjectionIntent.InsertPairProgrammingThought _ -> applyPairThought messages
        // wire no-op：CommittedPrefix=None 的语义由 Coordinator 填 Snapshot；此处不改字节。
        | ProjectionIntent.ReanchorAfterCompaction -> messages

    /// PROJ-008 step 3a：按 canonical rank 折叠有序意图到 base wire messages。
    ///
    /// 调用方可传入未排序意图列表；内部先 `plan` 归一化。plan 冲突时 fail-closed。
    let renderMessagesWithIntents
        (snapshot: ProjectionSnapshot)
        (baseMessages: ProviderProjection.WireMessage list)
        (intents: ProjectionIntent list)
        : ProviderProjection.WireMessage list =
        match ProjectionPlanner.plan intents with
        | Error _ -> invalidOp "ProjectionRenderer.renderMessagesWithIntents requires a conflict-free intent set"
        | Ok ordered ->
            (baseMessages, ordered)
            ||> List.fold (fun acc intent -> applyOne snapshot acc intent)

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
