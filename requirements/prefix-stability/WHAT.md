# prefix-stability — WHAT

## PREFIX-STABILITY-001: 同 epoch append-only prefix law

在同一 PrefixEpoch 内，第 $n$ 次请求的 provider wire 必须是第 $n+1$ 次请求的精确字节前缀。权威判定唯一由 `ProviderProjection.isAppendOnlyPrefix` 给出，比较范围包含 provider、model、variant、tools、system prompt 及完整 message 序列。Tools 必须完全一致，严禁仅为前缀。

## PREFIX-STABILITY-002: 冷边界只有三证据源

同一 PrefixEpoch 内前缀逐字节稳定。Epoch 切换仅允许由以下三个已提交证据源驱动，且必须使 `EpochId += 1`：
1. 成功 prefix probe 提升（`EvidenceKind=Probe`）；
2. Host compaction 重锚（`Snapshot=None`）；
3. TodoCheckpoint lag-1 rebase（`EvidenceKind=TodoCheckpoint`）。
严禁按容量或 Token 数量主动切换 epoch。

## PREFIX-STABILITY-003: candidate ≠ committed

未提交的候选前缀仅进入不可变的 attempt profile，不修改 `ActivePrefixEpoch`。Probe 失败直接丢弃候选，不产生任何持久化事件，亦无回滚事实。

## PREFIX-STABILITY-004: ActivePrefixEpoch 是唯一 epoch SSOT

既有的 `ActivePrefixEpoch` 与 `PrefixRebaseCommitted` 是系统唯一的 prefix epoch 单一真相源。Magic Todo lag-1 rebase 必须接入该合同，禁止维护平行的 todo-only epoch 或状态机旁路。

## PREFIX-STABILITY-005: 已 seal epoch 不因 provider 成败回滚

`PrefixRebaseCommitted` 在下一次真实 provider attempt seal 绑定前原子提交。随后的 provider Failed 或 Aborted 结局绝不回滚已 seal 的 epoch。

## PREFIX-STABILITY-006: ContextReanchored 重锚语义

Host compaction 的唯一合法收容语义是 `ContextReanchored`：`PrefixEpochId + 1`、`Snapshot = None` 且 PrefixCoverage 归零。重锚记录已发生 run 集合，同一个 compaction run 绝不重锚两次。

## PREFIX-STABILITY-007: 同 Life system prompt byte-identical

Office system prompt 在同一个生命周期（Life）内保持逐字节一致。严禁因 T1 交托、Fallback 切换、Review 或 Host compaction 等事件改写 system prompt 字节或重绑 Persona。

## PREFIX-STABILITY-008: FrozenRecordPrefix 是明确标记的 low-trust context

FrozenRecordPrefix 以明确标记的 low-trust context block 注入上下文，绝不伪装为人类或系统指令；同一 epoch 内其内容保持完全冻结。

## PREFIX-STABILITY-009: cutoff 只在 canonical XTrace 完整语义 turn 边界，digest 失配 fail closed

Cutoff 游标只能位于 current-generation canonical XTrace 的完整 semantic turn 边界。`CoveredPrefixDigest` 的生产与 probe step-5 重算必须对同一个 `XTraceMaterialization.currentProjection` 做截断；请求级 provider presentation 的合法变化不得制造假 `CutoffProofFailed`。写回 Host 时必须把 semantic-turn cutoff 映射为 stable XTrace Host message identity，严禁把 cutoff 当作本次 provider message 数组下标。`ProviderRetryAttempt` 不具有 X semantic part，因此当前 retry 的 request-start cutoff 可以从与 XTrace capture 相同的 decodable Host message universe 取得 semantic-turn 坐标；这只用于定位 live request，不得把 retry 文本重新引入 canonical X。retry transport row 的退休同样受 prefix horizon 约束：`Current` presentation 必须保留已经进入当前 provider prefix 的所有 retry rows；只有新的 `TentativeCold` presentation 才可按 stable Host id 精确退休此前 horizon 的 retry rows，同时保留触发本次 cold presentation 的 current physical retry。由此 retry 控制文本可以不进入 X/Y/digest，而同一 horizon 的 provider wire 仍保持 append-only。真实 canonical X 历史发生变化时 digest 仍必须失配并 fail-closed 拒绝执行。

## PREFIX-STABILITY-010: 同一 horizon 的 guidance occurrence 原位 replay；reanchor 退休旧 replay set

全 provider 统一 cursor 模式：永不产生 synthetic skill 消息。Pair-programming guidance 仅以 `NUL+BOM` 后缀形式附着于终端真实工具结果（completed/error 均可）；无终端工具结果的轮次不产生任何 guidance 载体。在同一未重锚 horizon 内，历史 guidance 字节必须按其持久化的 occurrence 保持原位置、原字节回放，禁止删除、过滤、去重、重新定位或叠加第二后缀；重放先剥离后缀做 placement 判定再精确重附。`ContextReanchored` 将旧 occurrence 的可见性退休，新 occurrence 采用新的序号追加。

## PREFIX-STABILITY-011: 冷边界由事实驱动

同一 epoch 内历史前缀严禁发生漂移，严禁通过频繁切换 PrefixEpoch 来掩盖实现层的前缀漂移缺陷。历史 marker 不得在重放时重新计算流逝时间。

## PREFIX-STABILITY-012: reanchor/rebase 一旦提交不因后续 provider failure 回滚

已合法提交的 `ContextReanchored` 与 `PrefixRebaseCommitted` 是不可逆的历史事实，后续的 provider 失败绝不撤销已提交的重锚与 rebase。

## PREFIX-STABILITY-013: prefix identity 范围

参与前缀一致性比较的范围包括 provider、model、variant、tools、system prompt 与消息序列。任何一项发生变更均构成冷边界，不可当作追加前缀处理。

## PREFIX-STABILITY-014: HOST-013 guidance 后缀正文不进 trace 系

Pair-programming guidance 后缀正文仅用于影响 provider prompt 字节以维持前缀缓存，严禁进入 XTrace、Companion decode、Blogger delta、WorkRecord 或 compaction 输入；仅 guidance 的 durable occurrence 投影事实（PairProgrammingGuidelineAnchored → Guidelines）参与 HOST-013 恢复。

## PREFIX-STABILITY-015: synthetic id 确定性派生

Synthetic ID 必须由 SealRoot、frameEpoch 与 ordinal 等输入确定性派生，严禁使用 GUID、随机数、时间戳或临时运行时 ID。
