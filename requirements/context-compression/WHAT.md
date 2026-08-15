# context-compression — WHAT（唯一 normative 合同）

条款前缀：`CONTEXT-COMPRESSION-`。证据指针 → `PROOF.md` 对应行号。

---

## CONTEXT-COMPRESSION-001：不观察容量

**规范**：不得读取、查询、推导、缓存任何模型上下文窗口大小（CTX-001）。禁止
`contextWindow` / `remainingTokens` / `headroom` / `nearLimit` / `shouldCompact` /
`ensureCapacity` 等一切同义词概念。禁止 tokenizer、模型窗口表、字节→token 换算。
管理员配置与 provider 元数据不得改变本条。唯一允许的字节计量：CTX-003 输入合同与
文件/进程类既有合法计数（EXEC-011）。

**含义/动机**：估计容量把错误阈值固化成产品行为，并与 KV-cache / 前缀稳定性冲突。
历史 why/context 条款明确拒绝预测式压缩。

**边界**：「文件/进程既有合法计数」的字节计量归 `process-execution`；本命题拥有
「压缩路径不得观察窗口」。

**证据**：CTX-001；历史 why/context 条款；`Domain/BloggerDelta.fs`（只含 200 KiB 常数）。

---

## CONTEXT-COMPRESSION-002：不主动预测溢出

**规范**：请求前不得判断「是否接近上限」；禁止按投影长度比例、剩余输出预算、Y 字节阈值、
累计 token、型号选压缩点（CTX-002）。**真实失败是唯一恢复触发信号**；第一次溢出表现为
失败——主动接受的代价。HOST-006 重锚读的是「已发生的 compaction 事实」，不是「还剩多少空间」。

**含义/动机**：正常请求总是先直接执行。只有真实 provider attempt 失败之后的恢复槽才允许
尝试前缀替换或 frame 压缩（CTX-006 合取）。

**边界**：armed/primed 状态由 fallback 提供（FALLBACK-012，归 provider-attempt-recovery）；
本命题拥有「没有失败就没有压缩」。

**证据**：CTX-002；`Domain/RecoverySlot.fs`（`beginSequence = NotArmed`、`mayRecover` 三合取）。

---

## CONTEXT-COMPRESSION-003：200 KiB 输入合同

**规范**：支持的 LLM 在扣除固定 system/tools/封装后，至少能接收 **200 KiB** provider-visible
动态输入（CTX-003）。`BloggerDeltaLimitBytes = 200 * 1024` 是**输入合同**，不是窗口估算：
不与窗口比、不算比例、不触发主动 squash。计量点：TOML 渲染后的 UTF-8 字节。

**含义/动机**：合同只约束单次 delta 渲染字节；超限确定性切块/截断策略保持可复现。

**边界**：TOML 渲染布局归 provider-projection；「200 KiB 合同」本身是本命题。

**证据**：CTX-003；`tests/blogger-delta.test.mjs`（`CTX_003_delta_limit_is_200_KiB`、
`CTX_003_no_chunk_exceeds_the_limit`）。

---

## CONTEXT-COMPRESSION-004：输出预算属 provider

**规范**：插件不计算 squash 应占多少 token、不检查压缩比（CTX-004）。唯一内容校验：
`isValidTerminal = 非空 ∧ 非 XML-only`（与 FALLBACK-008 对齐）。

**含义/动机**：压缩点由失败堆栈决定，不看 token/配额。

**边界**：XML-only 判定的具体实现归 `terminal-validity` 类型；本命题拥有「不按比例/预算决策」。

**证据**：CTX-004；`tests/terminal-validity.test.mjs` 全部。

---

## CONTEXT-COMPRESSION-005：失败不分类

**规范**：控制流只看 snapshot `Outcome`：`Completed | Failed | Aborted`（CTX-005）。
不得按错误文字/类型名区分溢出、网络、限流等。「溢出」只许出现在诊断，不得进 Journal 字段
或 probe/squash 判定。compaction 来源同样不分类。

**含义/动机**：按错误文字分叉在换 provider 时整体失效，并制造永不执行分支。
`AttemptOutcome` 无 `Overflow` case 就是本条的结构表达。

**边界**：`TerminalValidity` 的 `CompletedInvalid` 分支归 FALLBACK-008 的 repair 语义
（provider-attempt-recovery）；本命题拥有「Failed/Aborted 同路径」。

**证据**：CTX-005；`Domain/RecoverySlot.fs`（`AttemptOutcome` 注释）；
`tests/recovery-slot.test.mjs`（`CTX_005_Failed_and_Aborted_take_the_identical_path`）。

---

## CONTEXT-COMPRESSION-006：恢复槽 = armed ∧ primed ∧ hasMaterial

**规范**：合取三者才允许恢复动作（CTX-006）：

