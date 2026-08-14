# PROOF — 测试落点表

> 每条 WHAT 命题恰好一行落点。类型：`NEW`（本包新写）/ `REUSE`（现有测试留在原处，integration 本轮不迁）/ `MOVE`（物理移入）。
> 单跑命令以 `node --test <file>` 为准；全套 `node requirements/verification-system/tests/run.mjs`；L0 门 `node scripts/check.mjs`。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| `DISTRIBUTION-001` | `requirements/distribution/tests/integration/package/contents.test.mjs` → `PACKAGE_contents_tarball_includes_manifest_dist_resources`；`requirements/distribution/tests/integration/package/install.test.mjs` → `PACKAGE_install_layout_matches_manifest_and_main` | REUSE | `node --test requirements/distribution/tests/integration/package/contents.test.mjs requirements/distribution/tests/integration/package/install.test.mjs` |
| `DISTRIBUTION-002` | `requirements/distribution/tests/cwd-independent-resources.test.mjs` → 三个 test（`DISTRIBUTION_resource_reads_resolve_under_package_root_regardless_of_cwd` / `DISTRIBUTION_fresh_process_with_foreign_cwd_imports_entry_and_reads_resources` / `DISTRIBUTION_lookup_is_single_fixed_relative_path_not_candidate_search`）；交叉 `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs` → `ENFORCER_resource_rulebook_load_independent_of_process_cwd` | NEW + REUSE | `node --test requirements/distribution/tests/cwd-independent-resources.test.mjs` |
| `DISTRIBUTION-003` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_manifest_entry_matches_exports_and_shipped_path`；交叉 `requirements/distribution/tests/integration/package/install.test.mjs`（main 存在）+ `requirements/distribution/tests/integration/package/import.test.mjs` → `PACKAGE_import_wanxiangshu_main_exits_zero` | NEW + REUSE | `node --test requirements/distribution/tests/pack-closure.test.mjs` |
| `DISTRIBUTION-004` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_files_whitelist_is_explicit_and_excludes_dev_test_legacy`；交叉 `requirements/distribution/tests/integration/package/contents.test.mjs` → `PACKAGE_contents_tarball_excludes_source_tests_docs_scripts` | NEW + REUSE | `node --test requirements/distribution/tests/pack-closure.test.mjs` |
| `DISTRIBUTION-005` | `requirements/distribution/tests/cwd-independent-resources.test.mjs` → `DISTRIBUTION_lookup_is_single_fixed_relative_path_not_candidate_search`（资源只解析到包根、无 dist/src fallback）；`requirements/verification-system/tests/run.mjs` fresh-dist staleness gate（runner 机制，`dist/` 陈旧拒绝运行）；`scripts/build.mjs`（先清空再编译、不复制 resources） | NEW + REUSE | `node --test requirements/distribution/tests/cwd-independent-resources.test.mjs`；`node requirements/verification-system/tests/run.mjs`；`node scripts/build.mjs` |
| `DISTRIBUTION-006` | `scripts/checks/architecture.mjs` 门 ⑥ `resource-boundary`（`PackageResources.` 引用仅限 `Infrastructure/Resources/`）；交叉 `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs` → `ENFORCER_resource_missing_package_path_throws`（缺失抛 `package resource missing`）+ `ENFORCER_resource_catalog_json_is_not_runtime_ssot` | REUSE | `node scripts/checks/architecture.mjs`；`node --test requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs` |
| `DISTRIBUTION-007` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_release_proof_covers_build_package_packing_and_artifact_checks`（本地 pin：`format-build-test` 含 build + `integration/package/run.mjs` + 末级 `npm pack --dry-run`）；发布手动关：`npm run format-build-test` 末级 `npm pack --dry-run`（实际 tarball membership 清单） | NEW + REUSE（发布关） | `node --test requirements/distribution/tests/pack-closure.test.mjs`；`npm pack --dry-run` |
| `DISTRIBUTION-008` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_enforcer_rulebook_closure_is_complete`（`resources/enforcer/**` → `behavior-diagnosis`）+ `DISTRIBUTION_provider_resource_closure_is_language_complete`（`resources/provider/**` → 各 provider 语义包）；交叉 `requirements/distribution/tests/integration/package/resources.test.mjs` → `PACKAGE_resources_provider_role_laws_and_rulebook_present_after_install` + `PACKAGE_resources_fixed_relative_path_from_PackageResources_module` | NEW + REUSE | `node --test requirements/distribution/tests/pack-closure.test.mjs` |

