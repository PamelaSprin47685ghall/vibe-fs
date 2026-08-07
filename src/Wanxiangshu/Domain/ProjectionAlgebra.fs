namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

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

/// PROJ-005：功能模块对投影的唯一合法表达。
///
/// 功能模块只声明意图，不得直接接收/改写 `Message list`（PROJ-001）。意图交给
/// `ProjectionPlanner` 排序与判冲突，再由 `ProjectionRenderer` 统一渲染（PROJ-004）。
///
/// 阶段 1（PROJ-008 迁移顺序第 1 步：普通 X + ActivePrefixEpoch projection）落地前缀意图；
/// 其余意图按迁移顺序在后续阶段接入：
/// `insertBlogFrames`（第 3 步）、`insertRepair`（第 4 步）、`appendReviewChallenge` /
/// `insertPairProgrammingThought`（第 5 步）、`suppressTransportOnly`（COMPANION-012，
/// 随 Blogger delta 落地）、`reanchorAfterCompaction`（第 6 步）。
[<RequireQualifiedAccess>]
type ProjectionIntent =
    /// 无 X 恢复时兜底：物理前缀原样。
    | KeepPhysicalPrefix
    /// X probe 已提交并成为 active snapshot：合成 companion memory 替换物理前缀。
    | ActivatePrefixEpoch of PrefixActivation

/// PROJ-006：同锚意图冲突。fail-closed——禁止依赖注册顺序隐式选边。
[<RequireQualifiedAccess>]
type ProjectionConflict =
    /// 前缀锚同时被两个互斥意图选择。阶段 1 的前缀意图是互斥选择而非顺序步骤，
    /// 同时出现即 `ProjectionConflict`，由调用方 fail-closed。
    | ConflictingPrefixSelection of ProjectionIntent * ProjectionIntent

[<RequireQualifiedAccess>]
module ProjectionPlanner =

    /// PROJ-006：汇总各功能意图 → 冲突检查 → 有序意图序列。
    ///
    /// 阶段 1 只有一个锚（provider 消息前缀位置），其两个意图是同一锚的互斥选择：
    /// 0 个 = 无 X 恢复（物理原样）；1 个 = 该选择；≥2 个 = 冲突（与注册顺序无关，
    /// 任意顺序都拒绝）。不同锚的意图排序（canonical order，how/projection.md）在
    /// 后续阶段随意图接入。
    let plan (intents: ProjectionIntent list) : Result<ProjectionIntent list, ProjectionConflict> =
        match intents with
        | []
        | [ _ ] -> Ok intents
        | first :: second :: _ -> Error(ProjectionConflict.ConflictingPrefixSelection(first, second))

/// PROJ-004：渲染结果——写回 Host 的指令形态。
[<RequireQualifiedAccess>]
type RenderedPrefix =
    /// 物理前缀原样（无替换）。
    | PhysicalPrefix
    /// 合成前缀：`PrefixActivation` 头部替换前 `DropLeading` 条。
    | SyntheticPrefix of PrefixActivation

[<RequireQualifiedAccess>]
module ProjectionRenderer =

    /// PROJ-004：把已排序意图渲染成写回指令。
    ///
    /// Planner 已保证至多一个前缀意图，因此这里是穷尽匹配而非猜选。
    let renderPrefix (intents: ProjectionIntent list) : RenderedPrefix =
        match intents with
        | []
        | [ ProjectionIntent.KeepPhysicalPrefix ] -> RenderedPrefix.PhysicalPrefix
        | [ ProjectionIntent.ActivatePrefixEpoch activation ] -> RenderedPrefix.SyntheticPrefix activation
        | _ -> invalidOp "unreachable: ProjectionPlanner.plan rejects multiple prefix intents"

    /// wire 层视图：合成头部 + 保留尾部。
    ///
    /// 与「写回 Host 后再 `decodeMessageView`」的视图一致（bookkeeping part 两侧都被
    /// 丢弃），因此 digest/seal 与测试可以在这个纯函数上断言，无需触碰 Host obj。
    let renderMessages
        (messages: ProviderProjection.WireMessage list)
        (rendered: RenderedPrefix)
        : ProviderProjection.WireMessage list =
        match rendered with
        | RenderedPrefix.PhysicalPrefix -> messages
        | RenderedPrefix.SyntheticPrefix activation ->
            if activation.DropLeading > List.length messages then
                invalidArg "DropLeading" "prefix cutoff exceeds the current message view"

            { Role = "user"
              Parts = [ ProviderProjection.WireText activation.Memory ] }
            :: List.skip activation.DropLeading messages
