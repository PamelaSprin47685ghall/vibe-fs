# prefix-stability

> 同一 semantic epoch 内已呈现给 provider 的前缀必须保持稳定；冷边界只能由事实驱动。

## 一句话 WHY

provider cache 与认知连续性依赖「已呈现的过去不会无故重排」。若同一 semantic epoch 的历史
字节不断搬家，identity / language / guidance 即使语义相同也会形成新的世界。

## WHAT 概览（→ WHAT.md）

| 组 | 命题 | 保证 |
|---|---|---|
| 前缀律 | PREFIX-STABILITY-001/013 | 同 epoch append-only：ProviderWire(n) ⊏ ProviderWire(n+1)；isAppendOnlyPrefix 唯一权威 |
| 冷边界 | PREFIX-STABILITY-002/006 | 只有三证据源：probe 提升 / compaction 重锚 / TodoCheckpoint；重锚语义 |
| 候选分离 | PREFIX-STABILITY-003 | candidate ≠ committed；ProjectionChoice attempt-local |
| epoch SSOT | PREFIX-STABILITY-004/005/012 | ActivePrefixEpoch 唯一；seal 后不因 provider 成败回滚 |
| 字节稳定 | PREFIX-STABILITY-007/008/009 | system prompt byte-identical；FrozenRecordPrefix 明确标记；cutoff digest fail closed |
| HOST-013 | PREFIX-STABILITY-010/011/014 | 历史 pair 原位 replay；anchor 缺失不重定位；synthetic 正文不进 trace |
| 身份稳定 | PREFIX-STABILITY-015 | synthetic id 确定性派生 |

## HOW 概览（→ HOW.md）

- 类型：`Domain/PrefixCandidate.fs`（PrefixSnapshot/PrefixProbe/XProjectionChoice）、
  `Domain/XPrefixProjection.fs`（forSnapshot/forChoice/requiredBlob）、
  `Domain/MagicTodoPrefixEpoch.fs`（TodoCheckpoint 同一 epoch 合同）
- epoch：`Context/Prefix/Epoch.fs`（applyRebase/applyReanchor/isReanchored）、
  `Context/Companion/Blogger/ContextFactFold.fs`（PrefixRebaseCommitted/ContextReanchored）
- 权威判定：`Domain/ProviderProjection.isAppendOnlyPrefix`；生产前置 proof 与回归测试共用
  （`Context/Prefix/XWire.fs`）

## proof 概览（→ PROOF.md）

- MOVE：`requirements/prefix-stability/tests/prefix-epoch.test.mjs` → `requirements/prefix-stability/tests/`
- REUSE：`requirements/prefix-stability/tests/system-prompt-stability.test.mjs`（byte invariants）、
  `requirements/prefix-stability/tests/pair-thought-anchored.test.mjs`（HOST-013 前缀律端到端）、
  `requirements/prefix-stability/tests/g2-inspector-provider-wire-prefix.test.mjs`（PREFIX LAW on reused child）
- NEW：`prefix-append-only-law.test.mjs`、`prefix-epoch-todo-checkpoint.test.mjs`

## 阅读顺序

1. `WHY.md` → 2. `WHAT.md` → 3. `HOW.md` → 4. `PROOF.md`

## DEPENDS ON

- `provider-projection`：prefix 的意图/表示由 projection 定义；本包只拥有稳定性合同。
- `context-compression`：candidate 何时有资格（CTX-011 判定）是替换前提。
- `provider-language` / `participant-identity`：若 identity/language 材料属于 prefix identity，
  其稳定性由本包要求（内容本身归它们）。

## 边界（DOES NOT OWN）

- 为什么需要 compression/rebase → `context-compression`
- provider language/identity/cognition 内容 → `provider-language` / `participant-identity`
- renderer 实现 → `provider-projection`
- 当前 gap-anchor / synthetic empty-name `skill` / Cursor suffix HOW（可整体替换）
- fold 拒绝语义 → `durable-events`
