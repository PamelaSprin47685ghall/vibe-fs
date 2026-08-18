# HOW：verification-system 的实现模型与约束

非 normative。本文件解释机制怎么工作、历史怎么来的、什么被弃权。

## 实现模型

无 runtime 源码（META 包正确形态，见 2026-08-14 cutover 设计期 EVIDENCE §1）。证据面
分布在四个机制层：

### 1. proof ladder（`tests/proof-ladder.test.mjs`）

`node --test requirements/verification-system/tests/proof-ladder.test.mjs`

三组断言（Oracle 3，HANDOFF §29 调查结论直接执行）：

```text
1. format-build-test 层序（fantomas → check.mjs(L0) → build.mjs →
   unit/run.mjs → integration/run.mjs → integration/package/run.mjs →
   warmup-opencode.mjs → e2e/entry.test.mjs(L4，恰一个) → npm pack --dry-run(L5)）
2. check.mjs wired gate 清单：每个 wired 路径存在；
   scripts/checks/*.mjs == wired ∪ {spec-rules.mjs(lib), semantic-anchors.mjs(catalog)}
3. check.mjs fail-closed：process.exit(result.status ?? 1) 传播非零；
   行为面：必败 gate 退出码传播、不可 spawn 的 gate 判 exit 1
```

「可红」由现有 per-gate red fixture 交叉证明，不在本测试重造。`e2e-watchdog-feed.mjs`
已由 lead 接入 check.mjs（test-boundary 之后）。

### 2. layer-0 gate 回归（`tests/e2e-watchdog-feed.test.mjs`）

VERIFY-004 因果 watchdog feed 门禁的永久回归：top-level e2e 测试不得直接
`watchdog.advance(`；唯一入口 `requirements/verification-system/tests/e2e/entry.test.mjs` 必须在扫描范围内。
（自 `tests/unit/verify/` 迁移，import 深度不变。）

### 3. physical contract 声明面（`tests/physical-contract.test.mjs`）

唯一 Long Stroke 入口必须写出它依赖的不可模拟 physical contract（OpenCode lifetime /
HOST-010 messageID / Repeat-until-pass forbidden）；删声明即红。`format-build-test` 禁止
repeat-until-pass。答不出则不得留在 e2e。

### 4. 行数非门禁 absence 证明（`tests/no-line-count-check.test.mjs`）

VERIFICATION-SYSTEM-012 的机器载体：扫描本包 tests 与 `scripts/checks/*.mjs`
（本包 MECHANISM），断言不存在行数检查指纹（SOFT_LIMIT / exceeds advisory /
size-advisory / 行数）。故意不扫泛词——`lineCount` 是 diagnostics 合法字段、覆盖
门禁（VERIFY-011）按行统计合法、`kolmogorov-principles` 是产品 tool 参数。

### 5. 运行器机制（lead 集成时执行，本包 REUSE 登记）

```text
node scripts/check.mjs              # 18 个 wired layer-0 gate（proof-ladder pin 清单）
node requirements/verification-system/tests/run.mjs             # L1–3 入口：staleness gate + verdict-silence 监督
node requirements/verification-system/tests/run.mjs --coverage  # VERIFY-009 覆盖门禁（run-inner 判阈值）
tests/e2e/support/*                 # watchdog / readiness / 因果原语（VERIFY-004）
```

## 依赖与理由

- INDEX 骨架：`verification-system → requirement-system`。理由：本包命题（每 assertion
  一个 owner、WHAT 是唯一合同、依赖闭包验收）建立在 requirement-system 的元合同之上；
  没有「谁拥有什么」就无法定义「谁的 Satisfied(P) 需要什么证据」。

## 运行与验证

```text
node --test requirements/verification-system/tests/proof-ladder.test.mjs
node --test requirements/verification-system/tests/e2e-watchdog-feed.test.mjs
node --test requirements/verification-system/tests/no-line-count-check.test.mjs
```

proof-ladder 现在必须绿。全量命令由 lead 在集成时执行（不跑 `node requirements/verification-system/tests/run.mjs` /
`node scripts/check.mjs` 于本支线）。

## 历史与弃权

