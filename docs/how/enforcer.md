# Enforcer — 目标实现

## Implements

行为合同见 `what/enforcer.md`；本文件只描述规则装载、canonical call 选择、补救算法、Full/Identity 交付与 Observation 配对。

## Ownership

规则、协调器和 host 侧效边界见 `shape/enforcer.md`。

---

## 规则装载与 System Prompt（ENFORCER-001 / ENFORCER-030 / ENFORCER-170）

启动时 Infrastructure 扫描 `resources/enforcer/*/`（每个子目录 basename = TipName），读取：

```text
enforcer.md  → EnforcerText  → Blogger effective system 合成
main.md      → MainText      → Main Full/Identity 交付
```

无 `catalog.json`。扫描结果经 Domain `EnforcerCatalog.validate`；失败 → 进程 fail fast，无代码内 fallback。  
lexical CatalogOrdinal / LexicalOrder 只描述装载与 enum 顺序（`1..N`），不参与多调用 tip 优先级。

Blogger effective system：

```text
resources/prompts/blogger-system.md
  + 全部 tip 的 enforcer.md 全文（按 lexical 顺序拼入）
```

契约固定：**每轮响应必须且仅能调用一次 `blog`**，携带有效非空 `text` 与目录 TipName enum 内的 `tip`。

### 多调用 tip 选择算法（ENFORCER-025）

当一次 provider run 中出现多个 `blog` 工具调用时，按以下确定性算法筛选派生 canonical call：

```text
1. 将所有 blog 工具调用按 PartOrdinal 升序排列。
2. 过滤出包含有效 tip（tip 存在于 directory TipName enum 且 text 非空）的调用集合 S。
3. 若 S 为空：归并失败，Cycle 视为无有效调用。
4. 若 S 非空：取 PartOrdinal 最小（排序后第一个）的调用派生 CanonicalTip。
   不在多调用 tip 选择上按 lexical ordinal / RuleId 做二级排序。
```

多调用 tip 选择只看 `PartOrdinal`；lexical ordinal 只描述装载顺序。

---

## ENFORCER-042：多调用防御性归并

同 run 多个 `blog` 按 PartOrdinal 防御性归并；正常协议仍是恰好一次。  
text 按 PartOrdinal 以 `"\n\n"` 稳定合并；evidence 完全相同去重后 `"; "` 拼接。  
tip 选择规则见上述 ENFORCER-025。  
multi-call 仍提交**单** cycle，并标记 protocol violation（`MultiCall=true`，静默诊断 HOST-007）。  
硬界（fail closed）：合并 tool call 数 >32；合并 text >512 KiB UTF-8；合并 evidence >128 KiB UTF-8。

---

## ENFORCER-046：Blogger Cycle 结果派生

Cycle 结果从归并后的 canonical call 派生（canonical text、TipName→RuleId、可选 evidence、ToolCallIds）。

---

## ENFORCER-047：Cycle 后 continuation 与单一恢复流程

成功提交后返回普通材料流程；失败直接选择 nudge/Fallback，不构造业务状态机或第二解释器。

---

## ENFORCER-051：物理 Prompt 与 provider view 重建

物理 prompt 与 provider-visible 历史重建分离：重建只经 durable frames + typed context（COMPANION-005）+ RecentTips Observation 配对。

---

## ENFORCER-065：进入 InteractionNudge 的条件

进入 InteractionNudge 的条件固定表驱动。对于一次 Provider Run 产出的响应，按以下固定表驱动判定：

| 响应结局 | 描述 | 是否进入 InteractionNudge | 后续动作 |
|---------|------|--------------------------|---------|
| `ValidCycle` | 成功产生 1 个有效 `blog` 调用 (含归并后) | 否 | 提交 `BlogEntryCommitted` 事实 |
| `NoToolCall` | 模型仅输出普通文本/代码，未调用 `blog` | **是** | `NoRecovery` 时发送 Nudge Continuation |
| `InvalidTip` | 提供了 `blog` 调用但 `tip` 缺失或不在目录 enum | **是** | `NoRecovery` 时发送 Nudge Continuation |
| `EmptyText` | 提供了 `blog` 调用但 `text` 规范化后为空 | **是** | `NoRecovery` 时发送 Nudge Continuation |
| `ToolExecutionError` | 工具解析崩溃或语法严重错乱（`status=error` 且无 `interrupted`） | **否** | 跳过 Nudge，直接进入 Fallback 流程 |
| `AbortResidue` | Host abort 清理残留（`status=error` ∧ `metadata.interrupted=true`） | **否** | 注入一次 repair；**不推进 cursor**（FALLBACK-013 / LOOP-006） |

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
| `AbortResidue`（blog 调用被 abort 清理打断） | 注入一次 repair，**不调用 `recordConfirmedFailure`**；repair marker 已注入则终局（FALLBACK-013） |
| Fallback 返回 `MayContinue` | 按新的 `EffectiveAgent` 发送物理 attempt |
| Fallback 返回 `Exhausted` | 停止自动请求，等待新 Authority Root 或显式恢复 |

