# degeneration-guard — HOW

## 架构与核心机制

### 检测器与加权相异指标

- **LoopDetector**：使用 `o200k_base` 编码 token，维护指数衰减的加权相异度 $D_t = \lambda D_{t-1} + (1 - \lambda^{t - p})$（若已出现）。以固定均值作为初始 prior，单次低于阈值即触发 LOOP。
- **有界内存**：仅保存 `token_id -> last_step` 映射，更新复杂度 $O(1)$，内存上限由 tokenizer 词表大小严格绑定。

### 传感器与强杀桥接

1. **LoopSensor 观测**：监听 Host 流式事件中的 text 与 reasoning delta，排除 user-facing root 与非 managed 会话。
2. **强杀执行**：首次命中时记录内存标志 `LoopKillArmed` 并调用 Host SDK 中断当前 attempt。
3. **桥接 Recovery**：Reconciler 收到 abort 结果且匹配 `LoopKillArmed` 时，清除标记并调用 `FallbackLedger.recordConfirmedFailure`，无缝接入标准 A/A/B/B 恢复流程。

## 依赖关系

DEPENDS ON:
- `provider-attempt-recovery`
- `host-boundary`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DG-001 | `requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-002 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-003 | `requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-004 | `requirements/degeneration-guard/tests/loop-calibration.test.mjs` |
| DG-005 | `requirements/degeneration-guard/tests/loop-detector-memory.test.mjs` |
| DG-006 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-007 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-008 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-009 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-010 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-011 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-012 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
