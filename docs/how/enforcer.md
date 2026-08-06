# Enforcer — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在 Companion / Blogger 运行过程中，模型必须在其单轮响应中精准调用 `blog` 工具并提交符合规范的 `tip` 标签与工作记录。如果模型忘记调用工具（NoToolCall）、提供了非法 tip（InvalidTip）或输出了空文本（EmptyText），系统必须能够捕获该异常，发起有界的补救提醒（InteractionNudge / InteractionRepair），并在补救失败后干净切入 Fallback 控制器，防止破坏 Blogger 主会话的物理 Transcript。

### 2. 输入输出与规则边界
- **输入**：Provider Run 物理响应、`blog` 工具调用参数、`resources/enforcer/catalog.json` 静态规则表。
- **输出**：`BlogEntryCommitted` 事实、`InteractionNudge` Continuation Prompt、基于物理 attempt 证据的恢复决策。
- **核心边界与不变量**：
  1. 规则载体（ENFORCER-001/030）：规则是静态数据（`catalog.json`），禁止代码内定义 Fallback Catalog 或硬编码 F# 评分算法。
  2. 多调用防御性归并（ENFORCER-025）：同 Run 多个 `blog` 调用按 Rule 优先级与 PartOrdinal 确定性归并为唯一 Canonical Call。
  3. 有界 Nudge（ENFORCER-067）：每个逻辑请求最多产生 1 次有身份的 Nudge；相同 terminal run 重入幂等等待，新的 terminal run 仍无效才切入 FallbackController。不保存 Nudge 计数器。
  4. 规则表决策（ENFORCER-068）：`Evidence → Decision` 完全由有限表决定，绝对禁止根据 Provider 错误散文文字分叉或保存程序阶段。

---

## 目标与 System Prompt 契约（ENFORCER-001 / ENFORCER-030）

Blogger 以 `blog` 工具提交稠密工作日志。fast/deep blogger 统一加载静态配置资源 `resources/prompts/blogger-system.md` 与 `resources/enforcer/catalog.json` 构建 System Prompt。系统契约强制约定：**模型在每轮响应中必须且仅能调用一次 `blog` 工具**，携带有效非空的 `text` 与符合 catalog 的 `tip` 枚举。

### 多调用 tip 选择算法（ENFORCER-025）

当一次 provider run 中出现多个 `blog` 工具调用时，按以下确定性算法筛选派生 canonical call：

```text
1. 将所有 blog 工具调用按 PartOrdinal 升序排列。
2. 过滤出包含有效 tip（tip 存在于 catalog.json 且 text 非空）的调用集合 S。
3. 若 S 为空：归并失败，Cycle 视为无有效调用。
4. 若 S 非空：
   a. 按 catalog.json 中 RuleId 优先级（rule ordinal 越小优先级越高）对 S 排序。
   b. 若 Rule 优先级最高者唯一，选中该调用派生 tip；
   c. 若存在相同最高优先级的多个调用，取 PartOrdinal 最小（最早）的调用派生 tip。
```

---

## ENFORCER-042：多调用防御性归并

同 run 多个 `blog` 按 PartOrdinal 防御性归并；正常协议仍是恰好一次。  
tip 选择规则见上述 ENFORCER-025 算法。

---

## ENFORCER-046：Blogger Cycle 结果派生

Cycle 结果从归并后的 canonical call 派生。

---

## ENFORCER-047：Cycle 后 continuation 与单一恢复流程

成功提交后返回普通材料流程；失败直接选择 nudge/Fallback，不构造业务状态机或第二解释器。

---

## ENFORCER-051：物理 Prompt 与 provider view 重建

物理 prompt 与 provider-visible 历史重建分离：重建只经 durable frames + typed context（COMPANION-005）。

---

## ENFORCER-065：进入 InteractionNudge 的条件

进入 InteractionNudge 的条件固定表驱动。对于一次 Provider Run 产出的响应，按以下固定表驱动判定：