```text
armed ∧ primed ∧ hasMaterial
```

| Session | 动作 | 额外 LLM |
|---|---|---|
| X | prefix probe（不先永久提交） | 无 |
| Y | frame squash（有效则提交） | 一次 |

无材料时发正常主请求——正常状态，不是错误。恢复槽是机会，不是「必然压缩」。

**含义/动机**：没有失败、没有 prime 位置、没有材料，任一缺失都回到普通请求。
`Domain/RecoverySlot.mayRecover` 的结构即本命题。

**边界**：armed/primed 来自 FALLBACK-012（provider-attempt-recovery）；本命题拥有
hasMaterial 与动作选择。

**证据**：CTX-006；`Domain/RecoverySlot.fs`；
`tests/recovery-slot.test.mjs`（`CTX_006_*`）。

---

## CONTEXT-COMPRESSION-007：按 RequestKind 分派结局

**规范**：每个 attempt 的三种结局（Outcome + isValidTerminal）按 `ProviderRequestKind`
分派固定后继：WorkMain / BloggerMain / BloggerSquash / InteractionRepair / StrengthReplica
各有固定动作。**同种 RequestKind 同种结局必须同一分派**，禁止按错误字符串分叉（CTX-007）。
恢复槽内失败仍走 Fallback 连续失败计数；维护子请求成功不得单独清零 count（CTX-008；
FALLBACK-011）；不得为「压缩失败」另造第二套预算。

**含义/动机**：分派是穷尽的、deterministic 的；错误文本永不参与控制流。

**边界**：RequestKind 类型本身与 mayCarryProbe 归 provider-attempt-recovery / dispatch；
本命题拥有「结局→动作映射」这一半。

**证据**：CTX-007/008；`Domain/RecoverySlot.fs`（`onSquashOutcome`/`onMainOutcome`/
`advancesCursor`）；`tests/recovery-slot.test.mjs`（`CTX_007_*`、`CTX_008_*`、
`PROMPT_008_every_request_kind_has_a_distinct_diagnostic_label`）。

---

## CONTEXT-COMPRESSION-008：X 不发压缩请求

**规范**：Work Session 从不向主模型发送「请压缩历史」类请求（CTX-009）。压缩只发生在
Y 的 squash 或 X 的 prefix 替换投影。只有 `WorkMain` 可携带 prefix probe
（`ProviderRequestKind.mayCarryProbe`）。

**含义/动机**：压缩是系统侧投影能力，不是交给模型做的写作任务。

**边界**：投影构造归 provider-projection / prefix-stability；本命题拥有「谁被允许请求压缩」。

**证据**：CTX-009；`Domain/PrefixCandidate.fs`（`mayCarryProbe`）；
`tests/recovery-slot.test.mjs`（`CTX_010_only_the_work_main_request_may_carry_a_prefix_probe`）。

---

## CONTEXT-COMPRESSION-009：候选未提交不是事实

**规范**：恢复槽中替换 X 前缀时**不**立即改 ActivePrefixEpoch；候选只进不可变
`AttemptExecutionProfile.ProjectionChoice`（CTX-010）。probe 失败 → 丢弃候选、无任何事实；
无 `PrefixProbeRolledBack` 类事实。`A′` 失败不禁止 `B′` 用等价候选重试。

**含义/动机**：失败不写事实 = 无需回滚神话；候选从未成为世界的一部分。
（与 semantic-trace 的「未发生材料永不写成历史」同构。）

**边界**：epoch 提升（成功后的 `PrefixRebaseCommitted`）归 prefix-stability。

**证据**：CTX-010；`Domain/PrefixCandidate.fs`（`PrefixProbe`/`XProjectionChoice` 注释）；
`tests/prefix-epoch.test.mjs`（prefix-stability 包）`CTX_010_a_failed_probe_leaves_no_trace_to_undo`。

---

## CONTEXT-COMPRESSION-010：候选选择严格新于已提交 epoch

**规范**：候选必须严格新于已提交 epoch 的 coverage 证明（CTX-011）：cutoff 不得回退；
候选与已提交不可区分时拒绝（不烧 epoch）；无候选 → 不构造空 probe，走正常主请求；
CoverableTurnCutoff 只前进；失配 `CoveredPrefixDigest` → fail closed（COMPANION-011）。

**含义/动机**：同 cutoff 更紧的 frozen digest 是新候选；identical candidate 是「烧一个
冷边界换零变化」。

**边界**：digest 的计算（hash 哪些字节）归 provider-projection / prefix-stability 的
cutoff 证明；本命题拥有「什么算新候选」的判定。

**证据**：CTX-011；`Domain/PrefixProbeSelection.fs`；`tests/probe-selection.test.mjs` 全部。

---

## CONTEXT-COMPRESSION-011：提交语义分型