| 来源 | 裁决 | 记录在哪 |
|---|---|---|
| multi-canary / parallel pool / shuffle / 三轮 repeat（test.md G4R 之前形态） | GARBAGE（target-delete）：One World 取代；只作反例不成为目标 | WHAT-002/003；本 HOW |
| `tests/e2e/cases/**`（31 cases） | GARBAGE：已删除；E2E_CASE_CEILING=0 只降不升 | WHAT-002 |
| `enforcer-rulebook-gate.mjs` | retired stub（2026-08-12）：RuleBook 散文质量属编辑/判断关切，不设机械门；已删除，全仓无该文件，proof-ladder allowlist 已移除 | proof-ladder allowlist |
| g4r-freeze / student-teacher-absence | 迁移期 ratchet，已删除（2026-08-14 Wave 2b）：由 `e2e-watchdog-feed`（One World 门）与 unified-store `student-qa-revival` scanner 承接 | PROOF SPLIT@cutover |
| 旧 symbol blacklist（dsl-ownership / provider-leak） | 迁移期 ratchet（PROOF-MAP 标 DELETE）：基线稳定后弱化；不进入永久 verifier | PROOF SPLIT@cutover |
| canary-unbend / orchestrator-e2e-timeout 的具体场景修复 | 历史证据：证明「断言不可弯曲」「先可解释再修根因」有现实失败模式 | WHY 考古；WHAT-004/005 |
| waitfact-causal-renewal 的 `renewOn` 记法 | 并入 VERIFY-004 因果续期语义（WHAT-006）；具体 schema 是当前 HOW | WHAT-006 |
| fix.md 的 DSL 门禁盲区（136/245 文件） | 教训并入 WHAT-009（静态门禁命中真实路径）+ WHAT-010（验收判据不可放宽） | WHAT-009/010 |
| PROOF-MAP 顶层 3 文件归属（verdict-feed→review-judgement、domain.meta→requirement-system） | 按断言内容改判 verification-system；显式记录差异，cutover 复核 | PROOF SPLIT@cutover |
| `tests/unit|integration|e2e` 顶级目录分类 | HOW/GARBAGE：当前物理载体；cutover 后按包重组 | WHAT 边界；本 HOW |
| 当前 One Long Stroke 的 OpenCode 脚本名（warmup-opencode.mjs 等） | HOW：具体脚本名是当前载体；「恰一个 Long Stroke」原则是 WHAT-002 | WHAT-002 |

## 遗留风险 / cutover 待办

- **SPLIT@cutover**：g4r-freeze 迁移 ratchet → 永久 One World 门（已执行：`e2e-watchdog-feed`）；
  覆盖门禁 → 独立 oracle 或包内测试；PROOF-MAP 归属分歧按 assertion 复核后回写协调文件。
- 「禁止跨级」物理契约声明面：`tests/physical-contract.test.mjs` + e2e entry PHYSICAL CONTRACTS
  块（`requirements/GAP.md` GAP-006 CLOSED）。
- 本包测试均为文本/文件系统级，不依赖 dist；proof-ladder 对 package.json / check.mjs 的
  格式假设（`&&` 拼接、`const checks = [...]` 形状）若未来改格式需同步适配（属本包独立
  变化）。

## 边界（DOES NOT OWN）

- 「artifact 必须含 resources」等 distribution 产品事实 → `distribution`。
- 「prompt 不得泄漏 SessionId」等具体产品事实 → 各对应包；verification 只规定如何证明。
- 当前 `tests/unit|integration|e2e` 顶级目录分类 → 当前 HOW（迁移载体）。
- 当前 One Long Stroke 的 OpenCode 具体脚本名 → 当前 HOW。
- 「谁拥有什么」→ `requirement-system`（本包消费它的 guarantee）。

## 验证与测试落点

