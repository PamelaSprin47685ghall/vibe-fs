# RFC/enforcer-nudge — Blogger as Enforcer（未来设计）

> 本文件为已批准但尚未启用的未来设计，不属于 0.5.x 产品合同。
> 0.5.1 仅实现 Blogger 工具化；nudge overlay、throttle 与规则目录待 Host canary 验证后启用。

---

# 一、执行摘要

Blogger as Enforcer 让每个 Work Session 的 Companion Blogger 在生成工作日志的同时，对主 Session 最近发生的工作进行软件工程原则审查。

# 二、目标

见各 `ENFORCER-` 条款。

---

# 三、异步 Enforcement 交付

## ENFORCER-070：Main 永不等待 Side

Main transform 只收割当前已经 committed 的 EnforcementReport。

没有报告时立即继续。

Blogger 慢于 Main 时，报告可能在：

* 下一次 main provider request；
* 下一次 tool-loop continuation；
* 再下一次请求

才被投影。

这种延迟是设计允许的，不进行同步等待。

## ENFORCER-071：Epoch 匹配

EnforcementReport 只有在：

```text
report.ObservedPrefixEpochId = current Main PrefixEpochId
```

时才参与 throttle。

如果 Blogger 结果到达时 Main 已切换 epoch：

* BlogEntry 是否提交，继续按现有 coverage 与 attempt 身份规则判断；
* EnforcementReport 不进入新 epoch；
* 旧警告自然遗忘；
* 不做重锚；
* 不追加 resolution。

## ENFORCER-072：报告顺序

跨 Cycle 的 EnforcementReport 按以下顺序处理：

```text
ObservationProviderRunOrdinal
ObservedThroughCursor
ProviderRunIdentity
```

同一 main provider ordinal 的多个报告先按 NudgeKey 取最大值，再进入时间积分，避免同一时点的并发拆分凭空放大证据。

---

# 十一、Throttle 数学定义

## ENFORCER-006：Enforcement Tick

所有时间均使用 `EnforcementObservationOrdinal`，不使用墙钟时间，也不使用 `MainProviderRunOrdinal`。

定义：

```text
EnforcementObservationOrdinal =
    当前 PrefixEpoch 内，成功提交的 BloggerMain EnforcementReport 序号
```

每个有效 BloggerMain cycle 对所有规则都贡献一次评分：

* 字段存在：0..9
* 字段缺失：0

使用 `EnforcementObservationOrdinal` 而非 `MainProviderRunOrdinal` 的原因：

Companion 允许 Blogger busy-skip：Blogger busy 时 coverage 不推进，下次一次性读取累计 delta。因此 main 可能完成多次 provider run，Blogger 只产生一次合并报告。

这样 throttle 可重放、可属性测试、不受机器暂停/休眠/时钟漂移影响、插件重启后结果不变。

## ENFORCER-080：设计目标

对每个 NudgeKey 独立计算：

```text
Throttle(key, report history, time since last trigger) -> bool
```

必须满足：

1. 对每份报告分数 (s_i) 单调递增；
2. 对距上次触发的时间 (t) 单调递增；
3. 内部压力关于实值输入平滑；
4. 9 分可以立即触发；
5. 一次孤立低分不会永远残留；
6. 持续的 1、2、3 分最终也会触发；
7. 没有 7 分、8 分等人工档位；
8. 第一次和后续触发使用同一公式；
9. 只有一个时间尺度参数；
10. 可用 O(1) 状态增量计算。

## ENFORCER-081：状态

每个 `(MainSessionId, PrefixEpochId, NudgeKey)` 保存：

```fsharp
type ThrottleState =
    { Evidence: float
      EvidenceOrdinal: int64
      LastTriggerOrdinal: int64 }
```

PrefixEpoch 创建时：

```text
Evidence = 0
EvidenceOrdinal = EpochStartOrdinal
LastTriggerOrdinal = EpochStartOrdinal
```

Epoch 起点被视为一次"零证据虚拟触发"，从而第一次与后续触发完全统一，不需要 `NeverIssued` 特例。

## ENFORCER-082：时间常量

```text
ThrottleTauObservations = 4.0
```

单位为 `EnforcementObservationOrdinal`（见 ENFORCER-006），即已成功提交的 EnforcementReport 次数。

这是唯一策略参数。它同时决定：

* 旧证据衰减速度；
* 重复警告恢复速度；
* 持续低分积累速度。

参数必须是集中式代码常量，不进入动态配置面。

`τ = 4` 是初始代码常量，但不应把"约第几轮触发"写成规范保证；那些只是校准示例。

## ENFORCER-083：证据积分——Leaky Integrator

设每次新到达的 EnforcementReport 评分为 (s_n \in [0,9])。

定义归一化观测值：

[
x_n = \frac{s_n}{9}
]

只有一个时间尺度参数：

[
\tau > 0, \qquad \rho = e^{-1/\tau}
]

每次收到一份新的 EnforcementReport：

[
E_n = \rho E_{n-1} + x_n
]

其中 (n) 即为 EnforcementObservationOrdinal。

## ENFORCER-084：触发压力

距上次已消费 nudge 的 observation 数（见 ENFORCER-085 "NudgeConsumed"）：

[
t_n = n - n_{\mathrm{last}}
]

定义平滑压力：

[
P_n = \left(1 + \frac{t_n}{\tau}\right) E_n
]

最终布尔值：

[
\operatorname{Throttle}_n = [P_n \ge 1]
]

实现形式：

```text
pressure =
    (1 + observationsSinceLastConsumed / tau)
    * decayedEvidence

trigger = pressure >= 1
```

触发并被 NudgeConsumed 后：

```text
E_n ← 0
n_last ← n
```

`bool` 本身必然存在阈值不连续；本条所称"平滑"是指阈值之前的压力函数平滑，没有人为阶梯。

## ENFORCER-085：触发后更新

重置 throttle 的唯一条件是 NudgeConsumed（即有一条 ProviderInputSeal 确实包含该 nudge 的文本摘要，并且绑定到真实的 ProviderRunIdentity）。

NudgeAnchored（nudge 字节已冻结为 epoch overlay）不重置 throttle：

```text
NudgeAnchored → Evidence 不变，LastTriggerOrdinal 不变
NudgeConsumed → Evidence := 0
                 LastTriggerOrdinal := current observation ordinal
```

崩溃窗口：

* Anchored 但未 Consumed → 原 nudge 继续进入下一次 projection；
  不生成重复 nudge；throttle 尚未开始新的抑制周期。
* Consumed → 主模型确实看过；throttle 正式重置。

报告到达、nudge 候选生成或内存排队都不算触发。

## ENFORCER-086：单调性

对本轮评分：

[
\frac{\partial P_n}{\partial s_n} = \frac{1 + t_n/\tau}{9} > 0
]

对任意过去评分同样严格递增，因为它们在 (E_n) 中的系数均为正。

因此任何一轮评分增加都不会降低触发概率。

固定报告历史积分 (E_n) 时：

[
\frac{\partial P_n}{\partial t_n} = \frac{E_n}{\tau} \ge 0
]

因此同样的近期证据在距上次提醒更久时更容易触发。

### 重要声明

