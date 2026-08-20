# guidance-delivery — WHAT

## GD-001: 交付前沿与语义覆盖两轴正交分离

Main 会话的 tip 交付必须严格区分两个正交维度：
1. `TipDeliveryFrontier`：记录哪些 `TipOccurrence` 已向该 Main 完成过首次处置手册交付。基于 occurrence 单调推进，属于持久化事实，在 `ContextReanchored` 事件发生时不重置；
2. `TipSemanticCoverage`：记录哪些 `TipName` 的完整处置手册（`main.md`）语义当前仍可在 provider horizon 中被有效恢复。基于 TipName 维度且具有 horizon 局部性，在 `ContextReanchored` 时重置清空。

严禁将两者压缩为一个单一的持久化布尔标志。

## GD-002: 首次交付呈现 Full 处置手册全文并推进 Frontier

当目标 `TipOccurrence` 不在当前 Main 的 `TipDeliveryFrontier` 中，或当前 `TipSemanticCoverage` 表明该 TipName 全文不可从 horizon 恢复时，系统生成 `TipPresentation.Full` 形式：包含 `# Enforcer Tip` 头部、`tip = "<name>"` 标识以及按 owner 语言选择的完整 `main.md` 处置正文。首次生成 Full 交付时，必须原子追加 `TipGuidanceDelivered { Full }` 事实以推进 Frontier。

## GD-003: 覆盖内重复交付仅呈现 IdentityOnly 稳定身份

当目标 `TipOccurrence` 已包含在 `TipDeliveryFrontier` 中，且 `TipSemanticCoverage` 表明该规则全文仍可在当前 horizon 恢复时，系统生成 `TipPresentation.IdentityOnly` 形式：仅输出紧凑的 `tip: <name>`。此时不重复输出 `main.md` 全文，不推进 Frontier，严禁将 Identity 呈现误记为全文永久可恢复的持久事实。

## GD-004: 交付决策纯粹基于 Durable Facts 的投影判定

Full 与 IdentityOnly 的呈现判定唯一依赖于 `TipDeliveryProjection`（通过 fold `TipGuidanceDelivered` 等持久化事件派生），按 Main session 严格隔离。严禁使用进程内存集合、临时 JSON 文件或未持久化的提示账本进行判定。系统重启、崩溃恢复与重试后，交付判定结果保持确定性与无漂移。

## GD-005: 上下文重锚触发语义恢复而不伪造新 Occurrence

当 `ContextReanchored` 事件发生时，投影清空当前的 `TipSemanticCoverage`，但保持 `TipDeliveryFrontier` 不变。重锚后首次遇到该规则时再次呈现 Full 全文属于语义恢复（Semantic Restoration），严禁推进 `TipDeliveryFrontier`，严禁将其记录为新的 `TipOccurrence`，严禁因过期的 IdentityOnly 导致重锚后的会话陷入悬空引用。

## GD-006: 目标 Session Owner 确定性解析与空值语义

入参传入 Main 会话 ID 或 Blogger satellite ID 时，系统通过 `SessionAssociation` 确定性解析到对应的 owner Main 会话，并提取最近提交的 RecentTip。若不存在关联的 tip、无会话关联事实或在规则库中未找到对应规则，则返回 None，严禁向模型伪造不存在的 guidance。

## GD-007: `latestTipGuidance` 与 `latestTipNudge` 语义完全等价

`latestTipGuidance` 返回解析后的处置文本，`latestTipNudge` 为其完全等价的历史别名。两者在相同输入下返回完全一致的字符串字节，不引入任何额外的评分或提醒控制流。

## GD-008: 检测语料与补救手册的 Audience 隔离

检测边界文本（`enforcer.md`）仅进入 Blogger 的 effective system prompt；补救处置手册（`main.md`）仅进入 Main 的 Full/Identity guidance 交付。Blogger 的历史 tip 记录（`previous_enforcer_tip`）属于低信任观察数据，严禁进入 Main 的 Authority 面。两端共享 TipName 身份，但渲染器与受众上下文严格隔离，互不混用。

## GD-009: Guidance 交付仅作为提示，不创建新的 Interaction Authority

向 Main 交付 tip guidance 仅通过 `TipGuidanceDelivered` 投影及合成的 `skill({ name: "" })` 工具调用/结果对（形式为 `<skill_content name="">…</skill_content>`）注入 provider horizon。严禁向 Main 注入伪造的用户角色消息（fake-user message），严禁派发新的 Interaction Authority Root，交付过程保持权限与主体中立。

## GD-010: 规则语料可区分性由人类 Review 保证

规则之间的语义正交性、可区分性与无冲突性由规则作者与同行评审在代码审查阶段保障，运行时系统不设立机械词法重叠检测器，不因文本相似度拦截合法的规则交付。

## GD-011: 已投递 Auto-injected 字节按原文冻结并可确定性重放

每个 auto-injected guideline 对以 `PairProgrammingGuideline { Ordinal; CallId; MarkerText; CallGap; ResultGap }` 持久化记录。`MarkerText` 保存当时实际进入 wire 的精确 payload 字节。历史重放时严格还原存储的原文字节，不随规则库版本的演进而被改写。事件投影严格拒绝序号错乱、重复 CallId 以及同一放置点的重复记录。

## GD-012: 新 Occurrence 消费当前 Calibration 投影并原子提交冻结

每个新的 guideline pair occurrence 在物化时组合：latest tip guidance、session elapsed 时间、动态工具调用校准（remaining expected tool calls）、以及 `concern-routing` 待交付的订阅公告或邮箱消息。动态片段仅从各 owner 的 O(1) 投影读取一次；组装完成后的最终 `MarkerText` 立即写入持久化事件进行冻结，后续重放直接读取原文字节，不再重新执行动态渲染与消息抽取。
