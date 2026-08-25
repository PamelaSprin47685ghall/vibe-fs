# migration-ledger — HOW

## 架构与实现机制

`migration-ledger` 作为施工 DAG 的治理包，不含领域 runtime 代码，其保证由机械门禁与变异测试共同承载：

### 1. 门禁本体（`migration-ledger.mjs`）

`scripts/checks/migration-ledger.mjs` 实现 `validateLedger` 纯函数，按序执行：
- Schema 与重复检查、分类/状态/结果合法性、数组类型校验
- 依赖 referential integrity 与 kind 校验、READY 依赖 DONE 校验
- Kahn 拓扑排序环检测
- 覆盖完整性（semantic-owners 对齐）
- PENDING 证据纯度（verified/complete/green 大小写不敏感）
- READY owner 图与 proof/gate 校验
- DONE 闭环（结果非 PENDING、分类兼容、implementation_commit 祖先、touched_paths 生产路径、proofs/gates 非空、覆盖归属）
- closure 依赖 DONE 校验
- 基线冻结（deadcode / provider-prose baseline 对比 HEAD）

所有校验 fail-closed，错误聚合后返回 `{ok, errors}`，`main` 根据 `ok` 以非零退出。

### 2. 自检套件（`--self-test`）

`runSelfTest(validLedger, owners)` 以 5 基础 fixture（环、缺 kind、READY 非 DONE、覆盖缺失、基线接受）+ 11 扩展 fixture（PENDING GREEN、READY 无 owner、READY 无 proof、DONE PENDING、分类错配、缺提交、非祖先提交、缺 touched、缺 proofs、缺 gates、closure 未 DONE、覆盖无归属、基线增长）共 15+ 覆盖，任何篡改致门禁失活即红。

### 3. 变异拒绝测试（`gate-rejection.test.mjs`）

`requirements/migration-ledger/tests/gate-rejection.test.mjs` 对合法 ledger 的深拷贝施加 11 类独立非法变异，每类断言 `validateLedger` 返回 `ok:false` 且错误信息命中对应关键字，覆盖 PENDING 证据、READY 条件、DONE 闭环、分类兼容、提交祖先、变更路径、证明门禁、闭合依赖、覆盖归属与基线增长。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| MIGRATION-LEDGER-001 | `requirements/migration-ledger/tests/gate-rejection.test.mjs` + `scripts/checks/migration-ledger.mjs --self-test` |
| MIGRATION-LEDGER-002 | `requirements/migration-ledger/tests/gate-rejection.test.mjs` |
| MIGRATION-LEDGER-003 | `requirements/migration-ledger/tests/gate-rejection.test.mjs` |
| MIGRATION-LEDGER-004 | `requirements/migration-ledger/tests/gate-rejection.test.mjs` |
| MIGRATION-LEDGER-005 | `requirements/migration-ledger/tests/gate-rejection.test.mjs` |
| MIGRATION-LEDGER-006 | `requirements/migration-ledger/tests/gate-rejection.test.mjs` |
| MIGRATION-LEDGER-007 | `requirements/migration-ledger/tests/gate-rejection.test.mjs` + `scripts/checks/migration-ledger.mjs --self-test` |
