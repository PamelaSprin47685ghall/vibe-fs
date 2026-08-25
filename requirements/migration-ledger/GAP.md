# migration-ledger — GAP

| GAP | 命题 | 缺口 | 状态 | 承载 | 计划 | Owner |
|---|---|---|---|---|---|---|
| GAP-ML-001 | MIGRATION-LEDGER-001..007 | 初始门禁仅覆盖 schema/DAG/coverage，11 类非法态未拦截 | CLOSED | `scripts/checks/migration-ledger.mjs` 11 检查 + `requirements/migration-ledger/tests/gate-rejection.test.mjs` 13 变异 + `scripts/checks/migration-ledger.mjs --self-test` 扩展 fixture | 已落地，63 节点 ledger 绿，25 DONE 均含 implementation_commit 祖先与 touched_paths 生产路径 | migration-ledger |