**规范**（CTX-012）：

| 动作 | 成功 | 失败 |
|---|---|---|
| X probe | 提交 epoch（EvidenceKind=Probe）+ SealRoot 继承 | 无事实 |
| Y squash | `BlogSquashCommitted`，FrameEpoch+1 | 不改 frames/coverage |

squash 选择范围/级联：前半有效 frames；不混父 LWR（COMPANION-006）。squash 成功后
被永久保留；同一 armed slot 内 squash 与 main 是两次物理请求但至多一次
`FallbackCursorAdvanced`（FALLBACK-011）。

**含义/动机**：X probe 失败无事实（CONTEXT-COMPRESSION-009）；Y squash 失败只是
跳过——frames 还在，下次失败槽再试。

**边界**：epoch 事实（`PrefixRebaseCommitted`）的 fold 归 prefix-stability / durable-events。

**证据**：CTX-012；`Domain/RecoverySlot.fs`（`onSquashOutcome`）；`tests/blog-projection.test.mjs`
（`CTX_012_*`）；`tests/prefix-epoch.test.mjs`（prefix-stability 包）。

---

## CONTEXT-COMPRESSION-012：Blogger delta TOML 合同

**规范**（CTX-013）：data-only TOML 冻结进 blob；instruction header 投影时加；硬上限
200 KiB 渲染后字节，超限确定性切块/截断保持可复现；含 decision-relevant host-visible
reasoning、无 hidden reasoning 伪造；与 LWR gap 分投影，禁止混用 renderer 输出当
canonical digest。

**含义/动机**：delta 是 Y 的压缩输入；data-only 冻结保证 frame 正文可重放、可 digest。
instruction header 是渲染时注入，不是 frame 内容。

**边界**：TOML 布局/转义渲染归 provider-projection；本命题拥有 delta 合同。

**证据**：CTX-013；`Domain/BloggerDelta.fs`；`tests/blogger-delta.test.mjs`（`CTX_013_*`）；
`tests/companion-projection.test.mjs`（`COMPANION_007_canonical_digest_uses_semantic_projection_not_toml`）。

---

## CONTEXT-COMPRESSION-013：诊断不是控制输入

**规范**（CTX-014）：可观测诊断不得变成控制输入：不得用日志字段驱动 Fallback / probe /
squash 分支。

**含义/动机**：`Diagnostic.emit` 只接受白名单字段；任何 `context_ratio` 式字段
（把窗口估算写成日志）都会被 tombstone 测试拦下。白名单可以包含纯观察性的稳定 identity
provenance（例如 DryRun 可见 child 的 `replica_session_id`），前提是该字段只解释“观察的是谁”，
绝不参与 Fallback / probe / squash / promotion 等控制决定。

**边界**：诊断模块共享使用（LOOP-010 等）不影响 owner；本命题拥有「诊断不得回流控制流」。

**证据**：CTX-014；`tests/ctx014.test.mjs` 全部。

---

## CONTEXT-COMPRESSION-014：squash 只处理本 X 的 frames

**规范**：squash 只处理本 X frames，不混父 context（COMPANION-006）。父 LWR 是 child 的
输入 context，不是 child 的 frame。

**含义/动机**：混父材料会把另一段 work 的历史压进本段。

**证据**：COMPANION-006；`Context/Companion/Blogger/Projection.fs`；`tests/blog-projection.test.mjs`
（`COMPANION_006_squash_rewrites_first_half_of_frames_permanently`）。

---

## CONTEXT-COMPRESSION-015：busy/失败不推进 coverage

**规范**：Blogger busy：不打断、不排队、**不推进** RecordCoverage；失败/空/XML-only 不推进。
仅 `BlogEntryCommitted` 原子推进 frame 可见性与 RecordCoverage（COMPANION-008 / PERSIST-010）。

**含义/动机**：coverage 是「Y 真实消化到哪」；没消化就没有新覆盖。原子推进保证
frame 与 coverage 永不半套。

**边界**：coverage 是 XTrace 游标的事实归 semantic-trace；本命题拥有推进条件。

**证据**：COMPANION-008；`Context/Companion/Blogger/Projection.fs`；
`tests/blog-projection.test.mjs`（`COMPANION_008_entry_appends_frame_and_advances_coverage_together`、
`CTX_011_entry_that_consumed_nothing_is_refused`）。

---

## CONTEXT-COMPRESSION-016：Y prefix 只物化 PrefixCoverage 完整 turn

**规范**：Y prefix materialize 只许用 PrefixCoverage-complete-turn 的 proven Y，禁 RawGap
（CTX-015）。`CoverableTurnCutoffExclusive` 只在完整 Host turn 边界推进；`CoverableFrameCount`
约束 probe 只能用 cutoff 之前覆盖的 frames（COMPANION-011）。

