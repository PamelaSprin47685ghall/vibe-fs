# 问题 3：context budget 与 todowrite 触发

## 一、Flow 管线位置

本问题映射到管线上的 **`scan` 内部**。`ContextBudgetProjection` 作为 Step.State 的一部分，在每次 Input 进入 `scan` 时更新。预算判定作为 effect（注入 budget nudge synthetic message），在消息变换完成后、发送前计算。

## 二、当前实现存在的关键断点

### 断点 A：拿不到 token 就直接退出

当前 `resolveCurrentTokens` 的行为是：有实时 token count 则使用；否则有历史 `LastUsage` 按字节比例估算；两者都没有返回 `None`。随后 `applyContextBudget` 直接返回原消息，完全跳过预算保护。

> 最需要保护的未知状态，反而完全关闭保护。

### 断点 B：MaxInputTokens 可能变成 0

`resolveMaxInputTokens` 在同步和异步解析都失败后可能返回 0。`applyContextBudget` 又在 `MaxInputTokens <= 0` 时直接关闭机制。OpenCode 的 model limit 依赖 provider list 和最近 user model，provider API 不可用、model 解析失败、读取不到 limit 时可能把上限解析为 0。

### 断点 C：使用的是旧 assistant usage，不是即将发送的 prompt

最近一次 assistant 记录的 input/cache token，只表示上一轮 host 报告的使用量。它不一定包含当前 backlog projection、当前 caps、新加入的 synthetic message、message transform 后的最终消息、compaction 后的新上下文。

### 断点 D：R 的计算可能混入全部历史 todo

若完成 todo 的数量是从全部 flatten messages 统计，旧阶段做过的 todowrite 会持续影响当前公式。正确的 R 应是当前 budget episode 或当前 phase 开始后成功完成的 todo checkpoint 数量。

### 断点 E：NudgeTrack 被写入但没有真正参与防重和状态推进

系统可能记录 `EmergencySignaled`，但后续判定没有严格依据这个状态来决定是否已经发过、是否收到 todowrite、是否重置阶段、是否需要升级处理。

### 断点 F：message transform cache 掩盖了预算变化

如果缓存 key 只依赖原始输入消息，而模型 limit、usage observation、context generation 变化了，系统可能直接返回旧 transform 结果，不重新计算预算。

### 断点 G：开局误触发（增补稿 V）

当前 `rebuildPhaseState` 在第一次观测且 backlog 为空时将 `phaseBaseTokens = 0`。随后同一次 message transform 立即调用 `classifyPressure`。对于默认 `foldAfterFirst=false`，N=3，初始阈值约为有效窗口的 25%。CAPS、系统提示、用户首条消息本身可能已超过窗口 25%，于是系统把"开局就存在的固定上下文"错误认成"本阶段新增消耗"，立刻触发紧急 todowrite。

第一次没有真实 token usage 时当前兜底是 `totalBytes / 2`，属于未经校准的粗估，也可能严重高估首轮 token。

## 三、数学量语义

### 3.1 定义

* `L`：模型有效输入上限；
* `O`：为输出、工具、系统余量保留的 token；
* `B = L - O`：真正可用输入预算；
* `C`：当前最终 outbound prompt 的 token 数；
* `P`：一个 phase 预计新增的 token；
* `N`：允许继续的阶段数量；
* `R`：当前 budget episode 中已完成的 checkpoint 数；
* `A`：当前 phase 起点的 token 数；
* `G`：context generation。

### 3.2 关键约束

* 所有 host 对 L、O、C 使用同样定义；
* R 不从全历史计算；
* C 必须尽可能接近最终发送内容。

### 3.3 R 的正确计算

```text
phaseStartTodoOrdinal
currentTodoOrdinal
R = currentTodoOrdinal - phaseStartTodoOrdinal
```

Compaction、模型切换、context generation 变化时，都必须明确决定是否开启新 phase。

## 四、ContextBudget 状态机