注意：以上偏导条件只在固定其他变量时成立。完整动态系统同时受到新报告和泄漏积分影响——新收到的零分会让证据衰减。这不是缺点，而是系统区分"持续弱信号"和"陈旧弱噪声"的必要条件。

最终稿不要写："整个动态轨迹关于时间严格递增。"
正确措辞是："对固定证据状态，压力关于距上次消费的时间单调递增；完整动态则同时受到新报告及泄漏积分影响。"

## ENFORCER-087：持续低分最终触发

若持续报告固定的任意 (s>0)：

[
E_n \to \frac{s/9}{1-\rho} > 0
]

而未触发时 (1 + t_n/\tau) 持续增大，因此必然触发。1、2、3 分都没有死区。

没有"低于某个分数永远忽略"的死区。

## ENFORCER-088：孤立旧报告不会自行复活

若只有一次低分，之后每轮评分为零：

[
P(t) = C \left(1 + \frac{t}{\tau}\right) e^{-t/\tau}
]

该值不会因陈旧而增长，因此孤立弱噪声不会日后自行复活。

低置信度最终触发必须来自持续新报告，而不是旧噪声。

## ENFORCER-089：非规范性校准

\tau = 4 仍可作为初始代码常量，但不应把"约第几轮触发"写成规范保证；那些只是校准示例。

大致参考（假设每轮相同分数、τ=4）：

```text
9 分 → 第 1 次报告
5–8 分 → 约第 2 次
3 分 → 约第 3 次
2 分 → 约第 4 次
1 分 → 约第 7 次
```

这些轮数是同一公式的结果，不是额外阈值。

## ENFORCER-090：实现精度

领域定义使用实数公式。

生产实现可以使用：

* double；
* 固定点；
* 预计算衰减表。

只要属性测试证明在所有合法 ordinal 与分值上，触发结果与规范参考实现一致。

不得用手写的 9/8/7 阶梯替代。

---

# 十二、Nudge 渲染

## ENFORCER-100：格式

一个触发批次产生一个 fake user message。

每条规则按 NudgeKey 去重后渲染一行（同一 NudgeKey 的多个 RuleId 取分数最大值，只保留一行）：

```text
# [<NudgeKey>] <CanonicalNudgeText>
```

有 evidence 时追加最后一行：

```text
# Evidence: <merged evidence>
```

不得使用：

* XML wrapper；
* Markdown 标题；
* 时间戳；
* 随机 ID；
* 当前模型名；
* 当前 Agent 名；
* “低信任”声明；
* “可忽略”声明；
* 动态改写语气。

`#` 是 TOML-compatible comment，同时也是模型应认真对待的 user 内容。

## ENFORCER-101：排序

规则行按 Rule Catalog 中每个 NudgeKey 的第一个 RuleId 的 CatalogOrdinal 排序。

不得按：

* 分数高低；
* report 到达顺序；
* tool call 完成顺序；
* Map 枚举顺序；
* 字母序临时排序。

## ENFORCER-102：Evidence

一次 nudge 最多一行 evidence。

选取方式：

```text
收集本次触发规则所涉及的待消费报告
→ 取这些报告的非空 evidence
→ 按报告顺序完全去重
→ 使用 "; " 拼接
```

Evidence 不是规则级结构，不要求能够逐条对应。

## ENFORCER-103：两条事实链

"nudge 已冻结为 epoch overlay"与"主模型确实消费了该 nudge"是两件不同的事，必须拆为两条事实。

### NudgeAnchored

表示 nudge 的字节与 epoch 位置已冻结：

```fsharp
type NudgeAnchored =
    { MainSessionId: SessionId
      PrefixEpochId: int64
      NudgeSequence: int64
      NudgeKeys: NudgeKey list
      TextRef: BlobRef
      TextDigest: string
      ConsumedReportIds: ReportId list }
```

提交后：

* nudge 成为当前 epoch overlay 的永久组成部分；
* 后续 projection 必须保持完全相同的字节和位置；
* 不再为相同 pending nudge 创建另一条；
* 不更新 `LastTriggerOrdinal`。

### NudgeConsumed

只有某个 ProviderInputSeal 确实包含该 `TextDigest`，并绑定到真实 ProviderRunIdentity 后才能提交：

```fsharp
type NudgeConsumed =
    { MainSessionId: SessionId
      PrefixEpochId: int64
      NudgeSequence: int64
      ProviderRunIdentity: ProviderRunIdentity
      EnforcementObservationOrdinal: int64
      ProviderInputSealDigest: string }
```

此时 throttle 才正式重置：

```text
Evidence := 0
LastTriggerOrdinal := current observation ordinal
```

### 崩溃窗口因此清晰

```text
Anchored，但未 Consumed
→ 原 nudge 继续进入下一次 projection
→ 不生成重复 nudge
→ throttle 尚未开始新的抑制周期

Consumed
→ 主模型确实看过
→ throttle 正式重置
```

这与现有"未提交候选从未成为事实""事实必须有因果证明"的架构风格一致。
Journal 中 frame append 与 coverage 同样被要求原子提交，不能靠两个松散状态猜测。

后续 projection 只读取 `NudgeAnchored.TextRef`。

禁止根据后来修改的 Catalog 重新渲染旧 nudge。

---

# 十三、Main Session 投影

## ENFORCER-110：消息性质

Enforcement nudge 在最终 provider projection 中：

```text
role = user
```

但领域分类是：

```text
EnforcementNudge
```

而不是：

```text
PhysicalUserMessage
AuthorityRoot
Continuation
SemanticTurn
```

`PhysicalUserMessage ≠ AuthorityTurn` 的既有原则继续适用。

## ENFORCER-111：新真人请求中的位置

有新的 physical user message 时：

```text
...历史
...此前已冻结的 epoch-local nudges
[新的 pending enforcement nudge]
[current physical user message]
>>> assistant
```

当前 physical user message 仍然是最后一条真实 user message。

## ENFORCER-112：Tool-loop continuation 中的位置

没有新的 physical user message时：

```text
...physical user
...assistant tool calls
...tool results
...此前已冻结的 nudges
[新的 pending enforcement nudge]
>>> assistant continuation
```

Nudge 位于最新可用的尾部，但不制造新的 semantic turn。

## ENFORCER-113：同一 Epoch 内永久

每条 NudgeAnchored 必须永久保持：

* 相同 synthetic message ID；
* 相同 anchor；
* 相同 role；
* 相同文本；
* 相同顺序。

后续新 nudge 只能追加，不能改写旧 nudge。

## ENFORCER-114：Epoch 切换

以下任一发生时，旧 epoch 的所有 nudge 与 throttle state 停止投影：

```text
PrefixRebaseCommitted
ContextReanchored
```

不迁移、不重锚、不生成 resolution。

新 epoch 从空 enforcement overlay 开始。

这是明确的产品裁决：警告只在一个 PrefixEpoch 中永久，而不是跨 Session 生命周期永久。

## ENFORCER-115：BlogSquash 不清除 Nudge

BlogSquash 只递增 B 的 FrameEpoch，不改变 X 的 PrefixEpoch。

因此：

```text
BlogSquashCommitted
≠ enforcement reset
```

