# degeneration-guard — 唯一 normative 合同

条款 ID 前缀：`DG-`。本文每个命题都是**当前世界必须同时成立的事实**；测试落点见 PROOF.md。
术语：attempt = 一次绑定 `ProviderRunIdentity` 的物理 provider run；detector =
`LoopDetector`（4-gram + 指数核、固定内存的纯检测器）；sensor = `LoopSensor`（transport 边沿的
观测器，持有 per-session detector 与进程内 `LoopKillArmed` 集合）。

## DG-001：问题与非目标

必须检测：正在流式输出中的低多样性循环（单字符循环、短短语循环、整句 20~30 字符反复）。这类
退化若不被截断，会浪费资源、污染 transcript 尾部、推迟有效恢复槽。

非目标（本包**不**做）：
- 不估算 token / 上下文窗口（CTX-001）
- 不按错误文字分类 provider 失败（CTX-005）
- 不发明独立的自动恢复预算（复用 `provider-attempt-recovery` 的 FALLBACK-005）
- 不把 part.delta 内容拼装成领域事实（ARCH-002）
- 不按角色/模型/自然语言 vs 代码动态改阈值（统一按代码，DG-004）
- 不做「同一意思换说法」的语义理解式检测

## DG-002：传感器是 ARCH-002 的定点例外

`LoopSensor` 只做一件事：从 Host 流式事件中提取 `field=text` 的新增字符，喂给 detector；非
text delta 事件是 no-op。不变量：
1. 传感器不得写 Journal 业务事实（除 LOOP-006 强杀路径副作用）；
2. 传感器不得从 delta 推断 terminal / completion / tool 结果 / Authority；
3. 传感器不得把原始 payload 交给 Reconciler 或 FallbackController；
4. 业务层永远看不到 part.delta，只看到「某次 attempt 被强杀后的 reconcile 结局」。

## DG-003：判定指标

观测单位：丢弃空白与减号（`{' ', '\t', '\r', '\n', '-'}`）后，在过滤后的字符流上生成重叠
4-gram；其它标点与字符原样进入，不做 NFKC / 大小写 / 数字折叠。被忽略字符既不形成 4-gram，也
不推进指数衰减。

对已观察 4-gram 流用固定慢指数核混合逼近 Zipf 型无限历史，物理量是 **N_eff**（inverse
Simpson / Hill number of order 2）=「相当于多少个不同 4-gram」。阈值在 N_eff 空间取中点：
`N_eff <= LOOP_EFFECTIVE_COUNT` → LOOP，否则 NORMAL。无连续命中、无迟滞：单次越阈即 LOOP。

## DG-004：固定参数（KISS）

参数是规范常量，不得按模型、角色、上下文长度、自然语言 vs 代码动态改写：

```text
NGRAM_SIZE = 4；HASH_BUCKETS = 4096；K = 3
HALF_LIFE = [8, 64, 512]；LAMBDA = [2^(-1/8), 2^(-1/64), 2^(-1/512)]
COEF = [0.15, 0.25, 0.60]
NORMAL_EFFECTIVE_COUNT = 256（过滤空白后的正常代码先验）
GARBAGE_EFFECTIVE_COUNT = 24（典型垃圾循环基准）
LOOP_EFFECTIVE_COUNT = (256 + 24) / 2 = 140；LOOP_HHI = 1/140
```

正常代码先验（无罪推定）：创建时注入虚拟正常历史（HHI=1/256，N_eff=256），真实输出按核淡出；
过滤后不足 4 个有效字符时保持先验。不需要 `MIN_NGRAMS` 预热窗。

## DG-005：O(1) 递推与固定内存

每有效字符 O(K²)=O(1)，被忽略字符 O(1) 跳过；内存 O(HASH_BUCKETS·K)，不随流长度增长。固定哈希
桶（4096），哈希碰撞使 HHI 略偏高（更敏感）——禁止为「更准」改回无限 Map。

## DG-006：detector 生命周期绑定单次 ProviderRun

每个 provider attempt 一个全新 detector（带代码先验）；严格绑定到该 attempt 的
`ProviderRunIdentity`；强杀、turn 结束、session 删除 → 丢弃，禁止跨 attempt 复用或泄漏引用。
`LoopSensor.ResetDetector` 在每次 attempt 边界（SessionIdle）丢弃旧 detector 状态。

## DG-007：命中只停止当前物理 attempt

detector 判定 LOOP → 若该 session 当前 attempt 尚未武装 LoopKill：记录 `LoopKillArmed` →
`AbortSession(sessionId)`（物理强杀请求）；若已武装：忽略后续 delta（幂等，不二次 abort）。禁止
在 abort 返回前发送 continuation；禁止根据 delta 内容裁剪 transcript 或改写已发出的客户端可见
文本。

## DG-008：LoopKillArmed 是进程内局部事实

`LoopKillArmed` 不写 Journal，崩溃/重启后自然丢失（安全侧 Fail-Closed，允许重复输出）。它不是
恢复协议状态（HOST-007 精神）；同一 attempt 的重复 delta 幂等跳过。

## DG-009：强杀桥接标准 recovery，不造第二状态机

Host 对插件 abort 通常落成 `MessageAbortedError` / `finish=aborted`，reconcile 为
`TurnAborted`。`TurnAborted` 本身不推进 Fallback（用户中止与清理中止不得自动 AABB）。但**当
LoopKillArmed 命中该 session**：清除标记 → 走与 provider failure 等价的路径——
`FallbackController.recordConfirmedFailure`（`provider-attempt-recovery` 的唯一写入口）→
mayContinue 则发 `ProviderRetryAttempt` continuation，否则 `FallbackExhausted` 终局。桥接由
`ProviderRecoveryWorkflow.continueAfterLoopKill` 实现。

## DG-010：作用域与豁免

必须检测：插件 Owned 的 managed WorkSession / CompanionSession / BloggerSession 中正在进行
的 assistant 文本流（field=text）。必须忽略：非 Owned session、reasoning 字段 delta、compaction
pseudo-run（HOST-006）、title / 非 managed 的 Host 内部 run、已 LoopKillArmed 的同一 attempt
的后续 delta。

## DG-011：continuation 是独立叶子

LoopKill 后的 continuation 用固定 instruction-only Synthetic TOML，语义叶子
`runtime/loop-continue`（PROMPT-019），ContinuationKind = `ProviderRetryAttempt`，不新增 Origin
种类。普通 provider failure 的 continuation 是另一片叶子（`runtime/provider-retry`），不得混用。

## DG-012：detector 不是业务 truth / retry controller

LOOP 只负责更早地把退化 attempt 变成一次可恢复的失败。不绕过 FallbackController、不直接改
Offset、不直接发 prefix probe 或 squash、不根据 delta 内容改写已发文本。诊断字段只允许
session_id / operation / effective_character_count / detector_step / result / duration /
provider_error；禁止把完整循环正文写入日志；禁止用 HHI / N_eff 驱动 Fallback 之外的业务分支。
