# provider-attempt-recovery — HOW

## 架构与核心机制

### Cursor 代数与写入口

- **AgentPairCursor**：模 4 游标（Offset 0..3 映射到 SideA/SideA'/SideB/SideB'），维护连续失败计数与有限自动恢复预算（默认 12）。
- **FallbackLedger**：唯一写入口。负责对 `ProviderRunIdentity` 进行有界去重，追加 `FallbackCursorAdvanced`、`FallbackSucceeded` 或 `FallbackExhausted`。
- **RecoverySlot 槽决策**：维护子请求失败与主请求失败均收敛为单次失败槽推进；维护成功不清零计数，主业务成功清零计数。

### 恢复编排

1. **已确认失败识别**：从完整快照与失败终态中提取确切的 `ProviderRunIdentity`。
2. **Admission 裁决**：预算充足时发出 `ContinueRecovery` 并发送 `ProviderRetryAttempt` continuation；预算耗尽时发出 `RecoveryExhausted` 停止自动重试。
3. **身份隔离**：游标变更仅影响下一次派发的 `EffectiveAgent`，不改写 Persona、语言或 system prompt 字节。

## 依赖关系

DEPENDS ON:
- `participant-identity`
- `execution-model-routing`
- `interaction-authority`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PAR-001 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-002 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-003 | `requirements/provider-attempt-recovery/tests/fallback-ledger.test.mjs` |
| PAR-004 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-005 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-006 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-007 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-008 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs` |
| PAR-009 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-010 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs` |
| PAR-011 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs` |
| PAR-012 | `requirements/provider-attempt-recovery/tests/abort-residue.test.mjs` |
| PAR-013 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs` |
| PAR-014 | `requirements/provider-attempt-recovery/tests/fallback-ledger.test.mjs` |
| PAR-015 | `requirements/provider-attempt-recovery/tests/fallback-aabb-confluence.test.mjs` |
