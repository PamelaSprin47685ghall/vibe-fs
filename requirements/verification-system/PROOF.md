# PROOF：verification-system 测试落点表

落点类型：`MOVE`（从 tests/unit 物理移入）/ `REUSE`（留原处，记锚点与 SPLIT@cutover）/
`NEW`（新写）。运行命令均为仓库根目录相对。每条 WHAT 命题恰一行。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| VERIFICATION-SYSTEM-001 | `requirements/verification-system/tests/proof-ladder.test.mjs`（test: VERIFY_001_format_build_test_ladder_pins_the_five_layers_in_order / VERIFY_001_checks_directory_is_wired_plus_allowlist_only） | NEW | node --test requirements/verification-system/tests/proof-ladder.test.mjs |
| VERIFICATION-SYSTEM-002 | `requirements/verification-system/tests/proof-ladder.test.mjs`（test: VERIFY_001_l4_has_exactly_one_e2e_entry_in_the_ladder）；REUSE `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（sole-entry scope 回归；g4r-freeze 迁移期 ratchet 已退休 2026-08-14）；REUSE `requirements/verification-system/tests/e2e-event-ceiling.test.mjs`（long-stroke.toml declares theoretical exact event ceilings） | NEW+REUSE | node --test requirements/verification-system/tests/proof-ladder.test.mjs |
| VERIFICATION-SYSTEM-003 | REUSE `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（case 天花板 0 的机器面：sole top-level entry、无 cases/ 通道；g4r-freeze 已退休）；REUSE `requirements/verification-system/tests/e2e-event-ceiling.test.mjs`（event 天花板精确）；「物理契约论证」人工裁决面由 VERIFY-002 文本 + review 承接 | REUSE | node --test requirements/verification-system/tests/e2e-watchdog-feed.test.mjs |
| VERIFICATION-SYSTEM-004 | `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（layer-0 gate 永久回归）；`requirements/verification-system/tests/proof-ladder.test.mjs`（fail-closed 传播）；交叉：`requirements/requirement-system/tests/spec-rules.test.mjs`（spec gate 可红 fixture） | MOVE+NEW | node --test requirements/verification-system/tests/e2e-watchdog-feed.test.mjs |
| VERIFICATION-SYSTEM-005 | `requirements/verification-system/tests/proof-ladder.test.mjs`（test: VERIFY_001_check_mjs_propagates_nonzero_fail_closed / VERIFY_005_fail_closed_propagates_a_failing_gate_exit_code / VERIFY_005_fail_closed_treats_an_unspawnable_gate_as_failure） | NEW | node --test requirements/verification-system/tests/proof-ladder.test.mjs |
| VERIFICATION-SYSTEM-006 | `requirements/verification-system/tests/e2e-watchdog-feed.test.mjs`（E2E_WATCHDOG_FEED_case_files_do_not_feed_watchdog_directly）；REUSE `requirements/verification-system/tests/verdict-feed.test.mjs`（VERIFY_004_a_verdict_renews_the_silence_window / VERIFY_004_bytes_moving_is_recorded_and_does_not_renew） | MOVE+REUSE | node --test requirements/verification-system/tests/e2e-watchdog-feed.test.mjs |
| VERIFICATION-SYSTEM-007 | REUSE `requirements/verification-system/tests/domain.meta.test.mjs`（deadline verdict does not depend on the ambient timezone）；temporal 层 virtual-time 断言归 `time-capability`（tests/unit/temporal/**，REUSE 边界注） | REUSE | node --test requirements/verification-system/tests/domain.meta.test.mjs |
| VERIFICATION-SYSTEM-008 | REUSE `requirements/verification-system/tests/guide-contract.test.mjs`（VERIFY_008_the_published_plugin_entrypoint_loads / VERIFY_008_every_emitted_module_actually_loads / VERIFY_008_the_contract_and_the_facade_read_the_same_build）；REUSE `requirements/verification-system/tests/domain.meta.test.mjs`（facade 元测试：utcOffset / deadline comparisons）；REUSE `requirements/verification-system/tests/run.mjs`（staleness gate——陈旧产物 fail closed） | REUSE | node --test requirements/verification-system/tests/guide-contract.test.mjs |
| VERIFICATION-SYSTEM-009 | `requirements/verification-system/tests/proof-ladder.test.mjs`（test: VERIFY_001_every_wired_gate_path_exists / VERIFY_001_every_ladder_step_target_exists） | NEW | node --test requirements/verification-system/tests/proof-ladder.test.mjs |
| VERIFICATION-SYSTEM-010 | REUSE `requirements/verification-system/tests/proof-ladder.test.mjs`（层序与 sole-entry pin；g4r-freeze case-ceiling ratchet 已退休 2026-08-14，断言强度不缩水） | REUSE | node --test requirements/verification-system/tests/proof-ladder.test.mjs |
| VERIFICATION-SYSTEM-011 | REUSE `requirements/verification-system/tests/run.mjs`（--coverage 阈值门禁，run-inner COVERAGE_LINE_THRESHOLD）；SPLIT@cutover：覆盖门禁拆分计划见 HOW.md | REUSE | node requirements/verification-system/tests/run.mjs --coverage |
| VERIFICATION-SYSTEM-012 | `requirements/verification-system/tests/kolmogorov-size-advisory.test.mjs`（kolmogorov size over advisory limit never blocks / kolmogorov growth beyond baseline is suggestion not ratchet failure） | MOVE | node --test requirements/verification-system/tests/kolmogorov-size-advisory.test.mjs |

## 语义 anchor

`scripts/checks/semantic-anchors.mjs` 是角色/工具语义锚点 catalog（归属各产品包）。本包是
META 包，**无 anchor id**；本包的机器事实由 proof-ladder + watchdog-feed + kolmogorov
advisory 承担。

## SPLIT@cutover 清单

- `g4r-freeze.mjs`（+ 其回归）迁移期 One World ratchet，已退休删除（2026-08-14 Wave 2b）；
  由永久 One World 门 `e2e-watchdog-feed` + proof-ladder sole-entry pin 承接，断言强度不缩水（只收紧）。
- 覆盖门禁（VERIFY-011）：当前载体 `requirements/verification-system/tests/run.mjs --coverage`（MECHANISM）；cutover
  后按「分母完整 + 阈值即红 + 无豁免」重写为可独立单跑的 oracle 或包内测试。
- **PROOF-MAP 归属分歧（cutover 按 assertion 复核）**：`requirements/verification-system/tests/verdict-feed.test.mjs`
  （VERIFY-004 watchdog 分类器）、`requirements/verification-system/tests/domain.meta.test.mjs`（VERIFY-008 facade
  元测试）、`requirements/verification-system/tests/guide-contract.test.mjs`（VERIFY-005/008 契约面）按内容属
  verification-system；PROOF-MAP 曾将 verdict-feed 标 review-judgement、domain.meta 标
  requirement-system，本包以断言内容为准并在此显式记录差异。
- 语义分支「禁止直跳 E2E」的人工裁决面：VERIFY-002 文本 + review 过程，无机器落点
  （GAP@cutover 若需机器化再补；聚合台账见 `requirements/GAP.md` GAP-006）。
