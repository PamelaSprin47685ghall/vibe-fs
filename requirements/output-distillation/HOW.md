# output-distillation — HOW

## 物理清理与负向测试策略

1. **废止测试清理**：
   - 删除了断言已废止模型蒸馏行为的测试（`distiller-fragment-humility.test.mjs`、`executor-summarize.test.mjs`、`reconcile-supervisor-distill.test.mjs`、`executor-tool.test.mjs`）；
   - 将原 Large Gate（PROC-016）与 ToolResultBound（PROC-017）测试完整迁移至 `process-execution/tests/`，删除了本包侧的重复测试（`large-gate.test.mjs`、`large-gate-runner.test.mjs`、`tool-host-codec-full.test.mjs`），消除双主维护。
2. **唯一负向测试保留**：
   - 保留 `distiller-role-contract.test.mjs` 作为本包唯一的演进负向验证测试，绑定 `WHAT[DISTILL-014]`，严格断言系统角色枚举中无 Distiller、配置中无 Distiller 代理、且角色资源目录已被删除。

## DEPENDS ON

- `process-execution`