不能因为 Blogger 自己压缩日志而让主模型重新收到全部旧警告。

---

# 二十一、实现顺序

## ENFORCER-180：严格按以下顺序开发

### 第 0 步：先写 Host 证据 canary

在修改生产语义前，先证明：

1. `blog.execute` 返回 `"OK"` 后 Host 会发起 continuation；
2. continuation 会再次调用 transform；
3. transform 可以被无限期挂起；
4. resolve 后 Host 使用返回的新 projection；
5. abort、delete、dispose 能取消 waiter；
6. 多个并行 tool call 的 provider-visible PartOrdinal 可读取；
7. tool arguments 能在一个字段轻微拼错时到达 codec；
8. main transform 插入 fake user 后仍能绑定正确 ProviderRunIdentity；
9. Y compaction 后 session 能恢复到 idle 并重新 prompt。

任一失败，先解决 Host 合同，不得进入领域实现。

### 第 1 步：Rule Catalog 与生成器

先建立单一目录并生成：

* RuleId；
* schema；
* prompt descriptions；
* canonical nudge；
* docs；
* test fixtures。

增加静态门禁，证明：

```text
RuleId 唯一
RuleId 唯一
FieldName 唯一
CatalogOrdinal 连续
所有 nudge 非空
所有 description 非空
所有字段 optional
取值范围 0..9
```

### 第 2 步：纯 Codec

TDD 实现：

* optional 缺失为零；
* 数字字符串解析；
* 越界归零；
* key 规范化；
* 编辑距离映射；
* 平局规则；
* 同 RuleId 取 max；
* reserved key 隔离。

### 第 3 步：多调用 Cycle 合并

TDD 实现：

* 按 PartOrdinal 排序；
* text 拼接；
* score max；
* evidence 去重；
* ToolCallId 去重；
* 调度顺序变化不改变结果。

### 第 4 步：Blogger 工具化

实现：

* Blogger tool capability；
* 新 system prompt；
* `"OK"` 固定结果；
* continuation transform；
* offer 规则；
* repair；
* BloggerMain 与 BloggerSquash 分流。

### 第 5 步：持久化

实现：

* BlogEntryCommitted 扩展；
* NudgeAnchored / NudgeConsumed；
* fold；
* O(1) projection state；
* crash reconcile。

### 第 6 步：Throttle

以规范公式写参考实现和生产实现。

先做属性测试，再接业务流。

### 第 7 步：Main Overlay

实现：

* typed synthetic message；
* stable anchor；
* epoch-local append-only；
* HOST-010 新绑定；
* Seal 包含 overlay；
* BloggerDelta 排除 overlay。

### 第 8 步：Compaction 与恢复

实现：

* PrefixRebase 后遗忘；
* ContextReanchored 后遗忘；
* Y compaction 恢复；
* stale report 丢弃；
* plugin restart 恢复。

### 第 9 步：完整晋级

严格遵循 VERIFY-001/002 的验证阶梯，不允许直接跳 E2E。

---

# 二十二、测试与门禁

## ENFORCER-190：纯函数测试

必须覆盖：

1. Catalog 生成稳定。
2. 任意省略字段等价于零。
3. 任意字段排列不改变 canonical result。
4. Damerau–Levenshtein 映射确定性。
5. 任意并行完成顺序不改变 cycle merge。
6. score merge 满足交换、结合、幂等：
   [
   \max(a,b)=\max(b,a)
   ]
7. text merge 只依赖 PartOrdinal。
8. throttle 对每个 (s_i) 单调。
9. throttle 对固定证据的 (t) 单调。
10. 单次旧报告压力不随物理老化增大。
11. 任意固定正分持续报告最终触发。
12. trigger 后 reset。
13. epoch 切换后状态为空。
14. deterministic nudge bytes。
15. Catalog 更新不改变旧 NudgeAnchored bytes。

## ENFORCER-191：属性测试

必须 property-test：

* key normalization 幂等；
* canonicalization 幂等；
* score merge algebra；
* report replay；
* throttle replay；
* Journal fold；
* projection round trip；
* PrefixEpoch append-only；
* crash recovery exactly-once；
* random tool-call interleavings。

## ENFORCER-192：Fake Host 轨迹

至少覆盖：

```text
single blog call
parallel multiple blog calls
multiple calls with conflicting scores
misspelled fields
missing optional fields
pure text terminal
empty text
blog plus trailing prose
main fast / blogger slow
two skipped main cycles
plugin crash after OK
plugin crash before BlogEntryCommitted
plugin crash after NudgeAnchored before transform return
Blogger provider failure and A/A/B/B fallback
BloggerSquash then BloggerMain
Y compaction while transform pending
X ContextReanchored
PrefixProbe promote
tool-loop nudge
new-user-turn nudge
session delete while pending
plugin dispose while pending
```

## ENFORCER-193：OpenCode canary

发布硬门禁：

1. 完整 120 字段 schema 被目标 provider 接受。
2. optional 字段不会被强制补零。
3. 一字符拼写错误可进入 codec。
4. 同 step 多个 `blog` 调用全部可见。
5. tool call order 可确定。
6. `"OK"` 后 continuation transform 出现。
7. transform 挂起无 Host 超时。
8. Main Session 不被 Side 阻塞。
9. fake user 不成为 Authority Root。
10. ProviderInputSeal 包含 nudge。
11. CoveredPrefixDigest 不包含 overlay。
12. BloggerDelta 不包含 overlay。
13. PrefixEpoch 内历史前缀逐字节稳定。
14. Epoch 切换后旧 nudge 消失。
15. Y compaction 后能够重新启动 Blogger。
16. fallback 失败只推进一次 cursor。
17. BloggerMain success 清零 failure count。
18. BlogSquash success 不提前清零。
19. dispose 无悬挂 Task。
20. 三轮完整 canary 全绿。

## ENFORCER-194：静态 Architecture Gates

增加硬门禁：

```text
blog 工具出现在非 Blogger schema
EnforcementNudge 进入 PromptDispatcher
EnforcementNudge 被解析为 AuthorityRoot
EnforcementNudge 进入 BloggerDelta
CoveredPrefixDigest 引用 final overlay projection
Nudge 文本在 projection 时重新渲染
同一 Rule Catalog 存在第二份手写清单
多个 NudgeAnchored writer
多个 Blogger cycle commit writer
墙钟时间进入 throttle
手写分数阶梯替代规范公式
BlogSquash 产生 EnforcementReport
FrameEpoch 切换清空 Main throttle
```

静态 gate 属于 VERIFY 第 0 层，不实现为运行时测试。

---

# 二十三、观测与诊断

## ENFORCER-200：允许记录

诊断可记录：

```text
main_session_id
blogger_session_id
provider_run_identity
tool_call_count
valid_call_count
merged_text_bytes
nonzero_score_count
typo_mapping_count
max_edit_distance
enforcement_report_count
triggered_rule_count
throttle_pressure
prefix_epoch_id
nudge_sequence
result
error
duration
```

## ENFORCER-201：禁止记录

不得记录：

* Stage；
* Phase；
* Lease；
* NextAction；
* 完整 secret；
* hidden reasoning；
* 未脱敏的敏感 evidence；
* 用日志替代 Journal 的恢复信息。