## 统计

```text
WHAT 命题：8（DISTRIBUTION-001..008）
落点：   NEW  7（cwd-independent-resources ×3 + pack-closure ×4 个 test 块，覆盖 002/003/004/005/007/008）
        REUSE 11（integration/package ×4、integration/resources ×2 文件、unit/run.mjs staleness、architecture.mjs 门 ⑥、build.mjs、npm pack --dry-run 发布关）
        MOVE  0（integration 本轮不迁）
GAP：    0
```

## semantic anchor 归属（semantic-anchors.mjs）

本包在 `scripts/checks/semantic-anchors.mjs` 中 **拥有 0 个 anchor id**。该 catalog（`ROLE_SEMANTIC_ANCHORS` / `TOOL_DESCRIPTION_ANCHORS` / `OFFICE_CAPABILITY_ANCHORS`）的逐 ID owner 是各 provider 语义包：`provider-language`（Gate C 结构/双语）、`office-capability`（Gate F 后果）、`action-affordance`（工具描述）、`cognitive-environment`、`behavior-diagnosis`（tip 散文）等。distribution 的 proof 面是 pack-level 结构 oracle（本表 NEW 测试 + integration/package），不是 prompt 锚点正则——anchor 锁散文内容，本包锁「散文在 artifact 里」。

## SPLIT@cutover 计划（现有测试的 owner 拆分）

| 现有文件 | 当前 owner 混合 | cutover 动作 |
|---|---|---|
| ~~`tests/integration/package/{contents,install,import,resources}.test.mjs`~~（4） | 全部断言归 distribution（PROOF-MAP：integration/package → KEEP distribution） | 已整族迁入本包 `tests/integration/package/`（Wave 2b） **MOVE** 入 `requirements/distribution/tests/`（只 import node: 内置，深度适配零成本），test 名 `PACKAGE_*` 保持为锚点 |
| `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs` | distribution（cwd 独立、路径存在、missing throws）+ `behavior-diagnosis`（rulebook 内容/ordinal/zh 撰写） | **SPLIT**：distribution 侧断言（`ENFORCER_resource_rulebook_load_independent_of_process_cwd`、`ENFORCER_resource_missing_package_path_throws`、`ENFORCER_resource_catalog_json_is_not_runtime_ssot`、路径存在性）移入本包；rulebook 语义断言移入 `behavior-diagnosis` |
| `requirements/cognitive-environment/tests/integration/resources/prompts.test.mjs` | distribution（`PROMPT_resources_load_from_package_independent_of_cwd`、路径存在）+ `provider-language`（双语/zh-authored/parity）+ `cognitive-environment`（组合序） | **SPLIT**：cwd 独立断言移入本包；parity/composition 归 `provider-language`/`cognitive-environment` |
| ~~`tests/unit/resources/prompt-semantic-depth.test.mjs`~~ | 无 distribution 断言（读 Role Law 内容锚点，owner = `provider-language`/`office-capability`/`action-affordance`/`cognitive-environment`） | **SPLIT**：全部断言归各语义包；distribution 不取（路径存在只是读取的附带条件） |

## 红意味着什么（每类落点）

- **NEW cwd 测试红**：资源查找开始依赖 cwd / 出现候选搜索 / 回退副本 → 安装后行为与仓库不一致（DISTRIBUTION-002 世界破坏）。
- **NEW pack-closure 测试红**：`main`/`exports` 漂移、whitelist 混入开发资产、某语义包新增资源目录没进闭包、`format-build-test` 丢失 build/package/packing 阶段 → 发布物缺代码/资源、泄源码或 release proof 不再把关（DISTRIBUTION-003/004/007/008）。
- **REUSE integration 红**：tarball 内容假设被破坏（`files` 改、legacy 路径回潮、`catalog.json` 复活）。
- **architecture.mjs 门 ⑥ 红**：资源读取散落到 `Infrastructure/Resources/` 之外。
- **run.mjs staleness 红**：`dist/` 陈旧——测试将跑在过期字节上，拒绝运行。
- **发布关（`npm pack --dry-run`）红**：真实 tarball membership 与预期不符（缺 dist/resources 或混入开发资产）——release closure 未通过。