* Healthy
* Approaching
* EmergencyRequired
* EmergencySignaled
* TodoObserved
* PhaseReset
* CompactionRequired
* MeasurementDegraded

### Healthy → EmergencyRequired

根据公式达到边界时进入。

### EmergencyRequired → EmergencySignaled

只注入一次 context budget nudge，并记录：episode ID、计算时的 C、B、阈值左右两侧、model、measurement source、context generation。

### EmergencySignaled → TodoObserved

必须观察到一次真正成功的 todowrite tool result，而不是仅仅看到 LLM 说"我会写 todo"。

### TodoObserved → PhaseReset

成功提交 backlog 后：更新 phase base、更新 todo ordinal、递增 phase generation、清除本 episode 的 signaled 状态。

### EmergencySignaled 长时间无进展

有限次数升级：
1. 第一次普通 nudge；
2. 第二次更强制的 nudge；
3. 再不执行则进入 CompactionRequired 或阻止继续扩展上下文。

不能每次 message transform 都无限重复插入。

## 五、token 测量分层

按可靠性排序：

1. host 的精确 tokenizer，对最终 encoded outbound messages 计数；
2. provider/model 官方 tokenizer；
3. 已校准的字节/token 上界估算；
4. 保守固定比例估算。

无法精确测量时进入 `MeasurementDegraded`，但仍执行保护。降级估算应该偏保守，宁可早一点 todowrite，也不能完全不触发。

### 5.1 区分 ObservedUsage 和 EstimatedOutbound

必须区分：

**ObservedUsage**：上一轮 Host/Provider 报告的实际 usage。

**EstimatedOutbound**：本轮经过所有 message transform 后，将要发送给模型的消息估算值。

Context Budget 不能只使用 ObservedUsage，因为新 tool result 尚未计入、backlog projection 尚未计入、caps/system prompt 变化尚未计入、review task 重注入尚未计入、budget nudge 自身尚未计入。

### 5.2 AI SDK v6 token 字段正确解释

```text
tokens.total
tokens.input
tokens.output
tokens.reasoning
tokens.cache.read
tokens.cache.write
```

* `tokens.total` 优先用于上一轮整体 usage；
* `tokens.input` 是经过 cache 调整后的值；
* 若重新构造原始 input，应把 cache read/write 加回。

推荐规则：

```text
若 total 有效：
  ObservedUsage = total
否则：
  ObservedUsage = input + cache.read + cache.write + output + reasoning
```

但每个 Host codec 仍须通过测试确认字段语义。

### 5.3 不再只读取最后一条 assistant

应从后向前寻找：非空、数值合法、context generation 匹配、非 title、可识别 usage 语义的最新 observation。跳过 synthetic assistant。只能选择最新有效快照，不能把多条 assistant usage 相加。

如果每条 assistant 的 `input` 已经代表该轮完整上下文，则遍历历史后只能选择最后一个有效值，否则会严重重复计算。

### 5.4 最终预算输入

```text
C = max(LatestObservedUsage, EstimatedFinalOutbound)
```

如果两者都不可用，进入 `MeasurementDegraded`，使用保守字符/字节估算，不能返回 None 后关闭机制。

### 5.5 Bootstrap estimator

当 Host 没有提供实时 token counts，且历史 `LastUsage` 也为空时，必须引入多语境保守估计器：

* 检测内容是否包含中文字符。若是，采用偏高估计（如每 2 个字符 1 个 token）；
* 若全为代码，按已知的高密度估计；
* 计算出的初始估算值必须乘以安全系数（如 1.25）；
* 显式标记此测量为 `MeasurementDegraded`。

不能使用单一的 `bytes / 4` 判定。中文字符在 UTF-8 中占用 3 个字节，但在很多 tokenizer 中一个汉字就是一个或半个 token，字节/token 比例接近 3:1。代码、长路径、YAML 标记的 token 密度可能会根据标点符号发生巨大抖动。

### 5.6 UsageConfidence

token 数值应携带可信度：