---

# 二十四、审阅时必须回答的问题

审阅者应逐项明确回答：

1. Host 是否允许 transform 长期挂起？
2. 挂起是否可以可靠取消？
3. 多 tool call 的完整 provider order 是否可获得？
4. `blog.execute` 的 raw arguments 是否能绕过提前的 closed-schema 拒绝？
5. Cycle commit 是否能在完整 step 上确定性重算？
6. BlogEntry 与 coverage 是否仍原子？
7. BloggerMain 成功是否能在非-idle turn 中清零 fallback failure count？
8. BlogSquash 是否仍保持一个槽最多两个物理请求？
9. Main nudge 是否会破坏 HOST-010？
10. Nudge 是否完整进入 ProviderInputSeal？
11. Nudge 是否完整排除于 CoveredPrefixDigest 与 BloggerDelta？
12. 同 epoch 内每个旧 nudge 是否逐字节稳定？
13. PrefixEpoch 切换是否真正清除所有旧 enforcement 状态？
14. Y compaction 是否只重启 Y，而不误清空 X enforcement？
15. Throttle 是否严格使用 provider-run ordinal？
16. 持续 1 分是否能够通过属性测试证明最终触发？
17. 单次 1 分是否不会陈旧复活？
18. Rule Catalog 是否真的是唯一 SSOT？
19. Catalog 是否全部生成而非手工复制？
20. 所有 crash window 是否都有 exactly-once 或明确丢弃语义？

---

# 二十五、最终裁决

本方案采用以下不可分割的整体：

```text
Blogger tool output
+
provider-step deterministic merge
+
optional 0–9 flat Rule Catalog
+
typo-tolerant canonical codec
+
smooth leaky-evidence throttle
+
epoch-local immutable fake-user overlay
+
Journal-derived recovery
```

不得只实现其中一部分。

尤其禁止以下降级版本：

```text
多个 blog 调用首个获胜
评分改回 bool
缺失字段导致整轮失败
未知字段直接丢弃而不做最近邻
按 7/8/9 手写阈值
只看本轮最高分、不积累低分
wall-clock cooldown
nudge 每轮重新渲染
nudge 跨 epoch 重锚
nudge 进入 BloggerDelta
物理 Y transcript 直接充当 B
Main 等待 Blogger
```

Blogger as Enforcer 的最终语义是：

> Blogger 继续维护可恢复的工作日志，同时以稀疏评分报告观察工程行为。主 Session 在不等待 Blogger 的前提下，通过一个平滑、确定、可重放的证据积分接收必要的工程异议。异议在当前 PrefixEpoch 内一经出现便成为稳定前缀的一部分；当上下文的身份边界真正切换时，它们与旧 epoch 一同自然退役。

该设计保留了万象术的核心架构纪律：

* 结构化程序替代程序计数器；
* 事件只作信号，事实来自完整 snapshot；
* 不修改 OpenCode；
* 领域事实进入 Journal；
* Projection 与事实分离；
* PrefixEpoch 内字节稳定；
* compaction 只按已发生的物理事件收容；
* 测试验证行为和契约，而非实现布局。

审阅建议：批准概念与规范，实施前先以 ENFORCER-180 第 0 步的九条 Host canary 作为阻断门。
# 十九、Rule Catalog SSOT

## ENFORCER-170：单一目录

Rule Catalog 是以下全部产物的唯一来源：

```text
Provider tool schema
字段 description
RuleId enum
Catalog order
Damerau–Levenshtein 候选集合
Canonical nudge text
静态文档
测试 fixture
schema digest
```

不得手工维护多份平行清单。

## ENFORCER-171：目录字段

处理分两层：

* 检测层：RuleId 分别评分；
* 反馈层：同一 NudgeKey 的分数取 max，throttle state 按 NudgeKey 维护，同一 NudgeKey 只渲染一行。

```fsharp
type EnforcementRule =
    { RuleId: RuleId
      FieldName: string
      NudgeKey: NudgeKey
      Family: RuleFamily
      Description: string
      CanonicalNudgeText: string
      CatalogOrdinal: int }
```

例如：

```text
ENF-F01 serial-when-parallel
ENF-I04 serial-investigation
    ↓
NudgeKey = unnecessary-serialization
```

这样既保留面面俱到的检查，又不会重复训话。

所有策略规则共用同一 0–9 评分语义，因此目录不重复定义分值含义。

Throttle 的输入、`NudgeAnchored.NudgeKeys` 和持久状态都应从 `RuleId` 改为 `NudgeKey`。

## ENFORCER-172：字段稳定性

RuleId 和 FieldName 一旦发布，不得重命名。

NudgeKey 一旦发布，不得重命名或改变其与 RuleId 的映射。

文案修改会改变 provider-facing schema 和未来 nudge 文本，应视为发布变更并经过 canary。

旧 NudgeAnchored 永远使用已持久化文本，不受目录更新影响。

---

# 二十、规则目录

以下 `Score when` 文本作为 schema description 的核心；`Nudge` 为固定渲染文本。

---

## A. 类型与表示

### ENF-A01 — `primitive-obsession`

Score when: A domain concept such as an account ID, order ID, money amount, path, digest, or capability crosses a meaningful boundary as an undifferentiated string, number, or boolean.

Nudge: A domain concept is crossing a boundary as a primitive. Introduce a distinct type so invalid substitutions become impossible.

### ENF-A02 — `boolean-blindness`

Score when: Multiple booleans encode independent meanings, modes, permissions, or lifecycle states and allow ambiguous call sites or invalid combinations.

Nudge: Boolean flags are hiding distinct domain meanings. Replace them with named cases or explicit types.

### ENF-A03 — `null-ambiguity`

Score when: `null`, missing, empty, or optional values conflate different outcomes such as absent, unauthorized, failed, not loaded, or not applicable.

Nudge: A nullable value is carrying several meanings. Model the outcomes as explicit alternatives.

### ENF-A04 — `illegal-state-representable`

Score when: Nullable fields and flags allow combinations that cannot exist in the real domain.

Nudge: Illegal domain states are representable. Encode each valid state explicitly and attach only the data meaningful in that state.

### ENF-A05 — `catch-all-swallows-future`

Score when: A wildcard, default branch, generic fallback, or broad catch silently absorbs future domain cases that should require explicit handling.

Nudge: A catch-all branch is hiding future cases. Make the match exhaustive so new states create a visible failure.

### ENF-A06 — `expected-failure-as-exception`

Score when: A foreseeable business outcome such as not found, unauthorized, insufficient balance, or invalid transition is represented by an exception.

Nudge: An expected business outcome is being treated as an exception. Return a typed result that forces callers to handle it.

### ENF-A07 — `stringly-typed-error`

Score when: Callers interpret error strings, localized text, message fragments, or regular expressions to determine program behavior.

Nudge: Program logic is parsing error prose. Replace the string contract with a closed typed error value.

### ENF-A08 — `weak-boundary-parsing`

Score when: Untrusted or cross-language data remains weakly typed after entering the system, forcing downstream code to repeatedly infer its shape.

Nudge: Boundary data was not normalized early enough. Parse and validate it once into a strong internal type.

