# prefix-stability — WHAT（唯一 normative 合同）

条款前缀：`PREFIX-STABILITY-`。证据指针 → `PROOF.md` 对应行号。

---

## PREFIX-STABILITY-001：同 epoch append-only prefix law

**规范**（ARCH-004 / HOST-013 / cache.md PREFIX LAW）：同一 PrefixEpoch 内，
`ProviderWire(n)` 是 `ProviderWire(n+1)` 的**精确字节前缀**。权威判定只有
`ProviderProjection.isAppendOnlyPrefix`（比较 provider/model/variant/tools/system 及完整
message prefix）；**不得**再写第二套「差不多是前缀」的 helper。tools 必须相同、不得只是前缀
（tool 集合变化整体作废 KV cache）。

**含义/动机**：前次请求已发送的字节必须保持，Prefix Cache 才成立；「几乎一样」的断言
（pair 数量、callID 相同、markerText 正确）在历史已被搬家的实现上全部通过——不具判别力。

**边界**：why/when 需要替换前缀归 context-compression；本命题拥有「替换后的字节关系」。

**证据**：ARCH-004；HOST-013 行为约束 5；`Domain/ProviderProjection.fs`
（`isAppendOnlyPrefix`）；历史 change（cache）§1/§2/§11。

---

## PREFIX-STABILITY-002：冷边界只有三证据源

**规范**（COMPANION-009 / shape/companion.md）：同一 PrefixEpoch 内前缀逐字节稳定；epoch
切换**仅**下列证据源，且必须 `EpochId+=1`（单一 ActivePrefixEpoch 合同）：

1. 成功 prefix probe 提升（CTX-010/012）→ `PrefixRebaseCommitted`，`EvidenceKind=Probe`；
2. Host compaction 重锚（HOST-006）→ `ContextReanchored`，`Snapshot=None`；
3. TodoCheckpoint lag-1 rebase（TODO-009 / CTX-015）→ 同一 `PrefixRebaseCommitted` 合同，
   `EvidenceKind=TodoCheckpoint`。

禁止按容量/token 主动切换 epoch；`session.compacted` 不得冒充 TodoCheckpoint。

**含义/动机**：冷边界 = 已提交事实；任何其它来源（估算、状态机）都会让「何时换世界」不可审计。

**边界**：每个来源的触发条件分别归 context-compression（probe/candidate）、host-boundary
（compaction 观察）、obligation-ledger（Accepted 链）；本命题拥有「切换只由这三源发生」。

**证据**：COMPANION-009；`shape/companion.md`（epoch 切换表）；历史 why/context 条款。

---

## PREFIX-STABILITY-003：candidate ≠ committed

**规范**（CTX-010 / PROMPT-008）：未提交候选只进不可变 `AttemptExecutionProfile.ProjectionChoice`
（`UseCommittedEpoch | UsePrefixProbe of PrefixProbe`），**不**改 ActivePrefixEpoch。
probe 失败 → 丢弃候选、无任何事实、无 `PrefixProbeRolledBack`。`A′` 失败不禁止 `B′` 用等价
候选重试。probe 候选的 blob ≠ committed blob（`XPrefixProjection.requiredBlob` 按 choice 取）。

**含义/动机**：未提交候选从未成为世界的一部分；把它当 committed 会让一次失败的尝试
改写已呈现前缀。

**边界**：「什么算新候选」的判定归 context-compression（CTX-011）；本命题拥有
「候选与 committed 的分离」。

**证据**：CTX-010；`Domain/PrefixCandidate.fs`；`Domain/XPrefixProjection.fs`；
`tests/prefix-epoch.test.mjs`（`CTX_010_a_failed_probe_leaves_no_trace_to_undo`）。

---

## PREFIX-STABILITY-004：ActivePrefixEpoch 是唯一 epoch SSOT