| 来源 | 可信度 | 可否触发 emergency |
| :--- | :--- | :--- |
| Host 返回的真实 usage | Observed | 是 |
| 基于上次真实 usage 的 bytes 比例估算 | CalibratedEstimate | 是 |
| 第一次 `bytes / 2` 粗估 | BootstrapEstimate | 否 |

第一次只有粗估时：仅记录 baseline，不触发 todowrite。至少取得一次真实 usage 或完成一次校准后，才允许进行压力判定。

## 六、model limit 解析

### 6.1 EffectiveContextLimit

集中建立 `EffectiveContextLimit` 解析器，返回的不只是数值，还包括：limit、source、model、是否缓存、缓存年龄、是否降级、reserve、failure reason。

### 6.2 优先级

OpenCode 模型 limit 为 `context`、`input?`、`output`。

推荐顺序：
1. `limit.input`，如果存在；
2. `limit.context - output reserve`；
3. 同模型最近成功缓存；
4. model family 保守值；
5. Host 配置默认；
6. 全局保守默认。

### 6.3 禁止行为

* 官方在 limit 缺失时把 context 设为 0，最终禁用 compaction。万象术不能照搬该行为。
* 不能使用固定的 100,000 或 120,000 token 作为所有未知模型的默认上限。未知模型可能只有 8K、16K 或 32K。错误使用 100K 会让 nudge 迟到数倍。
* 任何解析出的 limit 若低于 8192，一律回落到该 Host 的保守默认水位（如 16384 或 32768）。
* 全局默认必须偏小，并明确记录其 provenance，不能只返回一个无法审计的整数。

### 6.4 Reserve 与 OpenCode 语义协调

```text
EffectiveInputBudget = min(explicit input limit, context limit - output reserve)
```

reserve 应由 model output limit、tool call 余量、reasoning 模型额外余量、Host 配置共同决定。

### 6.5 provider list 临时不可用时

使用最近一次成功缓存；没有缓存时使用该 host 的安全保守默认；不得返回 0 并静默关闭。

Opencode、OMP、Mux 应共享这一逻辑，不要各自采用不同 reserve。

## 七、消息流水线中的正确位置

预算判定应放在：
1. 消息清洗；
2. backlog projection；
3. caps/system prompt 注入；
4. compaction 结果处理；
5. 其他稳定 synthetic 内容加入；

之后。然后：
6. 对将要发送的最终消息计算 token；
7. 决定是否加入 context-budget nudge；
8. 加入 nudge 后再次验证不会超出硬上限。

预算自身生成的 nudge 也占 token，不能假装它是免费的。

## 八、修复缓存

message transform 缓存 key 只能由结构化身份与单调计数构成：`modelRevision`、`modelLimitRevision`、`ContextUsageRevision`、`BacklogRevision`、`BudgetRevision`、`contextGeneration`、`CapsRevision` 及对应 scope/policy/phase 标识。不得计算或使用 raw message fingerprint、内容 hash 或其他内容摘要作为 key；最终 outbound 的规范化字节/稳定前缀相等只能作为可观察验证。

更干净的方案：稳定消息变换可以缓存；context budget 判定移到缓存之后，每次发送前重新计算。

## 九、todowrite 成功后的 phase 更新

收到本地 `work_backlog_committed` 后不应直接用旧 token 数值建立新 phase base。

正确流程：
1. 增加 todo checkpoint ordinal；
2. 标记当前 BudgetEpisode 完成；
3. 状态转为 `RebaseRequired`；
4. 清除本 episode 的重复 nudge；
5. 使 message transform cache 失效；
6. 下一轮对最终 outbound context 重新测量；
7. 建立新的 phase base。

当前实现已经在 backlog 与 `LastBacklog` 不同时重建 phase state 并更新 store。需要新增的重点是保证 event 到达后缓存必然失效、todo checkpoint ordinal 正确推进、下一次 transform 不返回旧缓存、phase 重建使用当前 context generation。

## 十、NudgeTrack 真正参与判定

