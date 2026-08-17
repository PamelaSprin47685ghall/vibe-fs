# degeneration-guard — 唯一 normative 合同

条款 ID 前缀：`DG-`。本文每个命题都是当前世界必须同时成立的事实；测试落点见 PROOF.md。
术语：attempt = 一次绑定 `ProviderRunIdentity` 的物理 provider run；detector =
`LoopDetector`（token + 指数衰减 weighted-distinct 的纯检测器）；sensor = `LoopSensor`（transport
边沿观测器，持有 per-session detector 与进程内 `LoopKillArmed` 集合）。

## DG-001：问题与非目标

必须检测：正在流式输出中的低多样性循环（单 token 循环、短短语循环、固定代码/句子反复）。这类
退化若不被截断，会浪费资源、污染 transcript 尾部、推迟有效恢复槽。

非目标（本包不做）：
- 不估算上下文窗口或 remaining-token budget；tokenizer 只定义 detector 的观测单位
- 不按错误文字分类 provider 失败（CTX-005）
- 不发明独立的自动恢复预算（复用 `provider-attempt-recovery` 的 FALLBACK-005）
- 不把 part.delta 内容拼装成领域事实（ARCH-002）
- 不按角色/模型/自然语言 vs 代码动态改阈值（统一固定参数，DG-004）
- 不做「同一意思换说法」的语义理解式检测

## DG-002：传感器是 ARCH-002 的定点例外

`LoopSensor` 只做一件事：从 Host 流式事件中提取 `field=text` 的新增文本，喂给 detector；非
text delta 事件是 no-op。不变量：
1. 传感器不得写 Journal 业务事实（除 LOOP-006 强杀路径副作用）；
2. 传感器不得从 delta 推断 terminal / completion / tool 结果 / Authority；
3. 传感器不得把原始 payload 交给 Reconciler 或 FallbackController；
4. 业务层永远看不到 part.delta，只看到「某次 attempt 被强杀后的 reconcile 结局」。

## DG-003：判定指标

观测单位由 Node.js `gpt-tokenizer` 的 `o200k_base` 定义。每次 text delta 先编码成 token id；不再
按 UTF-16 字符、4-gram、空白过滤、HHI 或 inverse-Simpson 判定。

对 token 流维护指数衰减 weighted distinct count。第 `t` 个 token 为 `x`，若 `x` 上次出现于
`p`，则：

```text
D_t = λ·D_(t-1) + 1 - λ^(t-p)   （x 曾出现）
D_t = λ·D_(t-1) + 1             （x 首次出现）
```

含义：每个不同 token 的当前贡献等于其最近一次出现距今的指数权重；再次出现只把自己的贡献重置到
1，不会像频次平方那样让大量 Markdown `|`、表格分隔符、ASCII 连线压倒其余多样 token。
`D_t <= LOOP_WEIGHTED_DISTINCT_THRESHOLD` → LOOP，否则 NORMAL。无连续命中、无迟滞：单次越阈即
LOOP。

## DG-004：固定参数与仓库滴定

参数不得按模型、角色、上下文长度、自然语言 vs 代码动态改写：

```text
TOKENIZER = gpt-tokenizer/o200k_base
HALF_LIFE = 64 token
LAMBDA = 2^(-1/64)
NORMAL_WEIGHTED_DISTINCT = 18.385164504723882
THEORETICAL_LOOP_WEIGHTED_DISTINCT = 1
LOOP_WEIGHTED_DISTINCT_THRESHOLD = (18.385164504723882 + 1) / 2
                                 = 9.692582252361941
```

滴定语料 = 仓库中 Git tracked + 非 ignored 且 strict UTF-8 可解码的全部文字。half-life 由这些文字
所有非空行的 o200k token 长度 p99 向上取二次幂：当前 p99=57 → 64。正常值不是均值，而是把全部
可读文字按确定顺序连接后，从理论最大 distinct prior `1/(1-λ)` 开始扫描时出现的最低 `D_t`；异常
值不采样垃圾语料，直接取「所有观测都是同一个 token」的理论极限 1。阈值严格取二者中线。滴定由
`tests/loop-calibration.test.mjs` 永久重放。上述“当前”统计值只是仓库语料的派生基线，不是人工冻结
的产品语义；仓库可读文本变化时 feel free to modify，重新运行 calibration 并同步 production/WHAT/HOW/PROOF。