**规范**（CTX-015 / TODO-009/012）：既有 `ActivePrefixEpoch` / `PrefixRebaseCommitted`
是**唯一** prefix epoch SSOT。Magic Todo lag-1 rebase **必须**进入该合同：不得平行
todo-only epoch、不得 `NeedRebase`/`RebaseRequested` Stage、不得缺字段旁路。
`EvidenceKind = Probe | TodoCheckpoint`；TodoCheckpoint commit 至少含 `EpochId`、
`PrefixSnapshot`、`Cutoff`、`SealRoot`、`YBundleRef`/`YBundleDigest`、
`ProviderPrefixDigest`，以及 `TriggerTodoWriteId`/`CoveredBeforeTodoWriteId`（option）。

**含义/动机**：第二真相源会让崩溃恢复与 seal 绑定分叉——todo 说 rebase 了而 epoch 没变。

**边界**：commit 的时机（seal 前）归 CTX-015 流程；本命题拥有「同一合同、同一字段集」。

**证据**：CTX-015；`Domain/MagicTodoPrefixEpoch.fs`（`buildTodoCheckpointCommit` 与 probe
共用 `PrefixRebaseCommittedV2` 形状）；`tests/prefix-epoch-todo-checkpoint.test.mjs`（NEW）。

---

## PREFIX-STABILITY-005：已 seal epoch 不因 provider 成败回滚

**规范**（CTX-015 / TODO-009）：`PrefixRebaseCommitted` 在下一真实 provider attempt
seal/绑定**之前**原子提交；provider Failed/Aborted **不**回滚已 seal epoch。崩溃后 boot fold
按 Accepted 链重算 desired，下次 seal 前再 commit。probe 路径同：成功提升后才提交，
提交后 provider 结局不影响 epoch。

**含义/动机**：若 provider 失败能回滚 epoch，同一段历史会在两次 attempt 间「换世界」，
provider-visible prefix evidence / 前缀证明全部失真。

**边界**：desired cutoff 的推导归 obligation-ledger（仅从首次 accepted `planComplete=true` 起的
committed Accepted 子链；Pre-T1 planning checkpoints 不参与）；本命题拥有「commit 后不可逆」。

**证据**：CTX-015；TODO-009；`Context/Prefix/Epoch.fs`
（`applyRebase` 只校验 epoch 序，不读 provider 结局）。

---

## PREFIX-STABILITY-006：ContextReanchored 重锚语义

**规范**（HOST-006）：Host compaction 的唯一合法收容语义是 `ContextReanchored`：
`PrefixEpochId+1`、`Snapshot=None`、PrefixCoverage 归零。它**不是**恢复失败/容量信号；
不选择压缩内容；不替代失败驱动恢复。`ReanchoredRuns` durable 记录，同一 compaction
pseudo-run **永不**重锚两次（`CompactionAlreadyReanchored`）。重锚是 best effort，
不保证缓存连续 / busy skip 内容 / 摘要质量。

**含义/动机**：compaction 作废的是 Host 索引映射；epoch 照常前进（这是真实冷边界），
但同一观察不能重复消费。

**边界**：「观察到 compaction 就重锚」的决策归 context-compression（HOST-006 containment）；
本命题拥有 epoch 事实语义。

**证据**：HOST-006；`Context/Prefix/Epoch.fs`（`applyReanchor`/`isReanchored`）；
`tests/prefix-epoch.test.mjs`（`HOST_006_*`）。

---

## PREFIX-STABILITY-007：同 Life system prompt byte-identical

**规范**（PROMPT-014 / GLORY-075）：office system prompt 同一 Life 内 byte-identical；
禁止因下列事件改写字节或重绑 Persona：BlindPlan T1 / entrustment revelation、
Planning→Working（已删除）、Peer Fallback / Strength replica、process review / Finality、
Host compaction / reanchor / recovery。`SessionPersona` session 创建一次绑定不可变。

**含义/动机**：`The system prompt names the office. The conversation tells you which road is yours.`
T1 交托只走 conversation tool result；改 system 字节 = 废前缀缓存 + 制造第二份 Role Law。