至少区分：
* NotSignaled
* IncludedInOutboundPrompt
* AssistantProgressObserved
* TodoCommitted
* RetryAllowed
* Exhausted
* InvalidatedByCompaction
* InvalidatedByCancel

不能只写入 `EmergencySignaled`，却在下一轮继续无条件附加同一 prompt。

### 10.1 NudgeTrack 要表示 episode，而不是 transform 次数

当前 synthetic nudge 每次 transform 会被剥离，随后可能重新注入，并增加 `NudgeCount`。

更稳妥的状态应记录：
* context budget episode ID；
* signal 时的 todo ordinal；
* signal 时 tokens；
* stable synthetic nudge ID。

在压力仍成立且 todo ordinal 未推进时：输出中继续包含同一条提醒；不把每次 message transform 计作新提醒；不不断生成新 GUID；不快速耗尽"最多 2 次"限制。

只有以下情况才建立新提醒 episode：LLM 完成一次 todowrite 但压力仍再次跨阈值；phase baseline 重建；compaction 后重新进入压力区。

## 十一、开局防误触发

### 11.1 第一次观测只做校准，绝不触发提醒

`rebuildPhaseState` 应返回额外信息：是否为本次刚初始化的 baseline。若本次刚初始化：保存 state、保存 usage、不调用 emergency injection、直接返回原消息。

### 11.2 backlog 为空时也必须使用真实初始 baseline

删除特殊语义 `State=None && backlog.IsEmpty → phaseBaseTokens=0`。统一为 `phaseBaseTokens = stableTokens`。

即使 backlog 为空，CAPS、系统消息、首条用户消息也属于阶段基线，不属于阶段新增消耗。

初始化时若当前 tokens = 30,000，phase base = 30,000，则新增量为 0，不会立刻触发。

### 11.3 必须有正的阶段增长

在判定 emergency 前增加基本条件：`currentTokens > phaseBaseTokens` 或新增量超过最小噪声区间。避免 token 统计轻微抖动造成提醒。

### 11.4 phase base 已超过窗口 80% 时走 compaction，不再要求 todowrite。

## 十二、验收测试

### 开局测试

1. CAPS 很大，占窗口 30%：第一轮不提醒。
2. 没有真实 usage，只能 bytes/2：第一轮不提醒。
3. 首次 transform 连续调用两次：仍不提醒，baseline 不重复漂移。

### 正常触发测试

4. baseline 之后新增 token 未达阈值：不提醒。
5. 新增 token 跨过 F 阈值：注入一次紧急提醒。
6. 同一个消息集合重复 transform：提醒仍可见，不增加提醒 episode 数，synthetic ID 不变化。
7. 成功 todowrite：baseline 重建，提醒消失，不立即再次触发。
8. investigator 上下文超过阈值：不出现 todowrite 提醒。
9. phase base 已超过窗口 80%：走 compaction，不再要求 todowrite。

### 额外必测场景

* 公式边界前 1 token、正好边界、边界后 1 token；
* N、R、P 的性质测试；
* 历史存在 100 次 todowrite，但当前 phase 的 R 仍为 0；
* provider API 不可用；
* tokenizer 不可用；
* 第一次会话没有历史 usage；
* 中途从小模型切换到大模型；
* 中途从大模型切换到小模型；
* compaction 后 phase 正确重置；
* todowrite 失败时不能算 TodoObserved；
* 相同输入消息、usage 增长后必须重新判定；
* Opencode 和 OMP 对同一输入做出相同决策；
* 已经 Signaled 时不会连续插入重复 prompt；
* 自动 continue 期间预算 nudge 不与 fallback 抢 owner；
* 最后一条 assistant 无 usage 时能找到最新有效 observation；
* 不重复累加 cache.read；
* 大型 tool result 在本轮估算中被计入；
* transform 新增内容被计入；
* limit=0 时进入保守默认；
| work backlog commit 后下一轮必然重建 phase；
| 同一 budget episode 不产生无界重复 nudge；
| compaction 后 context generation 正确变化。