### ENF-A09 — `type-erosion-at-boundary`

Score when: `any`, unchecked casts, reflection, dynamic property access, or unboxing escape the designated adapter boundary and enter domain logic.

Nudge: Type information is being discarded beyond the adapter boundary. Contain dynamic decoding and expose a typed contract.

### ENF-A10 — `runtime-checked-builder`

Score when: A complex object is built through setters or fluent mutation and only validated after construction, allowing incomplete intermediate states.

Nudge: Construction correctness is deferred to runtime. Encode the required construction stages or use one validated constructor.

---

## B. 控制流与规则表达

### ENF-B01 — `program-counter-state`

Score when: Stage, phase, lease, generation, next-action, current-step, owner, or equivalent fields encode where the program should execute next rather than a real-world fact.

Nudge: Control flow has been reified as mutable program-counter state. Replace it with structured control flow and local continuations.

### ENF-B02 — `rule-spaghetti`

Score when: A rule set is expressed through nested conditionals, temporary flags, mutation, and early exits such that the reader must simulate execution to recover the rule.

Nudge: The business rule is buried in control flow. Rewrite it so the rule can be read directly from the code.

### ENF-B03 — `missing-rule-combinator`

Score when: Three or more rules with the same input/output shape are manually chained instead of composed through a reusable validation or policy combinator.

Nudge: Repeated rule composition is being written by hand. Introduce a small combinator that exposes the shared rule algebra.

### ENF-B04 — `wrong-rule-composition`

Score when: Dependent rules collect meaningless downstream errors instead of short-circuiting, or independent rules stop early instead of returning the full error set.

Nudge: The rule composition strategy is wrong. Short-circuit dependent checks and accumulate independent failures.

### ENF-B05 — `implicit-control-flow`

Score when: Critical ordering depends on callbacks, registration order, hidden lifecycle hooks, global initialization, or undocumented framework conventions.

Nudge: Essential control flow is implicit. Make the ordering and ownership explicit in ordinary program structure.

### ENF-B06 — `callback-pyramid`

Score when: Nested callbacks or promise chains obscure resource scope, cancellation, error propagation, or the linear story of the operation.

Nudge: Nested continuations are obscuring the operation. Flatten the flow with structured async control and scoped resources.

### ENF-B07 — `exception-driven-control-flow`

Score when: Exceptions are intentionally thrown and caught to express ordinary branching, iteration, absence, or expected retries.

Nudge: Exceptions are being used as ordinary control flow. Replace them with explicit branches or typed results.

### ENF-B08 — `duplicated-control-flow`

Score when: The same workflow, retry sequence, validation order, or state transition algorithm is independently implemented in multiple places.

Nudge: The same control algorithm has multiple owners. Establish one canonical implementation and route all callers through it.

### ENF-B09 — `non-exhaustive-transition`

Score when: A finite state transition can silently ignore or generically accept a state/event pair that should be explicitly legal or illegal.

Nudge: State transitions are not exhaustive. Enumerate the legal cases and reject impossible transitions explicitly.

### ENF-B10 — `phase-flag-accumulation`

Score when: New flags are repeatedly added to patch interactions between lifecycle phases, producing combinatorial behavior.

Nudge: Lifecycle flags are accumulating into an implicit state machine. Replace the flag product with a smaller explicit model or structured flow.

---

## C. 架构边界与 DDD

### ENF-C01 — `boundary-collapse`

Score when: Modules with different invariants or lifecycles directly share internals, mutate each other’s state, or bypass explicit translation at the boundary.

Nudge: A context boundary has collapsed. Restore a clear interface and pass only the facts that genuinely cross it.

### ENF-C02 — `context-model-leak`

Score when: One shared model is reused across authentication, ordering, sessions, persistence, UI, or other contexts that assign it different meanings.

Nudge: One model is serving incompatible bounded contexts. Give each context its own concept and translate explicitly.

### ENF-C03 — `cross-layer-internal-import`

Score when: A higher or unrelated layer imports internal implementation members rather than a declared public boundary.

Nudge: A layer is reaching through another layer’s boundary. Depend on the public contract, not its internals.

### ENF-C04 — `cyclic-dependency`

Score when: Module, package, service, or project dependencies form a cycle or require mutual initialization.

Nudge: The dependency graph is cyclic. Identify the missing boundary or fact flow and restore one-way dependencies.

### ENF-C05 — `god-module`

Score when: One module owns several unrelated side-effect boundaries, policies, resources, or domains merely because they are currently convenient to colocate.

Nudge: One module owns unrelated responsibilities. Split it along real domain or side-effect boundaries, not arbitrary file size.

### ENF-C06 — `mixed-side-effect-boundaries`

Score when: A single function or module simultaneously owns unrelated effects such as storage, network, process control, UI, Git, and policy decisions.

Nudge: Unrelated side-effect boundaries are mixed together. Isolate each effect behind a narrow port and keep policy pure.

### ENF-C07 — `framework-tax`

Score when: Configuration, lifecycle hooks, dependency injection ceremony, generated layers, or framework conventions exceed the essential complexity of the problem.

Nudge: Framework ceremony is larger than the problem. Remove the framework tax and expose the underlying operation directly.

### ENF-C08 — `pattern-sprawl`

Score when: Class hierarchies, factories, visitors, strategies, or interfaces simulate behavior that sealed data, pattern matching, and first-class functions could express directly.

Nudge: Design-pattern scaffolding is obscuring a simpler algebraic model. Prefer closed data and direct composition.

### ENF-C09 — `premature-unification`

Score when: Similar-looking code or concepts with different lifecycles, invariants, or reasons to change are unified before they represent the same knowledge.

Nudge: Similarity was mistaken for shared knowledge. Separate concepts that change for different reasons.

### ENF-C10 — `duplicated-truth`

Score when: The same fact has multiple authoritative representations that can drift or already disagree.

Nudge: One fact has multiple sources of truth. Choose a single canonical representation and derive the others.

---

## D. 数据、效应与事件

### ENF-D01 — `in-place-mutation`

Score when: Shared or externally visible state is overwritten in place, destroying the explicit transition from the previous value to the next value.

Nudge: Shared state is being mutated in place. Compute a new value or record an explicit transition instead.

### ENF-D02 — `mutable-public-state`

Score when: Callers can directly modify fields that should be protected by invariants or domain behavior.

Nudge: Public mutable fields bypass the object’s rules. Encapsulate the state and expose invariant-preserving operations.

### ENF-D03 — `clone-and-mutate-derived`

Score when: A new domain value is created by cloning a mutable prototype and patching selected fields.

Nudge: A derived value is being made through clone-and-mutate. Construct the intended immutable value directly.

### ENF-D04 — `impure-core`

Score when: Core business decisions directly read clocks, random sources, databases, networks, environment state, or mutable globals.

Nudge: Business policy is entangled with effects. Move effects to the shell and pass explicit values into a pure core.

### ENF-D05 — `time-source-in-logic`

Score when: Domain logic reads the current clock internally instead of receiving an explicit time value or clock port.

Nudge: Time is an implicit dependency. Inject the relevant instant or clock so behavior is deterministic and testable.

