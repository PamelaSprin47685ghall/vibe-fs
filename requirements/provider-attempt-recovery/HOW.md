# provider-attempt-recovery — HOW

## 架构与核心机制

### Cursor 代数与写入口

- **AgentPairCursor**：模 4 游标（Offset 0..3 映射到 SideA/SideA'/SideB/SideB'），维护连续失败计数与有限自动恢复预算（默认 12）。
- **FallbackLedger**：唯一写入口。负责对 `ProviderRunIdentity` 进行有界去重，追加 `FallbackCursorAdvanced`、`FallbackSucceeded` 或 `FallbackExhausted`。
- **RecoverySlot 槽决策**：把刚完成的 failure advance + primed Offset 归约为一次 `RecoveryOpportunity`；维护子请求失败与主请求失败均收敛为单次失败槽推进，维护成功不清零计数，主业务成功清零计数并把 A′/B′ 归一到同侧 A/B 普通槽。
- **历史 replay 边界**：PAR-004 改为成功关闭 A′/B′ 后，旧 journal 中已落盘的 `FallbackSucceeded → A′→B` / `FallbackSucceeded → B′→A` 仍必须可重放。fold 只吸收这一种“成功归一化后 previousOffset 比 canonical cursor 多一步”的历史形状；新 writer 永远从归一化后的 A/B 写 `A→A′` / `B→B′`，不得继续生产旧形状。

### 恢复编排

1. **已确认失败识别**：从完整快照与失败终态中提取确切的 `ProviderRunIdentity`。
2. **Admission 裁决**：先写 `FallbackCursorAdvanced`；只有预算允许的 `RecoveryAdvanced` 才继续。WorkMain 在新 primed 槽获得一次 X opportunity；BloggerMain 在新 primed 槽且有 frames 时先发送 BloggerSquash。
3. **Blogger retry 所有权**：失败 open request 先 abandon；下一 typed request 在物理发送前 materialize，并在 send 后绑定该次 PromptKey。Main→Main、Main→Squash、Squash→Main 共用同一规则。
4. **事件解锁**：WorkMain recovery 只在 linked Blogger 存在 durable open request 时通过 `AgentJournal.awaitChangeFromOrCancel` 订阅 committed journal change；`BlogObservationCommitted`、`BlogObservationsSquashed`、`BloggerRequestAbandoned` 等 fact 到达后重新求值，plugin shutdown 显式注销订阅。无 open producer 立即 retry，不读取 flight/pending，不存在 timeout/polling。
5. **成功记账**：RequestKind 从 typed request / durable receipt / accepted continuation evidence 证明。Squash/repair success 不写 FallbackSucceeded；WorkMain/BloggerMain success 才清零失败计数。
6. **身份隔离**：游标变更仅影响下一次派发的 `EffectiveAgent`，不改写 Persona、语言或 system prompt 字节。

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
| PAR-016 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs` |
| PAR-017 | `requirements/context-compression/tests/companion-recovery-slot.test.mjs` |
| PAR-018 | `requirements/context-compression/tests/companion-recovery-slot.test.mjs` |
