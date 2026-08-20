# guidance-delivery — HOW

## 架构机制与核心模型

### 1. 交付决策与投影状态机

1. **Owner 解析与 Guidance 决策**：
   - `resolveTipGuidance` 接收 session ID，经 `SessionAssociationProjection` 映射到 owner Main 会话，并提取最新提交的 RecentTip。
   - 依据 `TipDeliveryProjection` 判定：
     - 若未曾以 Full 形式交付过，返回 `TipPresentation.Full`（包含语言化头部与 `rule.MainText` 全文），并异步持久化 `TipGuidanceDelivered { Full }` 事实推进 Frontier；
     - 若已存在 Full 交付记录，返回 `TipPresentation.IdentityOnly`（形式为 `tip: <name>`），不追加持久化事实。
   - `latestTipGuidance` 与 `latestTipNudge` 均作为上述决策结果的纯文本读取接口。

2. **双轴状态机与重锚投影**：
   - `TipDeliveryProjectionState` 维护 `FullDeliveredTips` 集合；
   - `apply` 操作：遇到 Full 交付时将 tip 记录加入集合；遇到 IdentityOnly 时仅作为审计，不推进集合；
   - `applyReanchor` 操作：响应 `ContextReanchored` 事件，重置清空集合（代表 Coverage 丢失），使后续再次遇到该规则时重新生成 Full 文本，同时不推进 Frontier。

### 2. Guideline 注入与历史字节冻结

1. **Auto-injected Guideline 投影**：
   - `GuidelineProjection` 管理 `PairProgrammingGuideline` 序列，按序校验 `Ordinal`、`CallId` 与 `TranscriptGap` 放置点唯一性；
   - `applyReanchor` 推进可见下界，保留持久化事实供历史审查，新 horizon 下的注入使用递增的 Ordinal 与全新 CallId；
   - `MarkerText` 字段按当时实际写入的 payload 原样存储，保证跨生命周期重放的字节级一致性。

2. **Horizon 组装与 Transform**：
   - 动态装配器组合最新 tip guidance、耗时统计、动态工具调用期望与 `concern-routing` 待交付消息；
   - 最终组装的 pair body 封装为 `<skill_content name="">…</skill_content>`，通过合成的 `skill` 工具调用注入 transcript；
   - 注入成功后 MarkerText 立即持久化冻结，重放路径直接返回已存文本，不再重复触发动态渲染。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| GD-001 | `requirements/guidance-delivery/tests/tip-delivery-projection.test.mjs` |
| GD-002 | `requirements/guidance-delivery/tests/tip-guidance-delivery.test.mjs` |
| GD-003 | `requirements/guidance-delivery/tests/tip-guidance-delivery.test.mjs` |
| GD-004 | `requirements/guidance-delivery/tests/latest-tip-nudge.test.mjs` |
| GD-005 | `requirements/guidance-delivery/tests/tip-delivery-projection.test.mjs` |
| GD-006 | `requirements/guidance-delivery/tests/tip-guidance-delivery.test.mjs` |
| GD-007 | `requirements/guidance-delivery/tests/latest-tip-nudge.test.mjs` |
| GD-008 | `requirements/guidance-delivery/tests/audience-separation.test.mjs` |
| GD-009 | `requirements/guidance-delivery/tests/latest-tip-nudge.test.mjs` |
| GD-010 | `requirements/guidance-delivery/tests/audience-separation.test.mjs` |
| GD-011 | `requirements/guidance-delivery/tests/guideline-projection.test.mjs` |
| GD-012 | `requirements/guidance-delivery/tests/pair-calibration.test.mjs` |