### ENF-D06 — `random-source-in-logic`

Score when: Domain logic generates randomness internally and cannot be replayed from explicit input.

Nudge: Randomness is hidden inside policy. Inject a seed or random source and preserve replayability.

### ENF-D07 — `command-event-confusion`

Score when: An intention is stored as though it already happened, or an immutable fact is later revalidated and rejected using today’s rules.

Nudge: Commands and events are being conflated. Validate intentions now, then record completed facts as immutable history.

### ENF-D08 — `fragment-event-as-data`

Score when: Partial stream events, deltas, update ordering, or transport fragments are assembled into business facts instead of reading a complete snapshot.

Nudge: Transport fragments are being treated as domain data. Use events only as wake-up signals and read the complete authoritative snapshot.

### ENF-D09 — `snapshot-as-truth`

Score when: A cache, projection, snapshot, or summary is treated as the original fact source rather than a derived bookmark.

Nudge: A derived snapshot is being treated as truth. Recover from the authoritative facts and rebuild the projection.

### ENF-D10 — `overwrite-history`

Score when: Previously committed facts are edited or deleted to represent correction instead of appending a compensating or superseding fact.

Nudge: History is being rewritten. Preserve the original fact and append an explicit correction or replacement.

---

## E. 持久化与恢复

### ENF-E01 — `memory-before-disk`

Score when: Authoritative in-memory state is changed before the durable fact that justifies the change is committed.

Nudge: Memory was updated before durability. Commit the fact first, then derive runtime state from it.

### ENF-E02 — `blob-after-event`

Score when: A journal event referencing large content is appended before the referenced blob is durably written.

Nudge: A durable event can point to missing content. Write and verify the blob before appending the reference.

### ENF-E03 — `partial-write-assumption`

Score when: Recovery logic assumes an append or effect may be partially committed despite the storage contract defining committed versus unknown outcomes.

Nudge: Recovery is inventing a partial-write state. Follow the storage contract’s explicit committed and unknown outcomes.

### ENF-E04 — `unversioned-schema`

Score when: A durable event, file, wire shape, or cache format changes without an explicit schema version and compatibility rule.

Nudge: A persistent contract changed without versioning. Add an explicit version and deterministic compatibility policy.

### ENF-E05 — `guessed-migration`

Score when: Old durable data is heuristically interpreted or silently upgraded without a specified migration.

Nudge: An old schema is being guessed into a new one. Use an explicit migration or fail closed.

### ENF-E06 — `log-as-recovery-protocol`

Score when: Diagnostic logs, log messages, or log ordering are used to decide what durable business work occurred.

Nudge: Diagnostic logs are being used as recovery facts. Recover from the journal and authoritative external state instead.

### ENF-E07 — `recovery-by-filesystem-state`

Score when: Recovery infers workflow progress from incidental files, directories, temporary artifacts, or working-tree shape instead of durable lifecycle facts.

Nudge: Recovery is guessing progress from filesystem residue. Record the lifecycle fact explicitly and recover from it.

### ENF-E08 — `truncation-skips-damaged`

Score when: Recovery skips corruption in the middle of durable history and continues applying later facts.

Nudge: Recovery is continuing past corrupted history. Only a final incomplete record may be truncated; interior corruption must fail closed.

### ENF-E09 — `optimistic-retry-assumption`

Score when: An external effect is retried because its result is unknown, without an idempotency identity or at-most-once recovery contract.

Nudge: An unknown external effect is being retried optimistically. Establish idempotency or an explicit at-most-once protocol.

### ENF-E10 — `retry-not-idempotent`

Score when: A retryable operation can duplicate writes, prompts, publications, charges, processes, or resource creation.

Nudge: A retryable effect is not idempotent. Add a stable identity and prove repeated execution is safe.

---

## F. 并发与资源

### ENF-F01 — `serial-when-parallel`

Score when: Independent tool calls, reads, validations, or investigations are performed sequentially without a dependency requiring the order.

Nudge: Independent work is being serialized. Run it concurrently with a clear bound and deterministic result ordering.

### ENF-F02 — `unbounded-fanout`

Score when: Tasks, requests, subprocesses, agents, or file operations are spawned without a finite concurrency bound.

Nudge: Concurrency is unbounded. Use a bounded map or semaphore and define cancellation behavior.

### ENF-F03 — `shared-mutable-concurrency`

Score when: Concurrent workers coordinate by mutating shared state protected by ad hoc locks rather than owning state or exchanging messages.

Nudge: Concurrent workers share mutable state. Prefer ownership, message passing, or a single serialized writer.

### ENF-F04 — `blocking-event-loop`

Score when: A synchronous wait, blocking process, filesystem call, sleep, or CPU-heavy loop runs on an event-loop or hook thread.

Nudge: Blocking work is running on the event loop. Move it behind an asynchronous boundary or worker.

### ENF-F05 — `cancellation-not-propagated`

Score when: A cancellation token or abort signal stops at an outer layer while inner network, process, tool, or child work continues.

Nudge: Cancellation does not reach owned work. Propagate the cancellation signal through every resource boundary.

### ENF-F06 — `permit-leak`

Score when: A semaphore, gate, lock, lease, or capacity permit can be lost on exceptions, cancellation, or early return.

Nudge: A concurrency permit can leak. Acquire it through a scoped construct that guarantees release.

### ENF-F07 — `resource-not-scoped`

Score when: Files, processes, streams, sessions, subscriptions, worktrees, or handles are not tied to a deterministic lifetime.

Nudge: A resource lacks scoped ownership. Make acquisition and disposal part of one structured lifetime.

### ENF-F08 — `race-first-wins-semantics`

Score when: Scheduling order or the first completing concurrent call determines a domain result even though the calls may carry different information.

Nudge: A scheduler race is deciding business semantics. Collect the complete set and merge it deterministically.

### ENF-F09 — `lost-update`

Score when: Concurrent writers perform read-modify-write without version checking, compare-and-swap, serialization, or another conflict protocol.

Nudge: Concurrent updates can overwrite each other. Add a versioned compare-and-swap or a single writer.

### ENF-F10 — `sleep-based-synchronization`

Score when: Fixed sleeps or delays are used to wait for readiness, completion, ordering, or propagation.

Nudge: A fixed sleep is standing in for causality. Wait for an explicit signal or observable state transition.

---

## G. TDD 与测试

### ENF-G01 — `ignored-tdd`

Score when: Production behavior is implemented or changed before a failing behavioral test demonstrates the required outcome.

Nudge: TDD order was skipped. Add a failing behavioral test before changing the implementation.

### ENF-G02 — `missing-regression-test`

Score when: A defect is fixed without a test that fails on the old behavior and passes on the corrected behavior.

Nudge: A bug fix lacks a regression test. Capture the failure before considering the fix complete.

### ENF-G03 — `test-implementation-coupled`

Score when: A test asserts private structure, call counts, helper layout, internal fields, or incidental algorithm choices instead of observable behavior.

Nudge: A test is coupled to implementation details. Assert the public behavior and durable contract instead.

### ENF-G04 — `weakened-test-to-pass`

Score when: Assertions, cases, fixtures, or expected outcomes are removed or weakened primarily to make a failing test pass.