fresh detector 以 `NORMAL_WEIGHTED_DISTINCT` 为无罪 prior；真实 token 按同一 λ 自动淡出该 prior。

## DG-005：O(1) 更新与有界内存

每 token 只查/改该 token 的最近出现 step，时间 O(1)。状态只保存 tokenizer 有限 vocabulary 中已见
token 的 `token_id -> last_step`，因此内存上界由固定 vocabulary 决定，不随重复流长度增长；禁止改回
保存原始输出、无限 n-gram 或 transcript 历史的实现。

## DG-006：detector 生命周期绑定单次 ProviderRun

每个 provider attempt 一个全新 detector（带正常 prior）；严格绑定到该 attempt 的
`ProviderRunIdentity`；强杀、turn 结束、session 删除 → 丢弃，禁止跨 attempt 复用或泄漏引用。
`LoopSensor.ResetDetector` 在每次 attempt 边界（SessionIdle）丢弃旧 detector 状态。

## DG-007：命中只停止当前物理 attempt

detector 判定 LOOP → 若该 session 当前 attempt 尚未武装 LoopKill：记录 `LoopKillArmed` →
`AbortSession(sessionId)`（物理强杀请求）；若已武装：忽略后续 delta（幂等，不二次 abort）。禁止
在 abort 返回前发送 continuation；禁止根据 delta 内容裁剪 transcript 或改写已发出的客户端可见文本。

## DG-008：LoopKillArmed 是进程内局部事实

`LoopKillArmed` 不写 Journal，崩溃/重启后自然丢失（安全侧 Fail-Closed，允许重复输出）。它不是
恢复协议状态（HOST-007 精神）；同一 attempt 的重复 delta 幂等跳过。

## DG-009：强杀桥接标准 recovery，不造第二状态机

Host 对插件 abort 通常落成 `MessageAbortedError` / `finish=aborted`，reconcile 为
`TurnAborted`。`TurnAborted` 本身不推进 Fallback（用户中止与清理中止不得自动 AABB）。但当
`LoopKillArmed` 命中该 session：清除标记 → 走与 provider failure 等价的路径——
`FallbackController.recordConfirmedFailure`（`provider-attempt-recovery` 的唯一写入口）→
mayContinue 则发 `ProviderRetryAttempt` continuation，否则 `FallbackExhausted` 终局。桥接由
`ProviderRecoveryWorkflow.continueAfterLoopKill` 实现。

## DG-010：作用域与豁免

必须检测：插件 Owned 的 managed WorkSession / CompanionSession / BloggerSession 中正在进行的
assistant 文本流（field=text）。必须忽略：非 Owned session、reasoning 字段 delta、compaction
pseudo-run（HOST-006）、title / 非 managed 的 Host 内部 run、已 LoopKillArmed 的同一 attempt 的
后续 delta。

## DG-011：continuation 是独立叶子

LoopKill 后的 continuation 用固定 instruction-only Synthetic TOML，语义叶子
`runtime/loop-continue`（PROMPT-019），ContinuationKind = `ProviderRetryAttempt`，不新增 Origin
种类。普通 provider failure 的 continuation 是另一片叶子（`runtime/provider-retry`），不得混用。

## DG-012：detector 不是业务 truth / retry controller

LOOP 只负责更早地把退化 attempt 变成一次可恢复的失败。不绕过 FallbackController、不直接改
Offset、不直接发 prefix probe 或 squash、不根据 delta 内容改写已发文本。诊断字段只允许
session_id / operation / weighted_distinct_token_count / detector_step / result / duration /
provider_error；禁止把完整循环正文写入日志；禁止用 detector 分数驱动 Fallback 之外的业务分支。
