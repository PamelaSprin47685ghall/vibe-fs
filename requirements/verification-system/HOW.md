# verification-system — HOW

## 架构与实现机制

`verification-system` 作为验证体系的元规则包，通过分层运行机制、静态门禁管理与因果监督器共同落实证据完整性：

### 1. 证据阶梯与构建编排（`proof ladder`）

`tests/proof-ladder.test.mjs` 对全局构建与测试命令链（`package.json` 中的 `format-build-test` 及 `scripts/check.mjs`）进行强约束：
- 严格锁定第 0 层静态门禁、第 1–3 层纯逻辑与时序单元测试、第 4 层单入点物理 Long Stroke 与第 5 层 Release 构建的执行顺序。
- 确保 `scripts/check.mjs` 中注册的所有门禁脚本路径在磁盘上真实存在，且任何门禁失败时其非零退出码均能正确向上传播（fail-closed）。
- `scripts/checks/proof-levels.json` 独立保存精确 `(path, title, what_id) → level` 分类；共享 resolver 对缺失或重复键返回无权威结果，registry validator 阻断形状、层序与键歧义，外部登记行只能匹配该分类而不能自我改标。
- `scripts/build.mjs` 每次调用都持有跨进程 build lock，先删除上一轮 `dist/` artifact tree，再以显式 `Debug` configuration 执行一次真实 Fable compile；compiler 成功退出后才验证 `dist` 与 Surface Manifest。源码删除因此不会留下可被 package 收走的陈旧 JS。configuration 不依赖 Fable 的 watch/one-shot 默认值。不存在 watch-daemon、source-touch barrier、ack、artifact-exists fast path 或 wall-clock freshness 猜测，因此旧 `dist` 不能冒充当前源码的编译结果。

### 2. 因果看门狗与静默监督（`e2e-watchdog-feed`）

`tests/e2e-watchdog-feed.test.mjs` 与因果原语套件负责守卫时序推进契约：
- 确保 E2E 物理测试中看门狗计时器仅由明确的因果事件（如目标事实增长、检查点达成）驱动续期。
- 严禁顶层测试用例直接调用底层计时器的内部 advance 接口，防止由于传输层噪声或背景任务活动导致看门狗被非法延期。

### 3. 物理契约显式声明（`physical-contract`）

`tests/physical-contract.test.mjs` 强制要求唯一的 Long Stroke 物理入口显式声明其所依赖的不可模拟物理契约（如真实子进程生命周期、物理消息 ID 绑定）；无明确物理契约依赖的测试场景必须降级至底层 Pure 或 Temporal 证据层。

### 4. 覆盖率分母完整性守卫（`coverage-gate`）

`tests/coverage-gate.test.mjs` 与覆盖率策略模块（`tests/support/coverage-policy.mjs`）确保在覆盖率统计前，预先导入全部生产模块，杜绝未加载模块脱离统计分母导致的虚假高覆盖率。

### 5. JS 语义契约边界门禁（`js-boundary-gate`）

`tests/js-boundary-gate.test.mjs` 机械化断言语义测试环境与底层实现之间的彻底解耦：
- 验证生产语义测试中零深度导入（deep dist imports）、零混淆导出探测（mangled name lookup）以及零底层编译器表示依赖。
- 确保所有公开给测试的入口均在 `SURFACE_MANIFEST` 中完成完备注册并有明确命题授权。

### 6. 非机械度量断言（`no-line-count-check`）

`tests/no-line-count-check.test.mjs` 结构化验证仓库的所有门禁与检查套件中，不存在任何形式的文件行数或尺寸限制逻辑，确保质量保障专注于真实的架构语义和规范不变量。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| VERIFICATION-SYSTEM-001 | `requirements/verification-system/tests/proof-ladder.test.mjs::WHAT[VERIFICATION-SYSTEM-001] format-build-test ladder pins the five layers in order` |
| VERIFICATION-SYSTEM-002 | `requirements/verification-system/tests/proof-ladder.test.mjs::WHAT[VERIFICATION-SYSTEM-002] l4 has exactly one e2e entry in the ladder` |
| VERIFICATION-SYSTEM-003 | `requirements/verification-system/tests/physical-contract.test.mjs::WHAT[VERIFICATION-SYSTEM-003] sole e2e entry declares unsimulatable physical contracts` |
| VERIFICATION-SYSTEM-004 | `requirements/verification-system/tests/deadcode-scan.test.mjs::WHAT[VERIFICATION-SYSTEM-004] deadcode_private_binding_without_any_repository_reference_is_red` |
| VERIFICATION-SYSTEM-005 | `requirements/verification-system/tests/walk-fail-closed.test.mjs::WHAT[VERIFICATION-SYSTEM-005] walk throws on a missing root instead of returning an empty array` |
| VERIFICATION-SYSTEM-006 | `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs::WHAT[VERIFICATION-SYSTEM-006] top-level e2e tests never feed watchdog directly` |
| VERIFICATION-SYSTEM-007 | `requirements/verification-system/tests/temporal-harness.test.mjs::WHAT[VERIFICATION-SYSTEM-007] deterministic queue enumerates races explicitly`；`requirements/verification-system/tests/identity-capacity-interleaving.test.mjs::WHAT[VERIFICATION-SYSTEM-007] executes every valid identity/admission/capacity causal interleaving`；`requirements/verification-system/tests/identity-capacity-interleaving.test.mjs::WHAT[VERIFICATION-SYSTEM-007] rejects every causally invalid ordering before effects`；`requirements/verification-system/tests/identity-capacity-interleaving.property.test.mjs::WHAT[VERIFICATION-SYSTEM-007] deterministic families preserve replay, restart, identity, and fence laws` |
| VERIFICATION-SYSTEM-008 | `requirements/verification-system/tests/guide-contract.test.mjs::WHAT[VERIFICATION-SYSTEM-008] AgentProgram publishes its flow entrypoints`；`requirements/verification-system/tests/build-freshness.test.mjs::WHAT[VERIFICATION-SYSTEM-008] Fable build compiles once and never accepts watch-daemon freshness guesses` |
| VERIFICATION-SYSTEM-009 | `requirements/verification-system/tests/integration-entry-coverage.test.mjs::WHAT[VERIFICATION-SYSTEM-009] integration entry coverage accepts an exact reachable set`；`requirements/verification-system/tests/repository-closure-gates.test.mjs::WHAT[VERIFICATION-SYSTEM-009] repository closure gates reject a missing semantic owner and package member` |
| VERIFICATION-SYSTEM-010 | `requirements/verification-system/tests/deadcode-scan.test.mjs::WHAT[VERIFICATION-SYSTEM-010] deadcode_baseline_allows_only_existing_named_debt` |
| VERIFICATION-SYSTEM-011 | `requirements/verification-system/tests/coverage-gate.test.mjs::WHAT[VERIFICATION-SYSTEM-011] parseCoverageThreshold accepts valid positive finite numbers` |
| VERIFICATION-SYSTEM-012 | `requirements/verification-system/tests/no-line-count-check.test.mjs::WHAT[VERIFICATION-SYSTEM-012] no line-count check wording in package or gates` |
| VERIFICATION-SYSTEM-013 | `requirements/verification-system/tests/js-boundary-gate.test.mjs::WHAT[VERIFICATION-SYSTEM-013] product_semantic_debt_is_zero` |
