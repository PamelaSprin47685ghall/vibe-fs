# relay-assessment — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Contract.fs(.fsi)`：ScoreVector、AssessmentBinding、QualityCertificate。
- `src/Wanxiangshu/Mission/Relay/Assessment/Model.fs(.fsi)`：八维校验与 obligation derivation。
- `src/Wanxiangshu/Mission/Relay/Assessment/Admission.fs(.fsi)`：一次性 admission 与 atomic transaction。
- `src/Wanxiangshu/Mission/Relay/OpenCode/ReviewTool.fs(.fsi)`：OpenCode schema/codec，领域判断委托给 assessment owner。
- `src/Wanxiangshu/Mission/Relay/Assessment/Surface.fs(.fsi)`：唯一 JS proof surface。

## 依赖关系

DEPENDS ON:
- `relay-incumbency`
- `obligation-ledger`
- `participant-identity`

## 验证

| 命题 | executable proof |
|---|---|
| ASSESS-001 | `requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] review schema is exactly eight required integer scores with no extras`；`requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] malformed scores are rejected without coercion`；`requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] valid payload preserves all eight exact integers` |
| ASSESS-002 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-002] second assessment in one incumbency is rejected without overwriting the first` |
| ASSESS-003 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-003] assessment binds exact execution identity and rejects mismatched authority or incumbency` |
| ASSESS-004 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-004] low-score assessment atomically records obligations and grants work ownership` |
| ASSESS-005 | `requirements/relay-assessment/tests/certificate.test.mjs::WHAT[ASSESS-005] all-ten assessment creates an exact-bound certificate and downgrades the phase` |
| ASSESS-006 | `requirements/relay-assessment/tests/certificate.test.mjs::WHAT[ASSESS-006] assessed incumbency cannot submit a second review after work begins` |
| ASSESS-007 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-007] stale snapshot does not consume the one semantic assessment slot` |
