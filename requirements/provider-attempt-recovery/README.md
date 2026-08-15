# provider-attempt-recovery

> 单次 provider attempt 已确认失败后，可在不改变 authority/personhood 的前提下有界换执行绑定继续。

## 一句话 WHY

一次物理 provider attempt 失败了，系统不能重选 Authority、不能换 Persona、不能无限自动烧钱；
它必须在**同一 Logical Run、同一身份**内有限地换执行绑定再试，然后在预算耗尽时干净地停下。
（详见 `WHY.md`）

## WHAT 概览

唯一 normative 合同在 `WHAT.md`（15 条命题，`PAR-001`..`PAR-015`）：
- Fallback 属 Logical Run，新 Authority Root 开新 cursor（PAR-001）
- cursor 是 modulo-4 封闭 DU，损坏字节 fail-closed（PAR-002）
- `FallbackLedger` 唯一写入口，同一次失败最多推进一次（PAR-003）
- 推进不变量：失败 +1 count / 成功归零且 Offset 不动（PAR-004）
- 有界预算：耗尽写 `FallbackExhausted`，无自动下一步（PAR-005/006）
- fold 拒绝条件与 parked-cursor / armed 合取（PAR-007/011）
- 空/XML-only 不计、Host abort 残留不计、Host Attempt ≠ 领域计数（PAR-008/012/009）
- 槽内维护子请求、只换 EffectiveAgent、continuation 时序、replica 隔离（PAR-010/013/014/015）

## HOW 概览

实现模型见 `HOW.md`：`Domain/AgentPairCursor.fs`（纯算术）、`Domain/RecoverySlot.fs`（槽决策）、
`Application/Recovery/FallbackLedger.fs`（唯一写入口）、`FallbackEvidence.fs`（只读查询）、
`ProviderRecoveryWorkflow.fs`（恢复编排与 degeneration-guard 桥接）。

## Proof 概览

`PROOF.md` 给出每条命题的测试落点：
- MOVE：`tests/cursor.test.mjs`（原 `requirements/provider-attempt-recovery/tests/cursor.test.mjs`，34 断言）
- NEW：`tests/fallback-ledger.test.mjs`（4 断言：NoActiveRun / 去重 / admission）
- REUSE：`requirements/context-compression/tests/recovery-slot.test.mjs`（FALLBACK-008/011/012）、
  `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs`（FALLBACK-013）、
  `requirements/prefix-stability/tests/system-prompt-stability.test.mjs`（FALLBACK-014）等，均含 SPLIT@cutover 计划

## 阅读顺序

1. `WHY.md`（为什么存在、何时 RED、与 crash-reconciliation / degeneration-guard 的边界）
2. `WHAT.md`（15 条 normative 命题）
3. `HOW.md`（代码怎么满足）
4. `PROOF.md`（怎么验证、红了说明什么）

## 依赖

DEPENDS ON：`participant-identity`（换执行者 ≠ 换人；身份字节 guarantee 由 identity 包提供）、`execution-model-routing`（EffectiveAgent 对应的 session model lease）、`interaction-authority`（continuation 的 wire/authority 语义）。理由：PAR-013 的「只换 EffectiveAgent，再由 lane/lease 解析物理执行」消费前两者；PAR-014 的「continuation 只在该 Run 内」消费 interaction authority。