**边界**：Persona 内容语义归 participant-identity；「字节稳定」是本命题（与
prefix identity 的交界）。

**证据**：PROMPT-014；GLORY-075；`requirements/prefix-stability/tests/system-prompt-stability.test.mjs`（REUSE）。

---

## PREFIX-STABILITY-008：FrozenRecordPrefix 是明确标记的 low-trust context

**规范**（COMPANION-010）：FrozenRecordPrefix 以明确标记的 context block 注入 X
（`CompanionPrompt.companionMemoryBlock`），不伪装人类/system 指令；同 epoch 内内容冻结。

**含义/动机**：low-trust 片段（frozen prefix、enforcer tip、historic frame）一旦伪装成指令，
模型会把「压缩摘要」当「系统命令」。

**边界**：「不伪装指令」的注入形状归 provider-projection；本命题拥有「冻结 + 标记」事实。

**证据**：COMPANION-010；`tests/companion-projection.test.mjs`（context-compression 包）
`COMPANION_010_memory_block_marks_the_body_as_low_trust_context`。

---

## PREFIX-STABILITY-009：cutoff 只在完整语义 turn 边界，digest 失配 fail closed

**规范**（COMPANION-011）：Cutoff 只在完整 semantic turn 边界；投影前重算
`CoveredPrefixDigest`，失配 fail closed。digest 失配**不是** compaction 善后手段
（善后是 HOST-006 重锚）。`CutoffExclusive` 与 `CoveredPrefixDigest` 同生共死：
snapshot 携带其一必须携带其二。

**含义/动机**：半 turn cutoff = 模型看到同一 turn 两次；digest 失配 = 证明与内容对不上。

**边界**：digest 的字节计算归 provider-projection；本命题拥有「边界条件 + 失配行为」。

**证据**：COMPANION-011；`Domain/PrefixCandidate.fs`（`PrefixSnapshot` 字段成对注释）；
`tests/probe-selection.test.mjs`（context-compression 包）`COMPANION_011_*`。

---

## PREFIX-STABILITY-010：历史 synthetic pair 原位 replay，anchor 缺失不重定位

**规范**（HOST-013 行为约束 1–3/6，cache.md）：pair 一经加入即不可变永久历史。普通 provider
每次 transform 必须按 durable gap anchor 原位置、原字节恢复全部既有 synthetic half，再把本次
pair 按其 gap anchor 渲染。**禁止**删除、过滤、去重、改写历史 pair；禁止复用既有 `callID`。
durable anchor 引用的真实消息缺失时，该 historical pair 不参与本次渲染（禁止重定位到
「最接近位置 / trailing user 前 / 末尾」）；durable fact 保留，完整 transcript 回来后按
anchor 再 replay。legacy 无 anchor journal → fail closed（incompatible），禁止启发式迁移。

**含义/动机**：历史字节只能由 durable append-only 事实恢复，不得由当前 transcript 形态
重新决定——否则每次 transform 都在重排已呈现的历史。

**边界**：occurrence 的 wire 形状（ordinary synthetic `skill({ name: "" })` + `<skill_content name="">`；
Cursor `NUL+BOM` + 同一 skill-content payload）归 provider-projection；本命题拥有「位置与字节的稳定性」。

**证据**：HOST-013；cache.md §4/§5；`requirements/prefix-stability/tests/pair-thought-anchored.test.mjs`
（REUSE：`H13_02_historical_pair_never_relocates_to_current_batch`、`H13_05_missing_anchor_pair_is_omitted_not_relocated`）。

---

## PREFIX-STABILITY-011：冷边界由事实驱动（HOST-013 前缀漂移不得用 epoch 掩盖）

**规范**（HOST-013 行为约束 5）：同一 epoch 内前次 provider-visible wire 必须是后次 wire 的
稳定字节前缀（`ProviderProjection.isAppendOnlyPrefix` 权威判定）。**禁止**用 PrefixEpoch 切换
掩盖 HOST-013 自己造成的前缀漂移；禁止读 limit、做 token 估算（CTX-002）。历史 marker 永不因
replay / compaction / reanchor 重算 elapsed——只重放已存字节（`SessionStartedAt` 只采样一次）。