落点类型：`MOVE`（从 tests/unit 物理移入）/ `REUSE`（留原处，记锚点与 SPLIT@cutover）/
`NEW`（新写）。运行命令均为仓库根目录相对。每条 WHAT 命题恰一行。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| VERIFICATION-SYSTEM-001 | `requirements/verification-system/tests/proof-ladder.test.mjs`（test: VERIFY_001_format_build_test_ladder_pins_the_five_layers_in_order / VERIFY_001_checks_directory_is_wired_plus_allowlist_only） | NEW | node --test requirements/verification-system/tests/proof-ladder.test.mjs |
| VERIFICATION-SYSTEM-002 | `requirements/verification-system/tests/proof-ladder.test.mjs`（test: VERIFY_001_l4_has_exactly_one_e2e_entry_in_the_ladder）；REUSE `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（sole-entry scope 回归；g4r-freeze 迁移期 ratchet 已退休 2026-08-14）；REUSE `requirements/verification-system/tests/e2e-event-ceiling.test.mjs`（long-stroke.toml declares theoretical exact event ceilings） | NEW+REUSE | node --test requirements/verification-system/tests/proof-ladder.test.mjs |
| VERIFICATION-SYSTEM-003 | REUSE `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（case 天花板 0 的机器面：sole top-level entry、无 cases/ 通道；g4r-freeze 已退休）；REUSE `requirements/verification-system/tests/e2e-event-ceiling.test.mjs`（event 天花板精确）；NEW `tests/physical-contract.test.mjs`（唯一 Long Stroke 入口必须声明不可模拟 physical contract；format-build-test 禁止 repeat-until-pass） | REUSE+NEW | node --test requirements/verification-system/tests/physical-contract.test.mjs |
| VERIFICATION-SYSTEM-004 | `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（layer-0 gate 永久回归）；`requirements/verification-system/tests/proof-ladder.test.mjs`（fail-closed 传播）；`requirements/verification-system/tests/integration-entry-coverage.test.mjs`（受控 unwired / stale / duplicate 反例必须判红）；NEW `requirements/verification-system/tests/deadcode-scan.test.mjs`（production F# 中仓库级零引用 `let private` 反例必须判红）；交叉：`requirements/requirement-system/tests/spec-rules.test.mjs`（spec gate 可红 fixture） | MOVE+NEW | node --test requirements/verification-system/tests/e2e-watchdog-feed.test.mjs requirements/verification-system/tests/integration-entry-coverage.test.mjs requirements/verification-system/tests/deadcode-scan.test.mjs |
| VERIFICATION-SYSTEM-005 | `requirements/verification-system/tests/proof-ladder.test.mjs`（test: VERIFY_001_check_mjs_propagates_nonzero_fail_closed / VERIFY_005_fail_closed_propagates_a_failing_gate_exit_code / VERIFY_005_fail_closed_treats_an_unspawnable_gate_as_failure）；NEW `requirements/verification-system/tests/walk-fail-closed.test.mjs`（共享 walker fail-closed）；NEW `requirements/verification-system/tests/deadcode-scan.test.mjs`（missing source root 必须抛错，不能空扫变绿） | NEW | node --test requirements/verification-system/tests/proof-ladder.test.mjs requirements/verification-system/tests/walk-fail-closed.test.mjs requirements/verification-system/tests/deadcode-scan.test.mjs |
| VERIFICATION-SYSTEM-006 | `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（E2E_WATCHDOG_FEED_case_files_do_not_feed_watchdog_directly）；REUSE `requirements/verification-system/tests/verdict-feed.test.mjs`（`WHAT[VERIFICATION-SYSTEM-006] a verdict renews the silence window` / `WHAT[VERIFICATION-SYSTEM-006] bytes moving is recorded and does not renew`） | MOVE+REUSE | node --test requirements/verification-system/tests/e2e-watchdog-feed.test.mjs |
| VERIFICATION-SYSTEM-007 | REUSE `requirements/verification-system/tests/domain.meta.test.mjs`（deadline verdict does not depend on the ambient timezone）；`requirements/verification-system/tests/temporal-harness.test.mjs`（deterministic queue/clock + real Task lifecycle races：journal release drain、first-poison preservation、poisoned durable substrate rejects new reconcile admission、reconcile StopAndDrain、plugin scope background/reconcile drain、Finality reviewer abort drain）；virtual-time owner 边界仍归 `time-capability` | REUSE + NEW | node --test requirements/verification-system/tests/domain.meta.test.mjs requirements/verification-system/tests/temporal-harness.test.mjs |
| VERIFICATION-SYSTEM-008 | REUSE `requirements/verification-system/tests/guide-contract.test.mjs`（VERIFY_008_the_journal_publishes_boot_append_and_snapshot——AgentJournal 只从已 fold projection + writer 构造，EventStore boot/resume 属 EventStoreJournalWriter；retired EventStore-boot forwarding facade 不得回归 / VERIFY_008_the_published_plugin_entrypoint_loads / VERIFY_008_every_emitted_module_actually_loads / VERIFY_008_the_contract_and_the_facade_read_the_same_build）；REUSE `requirements/verification-system/tests/domain.meta.test.mjs`（owner-surface 契约元测试：utcOffset / deadline comparisons / journal codec / context fold / fallback cursor）；REUSE `requirements/verification-system/tests/run.mjs`（staleness gate——陈旧产物 fail closed） | REUSE | node --test requirements/verification-system/tests/guide-contract.test.mjs |
| VERIFICATION-SYSTEM-009 | `requirements/verification-system/tests/proof-ladder.test.mjs`（test: VERIFY_001_every_wired_gate_path_exists / VERIFY_001_every_ladder_step_target_exists）；REUSE `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（WHAT[VERIFICATION-SYSTEM-009] missing/non-directory e2e root fails closed — a gate whose path points at a non-existent directory must not be always-passing）；`requirements/verification-system/tests/integration-entry-coverage.test.mjs`（integration discovery 与统一入口精确覆盖；distribution package 子套件由共享 `tests/support/discover-suite-tests.mjs` 确定性发现，parent 与 child 同源——child `run.mjs` 执行该集合、parent 以精确 `childOwnedTests` 委托，prefix 不再作为所有权判据；行为测试：真实目录发现 / 新增自动纳入 / runner 与非测试文件排除 / 不可读目录 fail-closed / parent==child 无漂移 / 真实仓库入口覆盖全绿 / 未声明 child 测试判红） | NEW | node --test requirements/verification-system/tests/proof-ladder.test.mjs requirements/verification-system/tests/integration-entry-coverage.test.mjs |
| VERIFICATION-SYSTEM-010 | REUSE `requirements/verification-system/tests/proof-ladder.test.mjs`（层序与 sole-entry pin；g4r-freeze case-ceiling ratchet 已退休 2026-08-14，断言强度不缩水）；NEW `requirements/verification-system/tests/deadcode-scan.test.mjs`（tracked `file::binding` baseline 只允许既有债，新增项立即 regression） | REUSE+NEW | node --test requirements/verification-system/tests/proof-ladder.test.mjs requirements/verification-system/tests/deadcode-scan.test.mjs |
| VERIFICATION-SYSTEM-011 | REUSE `requirements/verification-system/tests/run.mjs`（--coverage 阈值门禁，run-inner COVERAGE_LINE_THRESHOLD）；NEW `requirements/verification-system/tests/support/coverage-policy.mjs`（纯函数 helper：parseCoverageThreshold / selectProductionModules / preImportModules / evaluateCoverage / COVERAGE_EXCLUDE_GLOBS，run-inner.mjs 导入调用）；NEW `requirements/verification-system/tests/coverage-gate.test.mjs`（WHAT[VERIFICATION-SYSTEM-011] 行为测试：阈值解析合法/非法、fable_modules 排除、预导入失败计数 + 全通过、阈值达标/未达标/无事件、排除项固定值；补充静态断言 runner 接入 helper）；SPLIT@cutover：已完成，见下方清单 | REUSE + NEW | node --test requirements/verification-system/tests/coverage-gate.test.mjs |
| VERIFICATION-SYSTEM-012 | `requirements/verification-system/tests/no-line-count-check.test.mjs`（结构性 absence：本包 tests 与 scripts/checks 内无行数检查指纹 SOFT_LIMIT / exceeds advisory / size-advisory / 行数） | NEW | node --test requirements/verification-system/tests/no-line-count-check.test.mjs |
| VERIFICATION-SYSTEM-013 | `requirements/verification-system/tests/js-boundary-gate.test.mjs`（test: WHAT[VERIFICATION-SYSTEM-013] product_semantic_debt_is_zero / boundary_gate_passes_at_terminal_state / no_package_local_contract_adapters / exemptions_are_only_compiler_distribution_or_host_canary / no_interop_or_domain_facade_imports / surface_manifest_is_nonempty_and_closed） | NEW | node --test requirements/verification-system/tests/js-boundary-gate.test.mjs |

### 语义 anchor

`scripts/checks/semantic-anchors.mjs` 是角色/工具语义锚点 catalog（归属各产品包）。本包是
META 包，**无 anchor id**；本包的机器事实由 proof-ladder + watchdog-feed 承担。

### SPLIT@cutover 清单

- `g4r-freeze.mjs`（+ 其回归）迁移期 One World ratchet，已退休删除（2026-08-14 Wave 2b）；
  由永久 One World 门 `e2e-watchdog-feed` + proof-ladder sole-entry pin 承接，断言强度不缩水（只收紧）。
- 覆盖门禁（VERIFY-011）：当前载体 `requirements/verification-system/tests/run.mjs --coverage`（MECHANISM）；cutover
  已完成——覆盖策略提取为纯函数 helper `tests/support/coverage-policy.mjs`（run-inner.mjs 导入调用），`coverage-gate.test.mjs`
  以行为测试行使预导入失败 / 阈值即红 / 无豁免 / 排除项固定，不再以源码正则为唯一证据。
- **PROOF-MAP 归属分歧（cutover 按 assertion 复核）**：`requirements/verification-system/tests/verdict-feed.test.mjs`
  （VERIFY-004 watchdog 分类器）、`requirements/verification-system/tests/domain.meta.test.mjs`（VERIFY-008 owner-surface
  契约元测试）、`requirements/verification-system/tests/guide-contract.test.mjs`（VERIFY-005/008 契约面）按内容属
  verification-system；PROOF-MAP 曾将 verdict-feed 标 review-judgement、domain.meta 标
  requirement-system，本包以断言内容为准并在此显式记录差异。
- 语义分支「禁止直跳 E2E」：唯一入口必须声明不可模拟 physical contract
  （`tests/physical-contract.test.mjs` + `tests/e2e/entry.test.mjs` PHYSICAL CONTRACTS 块）；
  聚合台账见 `requirements/GAP.md` GAP-006 CLOSED。