| 响应结局 | 描述 | 是否进入 InteractionNudge | 后续动作 |
|---------|------|--------------------------|---------|
| `ValidCycle` | 成功产生 1 个有效 `blog` 调用 (含归并后) | 否 | 提交 `BlogEntryCommitted` 事实 |
| `NoToolCall` | 模型仅输出普通文本/代码，未调用 `blog` | **是** | `NoRecovery` 时发送 Nudge Continuation |
| `InvalidTip` | 提供了 `blog` 调用但 `tip` 缺失或不在 catalog | **是** | `NoRecovery` 时发送 Nudge Continuation |
| `EmptyText` | 提供了 `blog` 调用但 `text` 规范化后为空 | **是** | `NoRecovery` 时发送 Nudge Continuation |
| `ToolExecutionError` | 工具解析崩溃或语法严重错乱 | **否** | 跳过 Nudge，直接进入 Fallback 流程 |

---

## ENFORCER-066：InteractionNudge 是真正的 InteractionRepair

nudge 即真正 InteractionRepair（Continuation），不新建 Authority。

---

## ENFORCER-067：何时算 nudge 彻底失败

彻底失败判据固定；失败后接 Fallback 或终局，禁止无限 nudge。每个逻辑请求最多一次 Nudge，其界由携带 terminal `ProviderRunIdentity` 的证据表达：

```text
canonical cycle 有效
→ 提交 BlogEntryCommitted

canonical cycle 无效 ∧ Recovery = NoRecovery
→ 写/恢复带 terminal run 身份的 Nudge claim → 发送一次 InteractionNudge

canonical cycle 无效 ∧ InteractionNudgeIssued(run) ∧ 当前 terminal = run
→ 同一观察重放；不发送、不推进，等待 Nudge 的新 terminal

canonical cycle 无效 ∧ InteractionNudgeIssued(run) ∧ 当前 terminal ≠ run
→ FallbackController.recordConfirmedFailure(当前 terminal run)
```

彻底失败后立即切入 FallbackController 推进 cursor 或终止于 FallbackExhausted，禁止发起第二次 Nudge。

---

## ENFORCER-068：恢复决策表

规则表固定，禁止按错误散文分叉。表的左侧全是已发生事实/证据，右侧是本次直接执行的决定；不得把任一行保存成 `Idle/Awaiting/Nudging/FallbackArmed` 程序阶段。

| 已发生证据 | 决定 |
|-----------|------|
| `ValidBlogCycle` | 提交 `BlogEntryCommitted` |
| `InvalidBlogCycle(NoToolCall/InvalidTip/EmptyText)` 且 `NoRecovery` | 发送一次 `InteractionNudge`，记录触发它的 terminal run |
| 上述无效 Cycle 且 terminal run 等于 `InteractionNudgeIssued` 中的 run | 幂等等待，不重复副作用 |
| 上述无效 Cycle 且 terminal run 不同 | Nudge 已产生新无效响应；调用 `FallbackController.recordConfirmedFailure` |
| `ToolExecutionError` | 不 Nudge，调用 `FallbackController.recordConfirmedFailure` |
| Fallback 返回 `MayContinue` | 按新的 `EffectiveAgent` 发送物理 attempt |
| Fallback 返回 `Exhausted` | 停止自动请求，等待新 Authority Root 或显式恢复 |

---

## ENFORCER-070：RecentTips 投影

RecentTips 投影覆盖 normal / squash / restart / recovery / compaction 后路径。

---

## ENFORCER-071：work record 呈现 previous_enforcer_tip

work record 以低信任 `previous_enforcer_tip` 块呈现；不得伪装 parent instruction。

---

## ENFORCER-140：X 侧 Host Compaction

X 侧重锚与 HOST-006 对齐，不在 Enforcer 另起 epoch 算术。

---

## ENFORCER-141：Prefix Probe Promote

probe promote 与 CTX/HOST 提交语义一致。

---

## ENFORCER-142：Y 侧 Compaction

Y 侧与 squash/coverage 合同一致。

---

## ENFORCER-143：Compaction Transform 白名单

transform 白名单：不得借 compaction 路径注入未授权 synthetic。

---

## ENFORCER-150：新增持久事实

新增事实种类服从 PERSIST fold；不得旁路 Journal。

---

## ENFORCER-152：CommitUnknown

CommitUnknown → fail-closed reconcile（PERSIST-003）。

---

## ENFORCER-153：恢复来源

恢复只从 Journal + Host snapshot，不从物理 Y transcript 猜历史。

---

## ENFORCER-154：Cycle 恢复

Cycle 恢复：能证明 response 属于 request 才提交；否则不提交。

---

## ENFORCER-156：Clean Break

schema clean break：不兼容旧评分模型，但保留 schema version 字段纪律。
