# relay-assessment — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Contract.fs(.fsi)`：ScoreVector、AssessmentBinding（含 IncumbencyId 绑定）、QualityCertificate、`RetirementOutcome`。精确 replay 须 identity/binding/snapshot/authority/scores 全一致，跨迭代重放拒绝。
- `src/Wanxiangshu/Mission/Relay/Assessment/Model.fs(.fsi)`：八维校验与 obligation derivation（Quality Ledger 写入）。
- `src/Wanxiangshu/Mission/Relay/Assessment/Admission.fs(.fsi)`：一次性 admission 与 atomic transaction。
- `src/Wanxiangshu/Mission/Relay/OpenCode/ReviewTool.fs(.fsi)`：OpenCode schema/codec；`spec.Description` 只用 `tool/review/description`，`acceptedResult` 按 `allPerfect` 在 `runtime/manager-work` 与 `runtime/manager-finish` 之间二选一；领域判断委托给 assessment owner。
- `src/Wanxiangshu/Mission/Manager/Workflow.fs(.fsi)`：`resourceForCurrentAction` 只从 active incumbent、accepted assessment transport 与 exact bound certificate 选择 nudge 文档：无 assessment 配 `runtime/manager-assess`，未持有效证书配 `runtime/manager-work`，exact valid certificate 配 `runtime/manager-finish`。
- `src/Wanxiangshu/Mission/Relay/Assessment/Surface.fs(.fsi)`：唯一 JS proof surface（schema parse）。

## 编译边界

`mission-relay-workspace-snapshot` 直接引用 `runtime-platform/digest`，保留 GitSubject 与 Relay core 的真实依赖，不再因字符串摘要引入 OpenCode 消息／事件合同。`WorkspaceSnapshot.canonical` 的 HEAD tree、status、index、binary diff、untracked blob hash 及分隔符不变，`capture` 和公开签名不变。

在 `be3054fab` 上按声明 ProjectReference 的递归闭包测量，本分片由 8 项目／38 个 `.fs/.fsi` 输入收窄至 4 项目／14 个输入；独立 Fable 编译通过 52 parsed sources（fingerprint `068168b18cec`）。ReviewTool、SuicideTool、Manager Workflow、PluginHooks 四条真实 consumer 路径的 focused 并集编译通过 1284 parsed sources／1246 items（`6d4bccc9b9ff`）。新隔离产物 smoke 在临时 Git 仓库验证无 HEAD 空仓的 canonical 字节，以及 Unicode／CRLF untracked 文件的 capture 与独立 SHA-256 一致，临时目录已清理。下表 assessment／certificate 测试使用 opaque snapshot identity，不冒称覆盖真实 Git capture；本次 smoke 也不证明全部 staged、conflict 或退休时序。

## 依赖关系

DEPENDS ON:
- `relay-incumbency`
- `obligation-ledger`
- `participant-identity`

## 验证

| 命题 | executable proof |
|---|---|
| ASSESS-001 | `requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] review schema is eight required PERFECT/REVISE/N/A scores with optional note`；`requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] malformed scores are rejected without coercion`；`requirements/relay-assessment/tests/review-tool-contract.test.mjs::WHAT[ASSESS-001] valid payload preserves exact ratings and rejects only on REVISE` |
| ASSESS-002 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-002] second assessment in one iteration is rejected without overwriting the first`；`requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-002] cross-iteration replay of another iteration assessment is rejected` |
| ASSESS-003 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-003] assessment binds exact execution identity and rejects mismatched authority or iteration` |
| ASSESS-004 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-004] revise assessment atomically records obligations and grants work ownership` |
| ASSESS-005 | `requirements/relay-assessment/tests/certificate.test.mjs::WHAT[ASSESS-005] no-revise assessment creates an exact-bound certificate and downgrades the phase` |
| ASSESS-006 | `requirements/relay-assessment/tests/certificate.test.mjs::WHAT[ASSESS-006] assessed iteration cannot submit a second review after work begins` |
| ASSESS-007 | `requirements/relay-assessment/tests/assessment-transaction.test.mjs::WHAT[ASSESS-007] stale snapshot does not consume the one semantic assessment slot` |
| ASSESS-008 | `requirements/relay-assessment/tests/temporal-separation.test.mjs::WHAT[ASSESS-008] iteration phase separates assess work and finish before and after review` |