repair 注入本身就是预算标记（ENFORCER-153 派生），因此 `AbortResidue` 不推进 cursor 也仍然有界：同一 cycle 第二次 abort 残留即终局。

---

## Observation 配对与 RecentTips（ENFORCER-070）

### 提交路径

`BlogEntryCommitted` 原子推进 Blog frame + RecordCoverage，并 fold Enforcement 半边：

```text
append RecentTip { RuleId; FieldName; CycleId = ProviderRunIdentity }
keep last RecentTipLimit = 8
```

无独立 `EnforcementCycleCommitted` 事实——Enforcement 由 BlogEntry 派生。

### 配对算法（domain `RulebookObservation.pairTipsAndFrames` / Companion 等价 zip）

```text
tips   = RecentTips 投影（oldest → newest；FieldName + CycleId）
frames = 有效 Blog frame 体（digest × body）

while tips 与 frames 皆非空:
  emit ObservationUnit { TipName = tipᵢ; FrameDigest; FrameBody }  // 消息层：tip msg 后接 frame msg
剩余 tips 或 frames → unpaired 追加
禁止 tips∥frames 两路平行流作为权威 history
```

Companion `build`：

- **Normal**：paired tip+frame units + 物理 delta last（HOST-010）
- **Squash**：对最老 k frames 做同样配对 + squash instruction LAST

### Squash tip co-move

`BlogSquashCommitted` 在同一 owner session 上：

```text
1. Blog frames：最老 count 个 Entry 折叠为一 Squash frame；FrameEpoch+1；coverage 不动
2. Enforcement：applySquash(count) → drop 最老 min(count, tips.Length) 条 RecentTips
```

1:1 假设：每次 BlogEntryCommitted 追加一个 Entry frame 与一个 tip；Squash frame 不增 tip。  
若历史中已含 squash frame，co-truncate 是有界近似，仍优于 tip 与 frame 独立寿命。

### previous_enforcer_tip 呈现

低信任、`[[do_not_exec]]`、role=assistant；行为权威见 `what/enforcer.md` ENFORCER-071。

---

## Main Full / Identity 交付（ENFORCER-071）

`EnforcerHost.resolveTipGuidance`：

```text
1. 经 SessionAssociation 解析 owner Main session（入参可以是 Main 或 Blogger satellite id）
2. 取该 Main 最近已提交 tip FieldName；目录查找 EnforcerRule
3. 读 TipDeliveryProjection.hasFullDelivered(TipName)
   - false → Presentation=Full；Text = name header + rule.MainText
            append HostFact.TipGuidanceDelivered { Full }（restart-safe）
   - true  → Presentation=IdentityOnly；Text = "tip: <name>"
            不改 FullDeliveredTips 集合
4. ContextReanchored → TipDeliveryProjection.applyReanchor → FullDeliveredTips = ∅
   下一 resolve 必须再发 Full main.md
```

`latestTipGuidance` = resolve 的 Text；`latestTipNudge` 为同义别名（Full/Identity，不是旧 Nudge 字段）。  
交付决策**只** fold `TipGuidanceDelivered`，禁止进程本地「已发送」集合。

---

## ENFORCER-140：X 侧 Host Compaction

X 侧重锚与 HOST-006 对齐，不在 Enforcer 另起 epoch 算术。  
重锚同时清空 TipDelivery Full 集合（与 Blog/Prefix 同原子 session 更新）。

## ENFORCER-141：Prefix Probe Promote

probe promote 与 CTX/HOST 提交语义一致。

## ENFORCER-142：Y 侧 Compaction

Y 侧与 squash/coverage 合同一致；Observation tip co-move 见 ENFORCER-070。

## ENFORCER-143：Compaction Transform 白名单

transform 白名单：不得借 compaction 路径注入未授权 synthetic。

## ENFORCER-150：新增持久事实

新增事实种类服从 PERSIST fold；不得旁路 Journal。  
Enforcer 相关 durable 事实族：`BlogEntryCommitted`、`BlogSquashCommitted`、`TipGuidanceDelivered`（Host）；Enforcement/TipDelivery/Observation 均为 fold 派生，非平行文件账本。

## ENFORCER-152：CommitUnknown

CommitUnknown → fail-closed reconcile（PERSIST-003）。

## ENFORCER-153：恢复来源

恢复只从 Journal + Host snapshot，不从物理 Y transcript 猜历史。  
`BloggerToolRecovery` 从 durable claim + provider-visible evidence 派生，不退化成整数 counter。

## ENFORCER-154：Cycle 恢复

Cycle 恢复：能证明 response 属于 request 才提交；否则不提交。  
同一 ProviderRun 重复 BlogEntry → Enforcement 半边 Error（调用方可吸收为幂等）。

## ENFORCER-156：Clean Break

schema clean break：不兼容旧评分模型，但保留 schema version 字段纪律（folder loader 固定 schemaVersion=1）。