**含义/动机**：epoch 切换是合法冷边界，不是修补 bug 的橡皮擦。

**边界**：elapsed 采样归 host-boundary（HOST-013 的 wall-clock 计量）；本命题拥有
「不得借 epoch 掩盖漂移」。

**证据**：HOST-013 行为约束 5/7；cache.md §10；历史 why/host 决策 13。

---

## PREFIX-STABILITY-012：reanchor/rebase 一旦提交不因后续 provider failure 回滚

**规范**：合法提交的重锚（`ContextReanchored`）与 rebase（`PrefixRebaseCommitted`，
含 TodoCheckpoint）不因后续 provider failure 回滚（CTX-015 合并本条语义；HOST-006 的
`ContextReanchored` 同理——一旦重锚，后续失败不把旧 snapshot 拉回来）。

**含义/动机**：已提交的冷边界是历史事实；回滚 = 否认「发生过」= 前缀证明与 seal 分叉。

**边界**：与 PREFIX-STABILITY-005 合并表述在 card 中；本命题覆盖 reanchor 一侧。

**证据**：CTX-015；HOST-006；`Context/Prefix/Epoch.fs`（apply 函数无回滚路径）。

---

## PREFIX-STABILITY-013：prefix identity 范围

**规范**（cache.md / `isAppendOnlyPrefix` 域）：参与前缀比较的字段 = provider、
model、variant、tools、system 与完整 message 序列。任一改变（即便 message 前缀仍成立）
都是冷边界而非 append；tools 必须相同不得只前缀。

**含义/动机**：KV cache 的命中面由这些字段决定；用「消息前缀差不多」冒充完整前缀
= 报告 provider 不会兑现的 cache hit。

**边界**：这些字段的内容语义归各自 owner；本命题拥有「它们参与 prefix identity」。

**证据**：`Domain/ProviderProjection.fs`（`isAppendOnlyPrefix`）；`tests/prefix-append-only-law.test.mjs`（NEW）。

---

## PREFIX-STABILITY-014：HOST-013 synthetic 正文不进 trace 系

**规范**（HOST-013 行为约束 4）：pair 正文不得进入 XTrace / Companion decode / Blogger delta /
work record / compaction input；仅 pair 的 durable 投影事实参与 HOST-013 恢复。

**含义/动机**：synthetic pair 是影响 prompt bytes / Prefix Cache 的合成历史，
但它是 provider 边界的机制；语义历史（XTrace）只记真实材料。

**边界**：XTrace 的 capture 边界归 semantic-trace（SEMANTIC-TRACE-002）；本命题拥有
「HOST-013 与 trace 系互斥」这一半。

**证据**：HOST-013 行为约束 4；`requirements/prefix-stability/tests/pair-thought-anchored.test.mjs`（REUSE 交叉）。

---

## PREFIX-STABILITY-015：synthetic id 确定性派生

**规范**（COMPANION-013）：synthetic id 由 SealRoot / frameEpoch / ordinal 等确定性派生；
禁止 GUID / random / 时间 / Host runtimeId。probe 成功 promote 时继承同一 SealRoot，
避免多余冷边界。`XPrefixProjection.forSnapshot` 使用 snapshot 自带的
`SyntheticMessageId`，不现场重派生（第二构造点会随每次派生漂移成冷边界）。

**含义/动机**：id 必须可重放稳定；换 id = 模型眼中新消息 = 前缀断裂。

**边界**：公式的具体输入归 companion identity HOW；本命题拥有「确定性 + 禁随机源」。

**证据**：COMPANION-013；`Domain/XPrefixProjection.fs`（`forSnapshot` 注释）；
`tests/companion-projection.test.mjs`（context-compression 包）`COMPANION_013_*`。