Nudge: The test was weakened instead of fixing the defect. Restore the behavioral expectation and repair the implementation.

### ENF-G05 — `flaky-test-tolerated`

Score when: A nondeterministic test is accepted, quarantined indefinitely, or treated as harmless.

Nudge: A flaky test is being tolerated. Find and remove the nondeterminism before relying on the suite.

### ENF-G06 — `repeat-until-pass`

Score when: A test or command is rerun until it happens to succeed, and the successful repetition is treated as verification.

Nudge: Repetition is hiding a nondeterministic failure. Make one run deterministic instead of retrying until green.

### ENF-G07 — `time-dependent-test`

Score when: A test depends on real current time, wall-clock delays, time zones, or timing luck.

Nudge: A test depends on real time. Inject time and make the scenario deterministic.

### ENF-G08 — `order-dependent-test`

Score when: A test passes only after another test, depends on global residue, or changes behavior with suite ordering.

Nudge: A test depends on execution order. Give every test isolated, explicit setup and cleanup.

### ENF-G09 — `failure-path-untested`

Score when: New error handling, cancellation, rollback, retry, malformed input, or recovery behavior has no direct test.

Nudge: A newly introduced failure path is untested. Add a test that exercises the actual failure and its observable result.

### ENF-G10 — `contract-test-missing`

Score when: A Host, provider, storage, process, network, plugin, or language boundary is changed without a contract-level test.

Nudge: A boundary contract changed without a contract test. Verify the exact input, output, identity, and failure semantics.

---

## H. 验证与门禁

### ENF-H01 — `unverified-completion-claim`

Score when: Work is declared complete without running the relevant tests, checks, build, reproduction, or observable verification.

Nudge: Completion was claimed without verification. Run the relevant behavioral checks and report the actual result.

### ENF-H02 — `ephemeral-verification`

Score when: A one-off shell command, temporary script, manual probe, or debug print is the only proof and is not converted into a durable test or gate.

Nudge: Verification exists only as an ephemeral probe. Preserve it as a repeatable test, script, or canary.

### ENF-H03 — `false-gate`

Score when: A gate can remain green because it scans the wrong path, matches nothing, ignores failures, or checks a condition that cannot fail.

Nudge: A quality gate can pass without checking the intended property. Add a self-test that proves the gate turns red.

### ENF-H04 — `coverage-theater`

Score when: A test or metric increases coverage but does not assert meaningful behavior, identities, values, or failure outcomes.

Nudge: Coverage is being mistaken for verification. Add assertions that would fail under a realistic defect.

### ENF-H05 — `property-test-missing`

Score when: A parser, serializer, fold, state transition, merge, normalization, round trip, or algebraic operation is tested only with a few examples despite clear general invariants.

Nudge: A general invariant is covered only by examples. Add property-based tests for the full input space.

### ENF-H06 — `behavioral-boundary-untested`

Score when: A public behavior is tested only through private helpers and never through the real supported entry point.

Nudge: The behavior is verified only below its contract boundary. Test it through the real public entry point.

### ENF-H07 — `canary-skipped`

Score when: A behavior that depends on undocumented Host or provider ordering is changed without a real integration canary.

Nudge: An undocumented Host assumption lacks a canary. Prove it against the real boundary before release.

### ENF-H08 — `release-ladder-skipped`

Score when: Validation jumps directly to a high-level test or release without passing the required lower-level pure, contract, replay, and canary stages.

Nudge: The verification ladder was skipped. Pass each lower-level gate before promoting the change.

### ENF-H09 — `timeout-inflated-to-pass`

Score when: A timeout or retry budget is increased mainly to turn a failing test or hanging operation green.

Nudge: A larger timeout is masking the failure. Fix the missing causal signal or resource leak instead.

### ENF-H10 — `mock-hidden-state`

Score when: A mock changes responses using an invisible cursor, mutable scenario state, request count, or time rather than provider-visible request content.

Nudge: The mock depends on hidden state. Make each response a pure function of the visible request.

---

## I. 调查与工作方法

### ENF-I01 — `guessed-not-verified`

Score when: Behavior, API shape, file content, Host semantics, or failure cause is asserted without reading the source or running a direct check.

Nudge: A material claim was guessed rather than verified. Inspect the authoritative source or run a targeted experiment.

### ENF-I02 — `blind-edit`

Score when: Code is changed before locating the true owner, reading surrounding contracts, or understanding the affected call path.

Nudge: The implementation was edited before the governing context was understood. Read the owner and boundary contracts first.

### ENF-I03 — `tool-error-ignored`

Score when: A tool, command, test, patch, search, or process reports an error that is skipped or treated as irrelevant without resolution.

Nudge: A tool error was ignored. Resolve or explicitly account for it before proceeding.

### ENF-I04 — `serial-investigation`

Score when: Independent searches, file reads, source inspections, or diagnostics are performed one by one despite having no dependency.

Nudge: Independent investigation is unnecessarily serial. Run the reads and searches concurrently, then synthesize the evidence.

### ENF-I05 — `wholesale-rewrite`

Score when: A broad rewrite, generated replacement, or large delete-and-recreate operation is chosen instead of a precise change preserving known-good structure.

Nudge: A wholesale rewrite is replacing a targeted repair. Make the smallest structurally correct change.

### ENF-I06 — `dirty-hack`

Score when: A fallback, bypass, compatibility shim, duplicated path, or special case is added to avoid repairing the underlying model or boundary.

Nudge: A workaround is hiding the root cause. Repair the governing abstraction or invariant instead.

### ENF-I07 — `guess-based-fix`

Score when: Changes are tried speculatively until symptoms disappear without a causal explanation or regression test.

Nudge: The fix is based on trial and error. Establish the cause, then encode it in a regression test.

### ENF-I08 — `premature-optimization`

Score when: Complexity is introduced for performance before a measured bottleneck or explicit resource constraint exists.

Nudge: Optimization was introduced without evidence of a bottleneck. Keep the simple design until measurement justifies complexity.

### ENF-I09 — `big-batch-intent`

Score when: A large ambiguous task is handed to one operation or agent instead of decomposing independent, reviewable units.

Nudge: The work was bundled into one oversized intent. Split it into explicit independent outcomes and execute them with bounded concurrency.

### ENF-I10 — `half-finished-refactor`

Score when: Old and new structures coexist without a completed migration, leaving duplicated ownership, temporary adapters, or inconsistent conventions.

Nudge: A refactor stopped halfway. Finish the ownership transfer and remove the obsolete path.

---

## J. 交付卫生与安全

### ENF-J01 — `scope-creep`

Score when: The implementation expands into unrelated behavior, cleanup, migration, or redesign not required by the current task or governing architecture.

Nudge: The change has expanded beyond its justified scope. Separate unrelated work and keep this delivery focused.

### ENF-J02 — `leftover-scaffolding`

Score when: Temporary files, experimental branches, probes, fixtures, flags, scripts, or migration scaffolding remain in the delivered result without a permanent role.

Nudge: Temporary scaffolding remains in the delivery. Remove it or promote it into a maintained tool with a clear owner.

### ENF-J03 — `legacy-cruft-retained`

