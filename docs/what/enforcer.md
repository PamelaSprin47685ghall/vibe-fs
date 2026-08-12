# Blogger / Enforcer — 可观察行为

条款前缀：`ENFORCER-`。  
Cycle 写入口与恢复证据边界见 `shape/enforcer.md`。  
归并、nudge、continuation、compaction、Full/Identity 与 Observation 接线见 `how/enforcer.md`。  
规则实例 SSOT：`resources/enforcer/<TipName>/{enforcer.md,main.md}`（目录名 = TipName = provider tip enum = durable RuleId；**无** `catalog.json`，也无并行元数据清单）。

## ENFORCER-001：目标

Blogger 以 `chronicle` 工具提交稠密工作日志；`tip` 绑定目录 TipName；一次有效 cycle 原子提交 Blog frame 与 RecordCoverage。  
同一 tip 目录对双消费者各交付一份正文：

- **Blogger（Y）**：装载 `enforcer.md` 全文进 effective system（与 `blogger-system.md` 合成），约束 tip 选择与检测边界。
- **Main（X）**：经 `TipGuidanceDelivered` 投影，按 Full / IdentityOnly 交付 `main.md`（见 ENFORCER-071 与 how 交付算法），不改写 Main Authority。

## ENFORCER-002：非目标

不把 Blogger 做成通用评分引擎；不恢复 score-vector 控制流；不在 transform 里预测压缩。  
不把 `catalog.json`、代码内 fallback catalog 或 dist 双副本当作规则 SSOT。  
不向 Main 注入工程 fake-user message 作为 tip 第二 Authority。

## ENFORCER-003：Blogger Cycle

一个 Blogger Cycle = 一次 provider run 上对 `chronicle` 的有效归并提交。

## ENFORCER-004：Blogger Cycle 结果

结果携带 canonical text（来自 `entry`）、tip→RuleId（= TipName）；无效 cycle 不进 frames。  
**无**独立 `evidence` 字段——若证据改变 occurrence，它进入 `entry`。  
提交成功时同时派生 Enforcement 半边：该 cycle 的 tip 进入有界 RecentTips（Observation 历史的 tip 侧）。

## ENFORCER-010：Blogger 工具权限

Blogger 工具权限仅 `chronicle`。

## ENFORCER-011：工具名称

工具名稳定为 `chronicle`。旧名 `blog` 非法，无 alias。

## ENFORCER-020：逻辑 schema

必填：`entry`、`tip`（目录 TipName 枚举）。无 `evidence`。

## ENFORCER-021：tip 枚举身份

`tip` 的合法值 = 已加载 rulebook 的目录 TipName 枚举；映射到 RuleId，且 RuleId = FieldName = TipName（folder cutover 后无第二身份前缀）。

## ENFORCER-022：Required / Optional 语义

必填缺失失败；可选缺省不发明值。

## ENFORCER-023：缺 tip / 未知 tip 失败

缺 tip 或 tip 不在目录 enum → 该调用失败，不得默认 tip，不得 fuzzy / 拼写修复。

## ENFORCER-024：字段识别

只认合同字段名；未知字段不得静默充当 tip/entry。  
额外 numeric property 不得复活 score path。

## ENFORCER-025：多调用时 tip 选择

多 `chronicle` 归并时 tip 选择规则确定（实现见 how 归并）；不得随机取，不得按 lexical ordinal 二级排序。

## ENFORCER-026：Transport 与 Semantic Schema 分离

不得用 wire 形态当领域身份；Semantic schema 才进 cycle。

## ENFORCER-030：统一 System Prompt

fast/deep blogger 共用 authoritative system（`resources/prompts/blogger-system.md`），并与 folder SSOT 各 tip 的 **enforcer.md** 全文合成 effective system；工具合同在 system 中固定「恰好调用一次 chronicle」。  
`main.md` **不**进入 Blogger system——它只服务 Main Full/Identity 交付。

## ENFORCER-040：工具立即返回

`chronicle.execute` 立即返回，不在工具内等待后续模型轮次。

## ENFORCER-060：缺少工具调用 — 总则

无有效 chronicle → 进入 InteractionRepair / nudge 路径或 Fallback（见 how）。

## ENFORCER-061：无有效 entry

无有效 entry → 不提交。

## ENFORCER-062：Fallback 切换

