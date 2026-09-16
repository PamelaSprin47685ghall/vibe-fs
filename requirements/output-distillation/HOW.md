# output-distillation — HOW

## 演进与迁移架构

`output-distillation` 包已转为撤销声明与演进记录，其有效条款已全部迁入 `process-execution`：

| 旧条款编号 | 旧条款主题 | 演进状态 | 新规范落点 |
| --- | --- | --- | --- |
| DISTILL-001 | 大输出固定成本提炼 | 撤销 | `process-execution` / PROC-013 |
| DISTILL-002 | 关键事实区分性保留 | 撤销 | `process-execution` / PROC-013 |
| DISTILL-003 | 截断谦逊声明 | 迁出 | `process-execution` / PROC-014 |
| DISTILL-004 | 禁止自动 fan-out/reduce | 撤销 | 无（模型蒸馏删除） |
| DISTILL-005 | 摘要自包含与可定位性 | 迁出 | `process-execution` / PROC-014 |
| DISTILL-006 | 唯一 Distiller 失败降级 | 撤销 | 无（模型蒸馏删除） |
| DISTILL-007 | Spool 窗口消费 | 迁出 | `process-execution` / PROC-015 |
| DISTILL-008 | Distiller await 与 permit | 撤销 | 无（模型蒸馏删除） |
| DISTILL-009 | Distiller 叶子运行时约束 | 撤销 | 无（角色已删除） |
| DISTILL-010 | Distiller 权能约束 | 撤销 | 无（角色已删除） |
| DISTILL-011 | Large Gate 预算互斥门禁 | 迁出 | `process-execution` / PROC-016 |
| DISTILL-012 | 自定义工具留尾截断 | 迁出 | `process-execution` / PROC-017 |
| DISTILL-013 | 蒸馏仪表盘禁令 | 撤销 | 无（模型蒸馏删除） |
| DISTILL-014 | 零 Distiller 负向保证 | 新增 | `distiller-role-contract.test.mjs` |

## 物理清理与负向测试策略

1. **废止测试清理**：
   - 删除了断言已废止模型蒸馏行为的测试（`distiller-fragment-humility.test.mjs`、`executor-summarize.test.mjs`、`reconcile-supervisor-distill.test.mjs`、`executor-tool.test.mjs`）；
   - 将原 Large Gate（PROC-016）与 ToolResultBound（PROC-017）测试完整迁移至 `process-execution/tests/`，删除了本包侧的重复测试（`large-gate.test.mjs`、`large-gate-runner.test.mjs`、`tool-host-codec-full.test.mjs`），消除双主维护。
2. **唯一负向测试保留**：
   - 保留 `distiller-role-contract.test.mjs` 作为本包唯一的演进负向验证测试，绑定 `WHAT[DISTILL-014]`，严格断言系统角色枚举中无 Distiller、配置中无 Distiller 代理、且角色资源目录已被删除。

## DEPENDS ON

- `process-execution`