Score when: Obsolete code, aliases, compatibility branches, or old names are kept despite an explicit clean-break policy.

Nudge: Obsolete compatibility code is being retained. Complete the clean break and remove the old surface.

### ENF-J04 — `dead-code-delivered`

Score when: Unreachable, unused, superseded, or unreferenced production code is left behind.

Nudge: Dead production code remains. Delete it and let version control preserve history.

### ENF-J05 — `todo-bomb`

Score when: A TODO, FIXME, placeholder, unimplemented branch, or temporary panic defers work required for correctness.

Nudge: Required correctness has been deferred to a TODO. Finish the behavior or explicitly reject the incomplete change.

### ENF-J06 — `commented-out-code`

Score when: Old implementation code is retained in comments instead of being removed.

Nudge: Commented-out code is being used as storage. Delete it and rely on version control.

### ENF-J07 — `debug-print-left`

Score when: Temporary logging, tracing, dumps, breakpoints, or debug output remains in production paths.

Nudge: Temporary debugging output remains. Remove it or convert it into intentional structured diagnostics.

### ENF-J08 — `secret-in-code`

Score when: A password, token, private key, credential, or sensitive value is embedded in source, fixtures, logs, prompts, or committed configuration.

Nudge: Sensitive material appears in code or committed data. Remove and rotate it, then use the approved secret boundary.

### ENF-J09 — `destructive-without-authorization`

Score when: Data, files, branches, worktrees, resources, or external state are deleted or overwritten without explicit authority and a verified target.

Nudge: A destructive action lacks explicit authorization or target verification. Stop and establish both before proceeding.

### ENF-J10 — `dependency-bloat`

Score when: A new dependency, plugin, service, or framework is added for behavior that the existing platform or a small local implementation already provides safely.

Nudge: A dependency was added without proportional value. Use the existing platform or a smaller direct implementation.

---

## K. 知识、决策与架构维护

### ENF-K01 — `unrecorded-lesson`

Score when: A reusable engineering lesson, debugging discovery, provider quirk, or recovery principle emerges but is not recorded durably.

Nudge: A reusable lesson emerged but was not recorded. Capture it with the skill-creator tool so it survives this session.

### ENF-K02 — `repeated-known-mistake`

Score when: Current work repeats a mistake, failed approach, or violated constraint already present in the work log or project guidance.

Nudge: The work is repeating a previously recorded mistake. Re-read the existing lesson and change the approach.

### ENF-K03 — `unrecorded-decision`

Score when: A material architecture choice, rejected alternative, compatibility decision, or operational tradeoff is made without a durable decision record.

Nudge: A material decision lacks a durable record. Document the choice, rationale, rejected alternatives, and consequences.

### ENF-K04 — `missing-invariant-documentation`

Score when: A non-obvious invariant is essential to correctness but exists only in implementation details or tribal knowledge.

Nudge: A critical invariant is undocumented. State it at the owning contract and add a mechanical guard where possible.

### ENF-K05 — `stale-documentation`

Score when: Code or behavior changes while authoritative documentation, schemas, examples, or diagrams continue to describe the old contract.

Nudge: The implementation and authoritative documentation disagree. Update the owning specification in the same change.

### ENF-K06 — `facade-hides-mess`

Score when: A new facade or wrapper makes an unhealthy architecture look clean while leaving duplicated ownership or boundary violations underneath.

Nudge: A facade is concealing unresolved architecture. Repair the underlying ownership and dependency structure.

### ENF-K07 — `manual-toil-repeat`

Score when: A repeated mechanical procedure is performed manually again despite being deterministic and suitable for a script, generator, or reusable skill.

Nudge: Repeated mechanical work remains manual. Automate it and preserve the procedure as a maintained tool.

### ENF-K08 — `spike-not-cleaned`

Score when: Experimental code or a proof of concept is promoted without replacing shortcuts, hard-coded assumptions, and missing contracts.

Nudge: A spike is being shipped as production design. Rebuild it around explicit contracts and remove experimental shortcuts.

### ENF-K09 — `compatibility-cruft`

Score when: Compatibility layers, aliases, duplicate formats, or dual paths are added without a real external compatibility requirement.

Nudge: Compatibility machinery lacks a justified external contract. Remove the duplicate path and keep one canonical interface.

### ENF-K10 — `missing-architecture-gate`

Score when: A critical boundary or forbidden dependency relies only on team discipline even though a static architecture gate could enforce it.

Nudge: An architecture boundary relies on memory alone. Add a static gate that fails when the boundary is crossed.

---

## L. 命名、表达与偶然复杂度

### ENF-L01 — `misleading-name`

Score when: A name suggests stronger guarantees, different ownership, broader scope, or a different domain meaning than the implementation provides.

Nudge: A name misrepresents the concept or guarantee. Rename it to match the actual domain fact.

### ENF-L02 — `abbreviation-anxiety`

Score when: Unfamiliar, overloaded, or unnecessary abbreviations force readers to decode names repeatedly.

Nudge: Abbreviations are increasing cognitive load. Use the full domain term unless the abbreviation is genuinely universal here.

### ENF-L03 — `math-flavored-name`

Score when: Mathematical symbols or abstract single-letter names are used without a real algebraic model and make ordinary domain code harder to read.

Nudge: Mathematical naming is obscuring an ordinary domain concept. Use names that expose the actual meaning.

### ENF-L04 — `generic-helper-bucket`

Score when: Files or modules named helpers, utils, common, core, service, primitives, or misc collect unrelated operations without one governing concept.

Nudge: A generic helper bucket is hiding missing ownership. Move each operation to the domain or boundary that owns it.

### ENF-L05 — `translator-layer-bloat`

Score when: Translator, broker, governor, coordinator, manager, adapter, or mediator layers merely forward calls without enforcing a real boundary or transformation.

Nudge: A forwarding layer adds ceremony without a concept. Remove it or give it a genuine invariant and ownership boundary.

### ENF-L06 — `implicit-convention-magic`

Score when: Correctness depends on file names, registration order, reflection, annotations, directory placement, or framework discovery that is not mechanically visible at the call site.

Nudge: Correctness depends on hidden convention. Replace it with an explicit typed registration or contract.

### ENF-L07 — `comment-theater`

Score when: Comments narrate obvious syntax, apologize for complexity, or describe intent that should be expressed through names and structure.

Nudge: Comments are compensating for unclear code. Improve the structure and keep comments only for durable non-obvious constraints.

### ENF-L08 — `status-announcement-noise`

Score when: Production output, code comments, logs, or agent messages repeatedly announce routine progress without conveying a decision, result, failure, or required action.

Nudge: Status announcements are adding noise. Report only decisions, meaningful progress, failures, and actionable results.

### ENF-L09 — `domain-language-drift`

Score when: Several names refer to the same concept, or one name is used for several different concepts across modules.

Nudge: Domain language is drifting. Choose one term per concept and separate concepts that currently share a name.

### ENF-L10 — `incidental-complexity-dominates`

Score when: Configuration, glue, wrappers, lifecycle management, serialization ceremony, or framework rituals occupy more attention than the actual domain problem.

Nudge: Incidental complexity is dominating the design. Remove ceremony until the essential domain concepts become the visible structure.

---