**含义/动机**：probe 用了 cutoff 之外的 frames，模型会看到同一 turn 两次
（一次摘要一次 verbatim）——不完整证据冒充完整证明。

**边界**：cutoff digest 的字节计算归 prefix-stability / provider-projection。

**证据**：CTX-015；`Context/Companion/Blogger/Projection.fs`（`BlogCoverage` 双字段）；
`tests/probe-selection.test.mjs`（`CTX_011_*`、`COMPANION_011_*`）；
`tests/blogger-delta.test.mjs`（`CTX_011_*` cutoff 只前进）。

---

## CONTEXT-COMPRESSION-017：Opening floor（WorkRecordStart）

**规范**：Opening 永久 raw：不交给 Y 改写、不随 rebase 消失、survives compaction /
reanchor / recovery（COMPANION-014 / CTX-016）。Blogger effectiveStart =
`max(RecordCoverage, Life.WorkRecordStart)`；WorkRecordStart 由 LifeOpened / XTrace
Opening cursor 纯推导，不是 Stage；不绑回 `WorkActivated`（TODO-001）。

**含义/动机**：旅程可缩短，章程不可缩短。Opening floor 是结构性 cursor，不是状态机判断。

**边界**：floor 的推导类型（`ManagerOpeningFloor`）语义归 work-record（WORK-RECORD-015）；
本命题拥有「Y 不得吞 Opening」这一半。

**证据**：CTX-016；TODO-001；`Mission/Manager/Life/OpeningFloor.fs`；`Domain/MagicTodo.fs`
（`bloggerEffectiveStart`）；`tests/ctx-opening-floor.test.mjs`（NEW）全部。

---

## CONTEXT-COMPRESSION-018：Blogger catch-up 连续追平；禁止 frozen drain frontier；quiet 在同一存活执行内必须 park 等未来 material

**规范**：一次 main-material wake 可驱动多个 ≤200 KiB Blogger cycle，直到当前 canonical Current
暂时无可消费 material。每个已提交 cycle 后，下一块必须由**当时最新**的 Blog coverage 与 XTrace
Current 重新派生；禁止冻结、缓存或持久化 wake-time / cycle-time XTrace head 作为 drain frontier、
upper bound、target sequence 或等价截止线。drain 期间新到且满足 coverage 规则的 material 必须被
同一连续 catch-up 消费，不得人为推迟到下一次 main-material wake。

当前暂时无 material（caught-up / quiet）**不是完成条件**。在当前物理执行仍存活、main 未被 durable
seal 或其它已有合法终止语义关闭时，Blogger continuation 必须先进入 parked wait；material 到达后恢复
同一 catch-up，并再次从最新 Current 派生下一块。禁止以 caught-up、quiet、当前 XTrace head 已覆盖为由
直接结束该连续 drain。park waiter 自身既有的 cancel / physical lifetime 解除语义保持不变；它们是
物理等待边界，不是“caught-up 已完成”的业务证据。**进程死亡不属于这里的“等待未来”**：按
CRASH-017/018，旧 tool/continuation 已中断，新 Host 不得自动恢复、重放或补 terminal；显式 `/continue`
也只能公开断点并重新登记 surviving child，不能续跑旧 Blogger tool invocation。

**含义/动机**：200 KiB 只限制单次输入，不限制一次 wake 的总追平量。冻结 frontier 会降低吞吐并
改变 material 可见时序；把 quiet 当完成则把持续 Companion 变成依赖下一次用户/主会话动作的离散批处理。
`ParkTransform`/pending offer 只承载物理等待与唤醒，不得成为业务 Stage/程序计数器；业务流程仍由
F# CE 的等待与继续直接表达（STRUCTURED-WORKFLOW-001/002/003）。

**边界**：
- canonical Current 的积分只归 DURABLE-EVENTS-019 Integrator；本条禁止业务路径自行扫描/重放 Journal。
- seal/cancel 的终止资格归各自 owner；Host restart / 显式续传归 `crash-reconciliation` CRASH-017/018。
  本条不创建跨进程 recovery owner，也不允许 restart 自动续跑旧 Blogger continuation。
- waiting 的因果诊断归 `causal-wait`；诊断 observation 不得反向决定是否继续 drain。

**证据**：`tests/enforcer-cycle-commit-convergence.test.mjs`
`ENFORCER_caught_up_park_absorbs_future_material_beyond_previous_head_without_frozen_frontier`；
`tests/blogger-convergence-gaps.test.mjs`
`C0_caught_up_is_parked_not_completed_and_wake_rechecks_live_Current`；跨包 restart 边界 REUSE
`requirements/crash-reconciliation/tests/explicit-continue.test.mjs` 的 CRASH-017/018；PROOF.md 第 018 行。
