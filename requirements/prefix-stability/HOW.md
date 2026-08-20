# prefix-stability — HOW

## 架构与核心机制

### 前缀快照与投影

- **PrefixSnapshot**：包含冻结前缀 blob 引用、cutoff 游标、覆盖前缀 digest、SealRoot 与 SyntheticMessageId。
- **ActivePrefixEpoch**：记录当前 EpochId 与可选的 PrefixSnapshot，并维护已重锚 run 集合防止重复处理。
- **Append-Only 判定**：由 `ProviderProjection.isAppendOnlyPrefix` 权威裁决后一次请求是否保持了前一次请求的完整字节前缀（包括 tools、system、model 等）。

### 冷边界流转

1. **Probe 提升**：Probe 成功后触发 `PrefixRebaseCommitted(EvidenceKind=Probe)`，提升 Epoch 并激活新快照。
2. **TodoCheckpoint Rebase**：基于 committed Accepted 链计算 lag-1 cutoff，在 seal 前提交 `PrefixRebaseCommitted(EvidenceKind=TodoCheckpoint)`。
3. **Compaction 重锚**：收容外部 compaction 事实，生成 `ContextReanchored`，清空快照并使 PrefixCoverage 归零。

## 依赖关系

DEPENDS ON:
- `provider-projection`
- `context-compression`
- `provider-language`
- `participant-identity`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PREFIX-STABILITY-001 | `requirements/prefix-stability/tests/prefix-append-only-law.test.mjs` |
| PREFIX-STABILITY-002 | `requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-003 | `requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-004 | `requirements/prefix-stability/tests/prefix-epoch-todo-checkpoint.test.mjs` |
| PREFIX-STABILITY-005 | `requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-006 | `requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-007 | `requirements/prefix-stability/tests/system-prompt-stability.test.mjs` |
| PREFIX-STABILITY-008 | `requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs` |
| PREFIX-STABILITY-009 | `requirements/prefix-stability/tests/projection-algebra-step5-digest.test.mjs` |
| PREFIX-STABILITY-010 | `requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` |
| PREFIX-STABILITY-011 | `requirements/prefix-stability/tests/prefix-append-only-law.test.mjs` |
| PREFIX-STABILITY-012 | `requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-013 | `requirements/prefix-stability/tests/prefix-append-only-law.test.mjs` |
| PREFIX-STABILITY-014 | `requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` |
| PREFIX-STABILITY-015 | `requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs` |
