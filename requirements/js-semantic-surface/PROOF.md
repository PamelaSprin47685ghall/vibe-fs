# PROOF：js-semantic-surface 测试落点表

落点类型：`NEW`（本包 tests/）/ `GATE`（静态门禁，`node scripts/check.mjs` 集成执行）/
`PENDING`（由后续 P 阶段 gate 落地，见 HOW）。运行命令均为仓库根目录相对。每条 WHAT 命题恰一行。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| JS-SEMANTIC-SURFACE-001 | `requirements/js-semantic-surface/tests/surface-charter.test.mjs`（test: WHAT[JS-SEMANTIC-SURFACE-001] JS_SURFACE_001_all_semantic_tests_are_mjs） | NEW | node --test requirements/js-semantic-surface/tests/surface-charter.test.mjs |
| JS-SEMANTIC-SURFACE-002 | `surface-charter.test.mjs`（tests: WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002_forbidden_patterns_absent_from_semantic_tests、JS_SURFACE_002c_whole_semantic_test_zone_is_scanned、JS_SURFACE_002d_zero-debt_generate_removes_empty_ledger、JS_SURFACE_002e_build-verification_ledger_exemption_survives_zero-debt_cleanup、JS_SURFACE_002b_registered_surfaces_exist_in_the_production_source_tree）；`scripts/checks/js-boundary-gate.mjs`（whole-zone `.mjs`/`.js` ratchet, no silent regeneration, zero-debt ledger terminal） | NEW + GATE | node --test ... / node scripts/check.mjs |
| JS-SEMANTIC-SURFACE-003 | `surface-charter.test.mjs`（tests: WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_law_owner_surface_registry、JS_SURFACE_003_every_registered_surface_has_a_contract_test）；`scripts/checks/js-surface-manifest.mjs`（owner WHAT/PROOF/source/Compile/emitted dist/active WHAT-authorized import evidence） | NEW + GATE | node --test ... / node scripts/check.mjs |
| JS-SEMANTIC-SURFACE-004 | `surface-charter.test.mjs`（test: WHAT[JS-SEMANTIC-SURFACE-004] JS_SURFACE_004_helper_not_directly_tested）；`semanticImportEdges` scans every package test dependency, including support/fixtures/e2e/integration | NEW + GATE | node --test requirements/js-semantic-surface/tests/surface-charter.test.mjs |
| JS-SEMANTIC-SURFACE-005 | P5 `requirements/verification-system/tests/support/js-contract.mjs`（`assertJsData` / `assertOpaque` validator）+ `surface-charter.test.mjs`（test: WHAT[JS-SEMANTIC-SURFACE-005] JS_SURFACE_005_js_native_representation_rules） | NEW | node --test requirements/js-semantic-surface/tests/surface-charter.test.mjs |
| JS-SEMANTIC-SURFACE-006 | `surface-charter.test.mjs`（test: WHAT[JS-SEMANTIC-SURFACE-006] JS_SURFACE_006_fable_representation_not_contract）；P2 gate scans Fable tokens and permits an absent baseline only at zero; absent `domain.meta` is a terminal cleanup, not a required file | NEW + GATE | node --test ... / node scripts/check.mjs |

## 语义 anchor

无 anchor id（META 包）；机器事实由 surface-charter + js-boundary-gate + js-contract 承担。

## 人工评审承接表

| 检查 | 失败含义（对应条款） |
|---|---|
| 新增 semantic surface 但无 contract test pin 名字 | JS-SEMANTIC-SURFACE-003 |
| 「测试需要」成为 export internal 的理由 | JS-SEMANTIC-SURFACE-002 |
| surface 翻译在 owner boundary 之外（中央 god facade） | JS-SEMANTIC-SURFACE-003 |
| Fable 升级破坏 semantic tests（quarantine 外） | JS-SEMANTIC-SURFACE-006 |
