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
| ASSESS-001/002/007 | `requirements/relay-assessment/tests/review-tool-contract.test.mjs` |
| ASSESS-004 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs` |
| ASSESS-005/006 | `requirements/relay-assessment/tests/certificate.test.mjs` |