失败与 Fallback 切换规则不另造预算；走统一 FallbackController。abort 清理残留不算失败（FALLBACK-013）。

## ENFORCER-063：成功关闭恢复窗口

成功提交把当前逻辑请求的 `BloggerToolRecovery` 恢复为 `NoRecovery`；不得保留会污染下一请求的 Nudge/AABB 证据，也不得另造 repair 计数器。

## ENFORCER-070：Observation 历史（RecentTips + frames）

每次已提交 cycle 恰好记录一个 tip 到有界 RecentTips（上限 8，oldest → newest）。  
Observation 是 tip 与 Blog frame 的**配对**视图（domain `ObservationUnit` / `pairTipsAndFrames`）：优先 tipᵢ↔frameᵢ 前向 zip，剩余侧 unpaired 追加——禁止 tip∥frame 两路平行流当权威历史。  
`BlogSquashCommitted` 在折叠最老 `count` 个 frame 的同时 **co-truncate** 最老 tip（Observation squash / tip co-move）；squash frame 本身不新增 tip。  
RecentTips 覆盖 normal / squash / restart / recovery / compaction 后重建路径（同一 projection 输入 Companion 重建）。

## ENFORCER-071：双消费者 tip 呈现；交付前沿 ≠ 语义覆盖

### 两轴分离（不得压成单一 durable bool）

```text
TipDeliveryFrontier
    哪些 TipOccurrence 已交付给该 Main
    durable、monotonic、occurrence-based
    ContextReanchored **不**重置

TipSemanticCoverage
    哪些 TipName 的 full main.md 语义此刻仍可从当前 provider horizon 恢复
    TipName-based、horizon-relative
    ContextReanchored 可重置 / 重导
```

IdentityOnly **仅当**当前 TipSemanticCoverage 表明该 TipName 全文仍可恢复时合法。  
覆盖丢失后再次给出 full main.md = semantic restoration，**不是**新 TipOccurrence，也不推进 TipDeliveryFrontier。

### Blogger 侧（低信任 previous tip）

work record 以低信任 `previous_enforcer_tip` 块呈现（role=assistant、`[[do_not_exec]]`）；不得伪装 parent instruction。  
与 historic_frame 组成配对 Observation unit 后，再接物理 delta（normal）或 squash instruction（squash）。

### Main 侧（Full / Identity main.md）

Main 自动 tip 半边经 `resolveTipGuidance`：

| 条件 | Presentation | 正文 |
|------|--------------|------|
| 该 TipOccurrence 尚未计入 TipDeliveryFrontier，或 TipSemanticCoverage 表明全文不可恢复 | `TipPresentation.Full` | name header + **main.md** 全文 |
| TipDeliveryFrontier 已含该 occurrence **且** TipSemanticCoverage 仍可恢复全文 | `TipPresentation.IdentityOnly` | 紧凑 `tip: <name>` 身份 |

决策只读 TipDeliveryFrontier + TipSemanticCoverage（fold `HostFact.TipGuidanceDelivered` 等 durable 事实），不读进程内存私账。  
`ContextReanchored`（HOST-006）清空 TipSemanticCoverage；重锚后若全文不可恢复必须再发 Full main.md，禁止用过期 IdentityOnly 搁浅。  
IdentityOnly 不把「全文永久可恢复」写成 durable bool；交付 marker 只记录 occurrence 与 presentation，不得冒充 TipSemanticCoverage。  
不得向 Main 注入工程 fake-user 作为 tip Authority；Main 语义仍由正式域独有。

## ENFORCER-072：ScoreVector 删除与版本化 clean break

ScoreVector 删除与版本化 clean break。

## ENFORCER-073：旧评分条款废止

旧评分条款废止，不得作为运行时分支。

## ENFORCER-170：规则 folder SSOT

规则实例唯一真相：`resources/enforcer/<TipName>/` 下恰好 `enforcer.md` + `main.md`（UTF-8；无第三文件作 SSOT）。  
装载后 Domain 校验：schemaVersion=1；至少一条；Name/RuleId/FieldName 唯一且三者相等；lexical ordinal 连续 `1..N`；EnforcerText/MainText（及装载派生字段）非空。  
启动 fail fast；**禁止**代码内 fallback catalog、dist 双副本、并行 `catalog.json` 元数据第二真相。  
发布后 TipName（= field = id）稳定；改名是显式迁移，不是静默别名。
