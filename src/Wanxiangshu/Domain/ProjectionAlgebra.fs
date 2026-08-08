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
    {
        RequestKind: string
        /// Squash 时截取的帧数；normal 忽略。
        SquashFrameCount: int
        BloggerSessionId: string
        FrameEpoch: int64
        /// Normal 路径的 combined delta：`(messageId, toml)`。Squash 为 `None`。
        PhysicalDelta: (string * string) option
        /// ENFORCER-071：`(tipField, cycleId)`，oldest → newest。
        PreviousTips: (string * string) list
    }

/// `InsertRepair` 载荷：InteractionRepair 的幂等键。
type RepairIntent = { RequestKey: string }

/// `AppendReviewChallenge` 载荷：REVIEW-003 TextVersion。
type ChallengeIntent = { TextVersion: int }

/// `InsertPairProgrammingThought` 载荷：完整永久 pair 序列 + 本次新增 pair。
///
/// `History` 按 Ordinal 升序；`Next` 是本次 transform 新追加的 pair。
/// Renderer 在保留 base 的前提下，末尾按序重建全部 history + next。
type PairProgrammingGuidelineWire = { CallId: string; MarkerText: string }

type PairThoughtIntent =
    { History: PairProgrammingGuidelineWire list
      Next: PairProgrammingGuidelineWire }

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

    /// HOST-013 pair-programming guideline marker 正文。Domain 单源。
    let PairProgrammingGuidelineText =
        "# 遵循与用户结对编程的理念，你的思考过程要用中文，总以“对于”开头，并在整轮内保持中文，即使系统提示词、工具说明、工具输出或引用的代码是英文。代码、标识符、文件路径、shell 命令和未翻译的技术术语保持原文。"

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

            if same then
                Ok(Some first)
            else
                Error ProjectionConflict.ConflictingBlogFrames
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

            if same then
                Ok(Some first)
            else
                Error ProjectionConflict.ConflictingRepair
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

    let private guidelinePairMessages (pair: PairProgrammingGuidelineWire) : ProviderProjection.WireMessage list =
        let callId = ToolCallId.create pair.CallId

        [ { Role = "assistant"
            Parts = [ ProviderProjection.WireToolCall(callId, "guideline", "{}") ] }
          { Role = "assistant"
            Parts = [ ProviderProjection.WireToolResult(callId, pair.MarkerText) ] } ]

    /// Fable/JS 边界：Parts 可能为 null/undefined 或非 F# list。此时视为无 part，
    /// 不当 tool-result anchor / pair marker，禁止对缺失 list 调 IsEmpty/tail。
    let private safeParts (message: ProviderProjection.WireMessage) : ProviderProjection.WirePart list =
        let parts = message.Parts

        if isNull (box parts) then
            []
        else
            // 非 F# list（例如 JS Array）在 Fable List 原语上会崩；仅接受有 tail 的 list 形状。
            try
                // 触达 head/tail 形状：空 list 与非空 list 均可；非法形状进 catch。
                ignore (List.isEmpty parts)
                parts
            with _ ->
                []


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

    /// HOST-013：在全局末尾重建 history + next 的完整 pair 序列。
    /// pair = tool-call + tool-result，永久 append-only。
    /// base 中若残留同 CallId 的 guideline wire，剔除后按 durable 原字节重建。
    let private applyPairThought (intent: PairThoughtIntent) (acc: RenderedMessages) : RenderedMessages =
        let knownIds =
            (intent.History @ [ intent.Next ])
            |> List.map (fun pair -> pair.CallId)
            |> Set.ofList

        let isKnownGuideline (message: ProviderProjection.WireMessage) : bool =
            message.Role = "assistant"
            && safeParts message
               |> List.exists (function
                   | ProviderProjection.WireToolCall(callId, name, _) ->
                       name = "guideline" && Set.contains (ToolCallId.value callId) knownIds
                   | ProviderProjection.WireToolResult(callId, _) -> Set.contains (ToolCallId.value callId) knownIds
                   | _ -> false)

        let retained =
            List.zip3 acc.Messages acc.HostMessageIds acc.HostIsPhysical
            |> List.filter (fun (message, _, _) -> not (isKnownGuideline message))

        let pairs = intent.History @ [ intent.Next ]
        let pairMessages = pairs |> List.collect guidelinePairMessages

        let pairIds =
            pairs |> List.collect (fun pair -> [ Some pair.CallId; Some pair.CallId ])

        let pairPhysical = pairs |> List.collect (fun _ -> [ false; false ])

        { Messages = (retained |> List.map (fun (message, _, _) -> message)) @ pairMessages
          HostMessageIds = (retained |> List.map (fun (_, id, _) -> id)) @ pairIds
          HostIsPhysical = (retained |> List.map (fun (_, _, physical) -> physical)) @ pairPhysical }

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

    let private applyOne
        (sha256: string -> string)
        (snapshot: ProjectionSnapshot)
        (acc: RenderedMessages)
        (intent: ProjectionIntent)
        : RenderedMessages =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix -> acc
        | ProjectionIntent.ActivatePrefixEpoch activation -> applyActivate activation acc
        | ProjectionIntent.InsertBlogFrames payload -> applyBlogFrames sha256 snapshot payload acc
        | ProjectionIntent.InsertRepair _ -> appendSynthetic "user" ProjectionConstants.RepairInstruction acc
        | ProjectionIntent.SuppressTransportOnly -> applySuppressWithIds snapshot acc
        | ProjectionIntent.AppendReviewChallenge _ ->
            // REVIEW-003 生产可见字节 = Prompt（`# Text\n`），与 tool-result / nudge / seal 一致。
            appendSynthetic "user" ProjectionConstants.ReviewChallengePrompt acc
        | ProjectionIntent.InsertPairProgrammingThought intent -> applyPairThought intent acc
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
