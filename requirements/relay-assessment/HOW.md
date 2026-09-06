# relay-assessment — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Contract.fs(.fsi)`：ScoreVector、AssessmentBinding（含 IncumbencyId 绑定）、QualityCertificate、`RetirementOutcome`。精确 replay 须 identity/binding/snapshot/authority/scores 全一致，跨迭代重放拒绝。
- `src/Wanxiangshu/Mission/Relay/Assessment/Model.fs(.fsi)`：八维校验与 obligation derivation（Quality Ledger 写入）。
- `src/Wanxiangshu/Mission/Relay/Assessment/Admission.fs(.fsi)`：一次性 admission 与 atomic transaction。
- `src/Wanxiangshu/Mission/Relay/OpenCode/ReviewTool.fs(.fsi)`：OpenCode schema/codec；`spec.Description` 只用 `tool/review/description`，`acceptedResult` 按 `allPerfect` 在 `runtime/manager-work` 与 `runtime/manager-finish` 之间二选一；领域判断委托给 assessment owner。
- `src/Wanxiangshu/Mission/Manager/Workflow.fs(.fsi)`：`resourceForPhase` 按 phase 选择 nudge 文档：AuditPending 配 `runtime/manager-assess`，WorkOwned 配 `runtime/manager-work`，PerfectAwaitingRetirement/RetirementCleanupBlocked 配 `runtime/manager-finish`。
- `src/Wanxiangshu/Mission/Relay/Assessment/Surface.fs(.fsi)`：唯一 JS proof surface（schema parse）。

## 依赖关系

DEPENDS ON:
- `relay-incumbency`
- `obligation-ledger`
- `participant-identity`

## 验证

| 命题 | executable proof |
|---|---|
| ASSESS-001 | `requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] review schema is exactly eight required integer scores with no extras`；`requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] malformed scores are rejected without coercion`；`requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] valid payload preserves all eight exact integers` |
| ASSESS-002 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-002] second assessment in one iteration is rejected without overwriting the first`；`requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-002] cross-iteration replay of another iteration assessment is rejected` |
| ASSESS-003 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-003] assessment binds exact execution identity and rejects mismatched authority or iteration` |
| ASSESS-004 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-004] low-score assessment atomically records obligations and grants work ownership` |
| ASSESS-005 | `requirements/relay-assessment/tests/certificate.test.mjs::WHAT[ASSESS-005] all-ten assessment creates an exact-bound certificate and downgrades the phase` |
| ASSESS-006 | `requirements/relay-assessment/tests/certificate.test.mjs::WHAT[ASSESS-006] assessed iteration cannot submit a second review after work begins` |
| ASSESS-007 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-007] stale snapshot does not consume the one semantic assessment slot` |
| ASSESS-008 | `requirements/relay-assessment/tests/temporal-separation.test.mjs::WHAT[ASSESS-008] iteration phase separates assess work and finish before and after review` |
