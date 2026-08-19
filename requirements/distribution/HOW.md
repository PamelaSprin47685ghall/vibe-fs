# HOW — 实现模型与约束（非 normative）

> 本文件描述当前实现长什么样、为什么长这样；不另造 normative owner。命题以 `WHAT.md` 为准。

## 1. artifact 形态（package.json）

```json
{
  "name": "wanxiangshu",
  "main": "./dist/OpenCode/Plugin/Plugin.js",
  "exports": { ".": "./dist/OpenCode/Plugin/Plugin.js" },
  "files": ["dist/", "resources/"],
  "engines": { "node": ">=20" },
  "peerDependencies": { "@opencode-ai/plugin": ">=1.17.4" }
}
```

- `main` == `exports["."]` == 同一 entry（DISTRIBUTION-003）。
- `files` 是唯一打包 whitelist：`dist/`（编译代码）+ `resources/`（runtime semantic resources）；`package.json`/`README.md`/`LICENSE` 由 npm 自动带进 tarball。**无** `.npmignore`。
- 私有不公开：`private: true`、`publishConfig.access: restricted`、`license: SEE LICENSE IN LICENSE`；从 tarball 或私有 registry 安装（`README.md` §获取与安装）。
- 用户可读安装说明见 `README.md`（`npm install ./wanxiangshu-0.8.4.tgz`；OpenCode 配置按包名或 `main` 挂载；启动时资源缺失/非法 → 启动失败，无代码内置副本兜底）。

## 2. 构建（scripts/build.mjs）

```text
rm -rf dist/
dotnet tool run fable precompile src/Wanxiangshu/Wanxiangshu.fsproj -o dist
   → 递归删除 dist/ 内的 .gitignore 与 .fs/.fsproj 残留（防源文件混入产物）
   → 校验：
       dist/OpenCode/Plugin/Plugin.js 存在（entry）
       resources/enforcer/ 至少一个 tip 目录，且 <tip>/{enforcer.md,main.md} 存在
       resources/enforcer/catalog.json 不存在（已废止）
       resources/provider/role/<11 角色>/{en,zh-CN}.md 存在
       resources/provider/{world/common-law,library/ingress,library/closing}/{en,zh-CN}.md 存在
       dist/Sphinx/McpServer.js 存在
```

- 先清空再编译：`dist/` 不留上次构建的旧字节（DISTRIBUTION-005）。
- 不把 `resources/` 复制进 `dist/`：单份发布，杜绝双副本漂移（历史 why/enforcer 分发裁决）。
- build 内嵌的 resources 校验是**编译期 closure 前置**：坏资源在 build 就红，不等到 pack/install。

## 3. 资源加载（Infrastructure/Resources/）

```text
src/Wanxiangshu/Infrastructure/Resources/
  PackageResources.fs         fixed package-relative read（import.meta.url → ../../../resources）
  ProviderResources.fs        语言对（en/zh-CN）requireLanguagePair / readText / exists
  PromptResources.fs          ProviderLanguage → PromptCatalog 组合（Common Law → Role Law → Library）
  EnforcerCatalogResource.fs  目录扫描 rulebook tips（enforcer.md + main.md），无 catalog.json
  RuntimeResources.fs         plugin init 时 load() → install() 一次；current() 只读
```

- `PackageResources.readText(rel)`：`dirname(fileURLToPath(import.meta.url))` 上溯 3 层到包根 → `join(packageRoot, "resources", rel)` → `existsSync` 失败抛 `package resource missing: <full>`。**无 cwd walk、无候选搜索、无 dist/src fallback**（DISTRIBUTION-002/006）。
- 编译后模块位于 `dist/Infrastructure/Resources/`，`../../../resources` 恰好落在包根 `resources/`（`requirements/distribution/tests/integration/package/resources.test.mjs` 与本包 NEW oracle 双面锁）。
- `RuntimeResources.install` 是唯一安装点：plugin 构造器先 `load()` 再 `install()`，任何 consumer 运行前资源已就位；未安装即 `current()` 抛错（fail fast）。
- 资源读取只允许出现在 `Infrastructure/Resources/`：`scripts/checks/architecture.mjs` 门 ⑥ `resource-boundary`（`PACKAGE_RESOURCE_READ` 扫描，`src/Wanxiangshu/` 内 `PackageResources.` 引用只能出现在该目录）。

## 4. fresh-dist 门（测试消费发布字节）

`requirements/verification-system/tests/run.mjs` staleness gate：`dist/**`（除 `fable_modules/`）最新产物必须不早于任何 `src/Wanxiangshu/**/*.fs`/`.fsproj` 源，否则拒绝运行（`dist/ is stale by Ns — run: npm run format-build-test`）。这是「测试与发布消费同一份编译字节」的执行时保证（DISTRIBUTION-005）；机制实现属 `verification-system` 层（runner harness），命题归本包。

## 5. release proof（L5）

`package.json` `format-build-test` 全链：

```text
fantomas → scripts/check.mjs（L0 静态门）
        → scripts/build.mjs（编译）
        → requirements/verification-system/tests/run.mjs → requirements/verification-system/tests/integration/run.mjs → requirements/distribution/tests/integration/package/run.mjs
        → scripts/warmup-opencode.mjs → requirements/verification-system/tests/e2e/entry.test.mjs（Long Stroke）
        → npm pack --dry-run（L5：真实 tarball membership 清单）
```

- `npm pack --dry-run` 输出实际将被打包的文件清单（206KB JSON），是 artifact closure 的**终极 membership oracle**：integration/unit 层只做静态前置，真实 membership 以 pack 清单为准（DISTRIBUTION-007）。
- 阶梯分层/晋级纪律/No-Go 红线（`repeat-until-pass` 永久禁止）归 `verification-system`（VERIFICATION-SYSTEM-001/003/010）。
- 发布流程：`npm ci` → `dotnet tool restore` → `npm run format-build-test` → `npm pack --pack-destination artifacts/package`；Git 工作树须干净（`README.md` §发布）。

## 6. 依赖（DEPENDS ON — 特殊 edge）

`requirements/INDEX.md` 依赖骨架末行：

```text
distribution → 特殊：所有声明 runtime resource 的 semantic packages（不获其语义 ownership）
```

逐条理由（当前资源树 → 声明包 → 一句话理由）：

| 资源子树 | 声明它的 semantic package | 理由 |
|---|---|---|
| `resources/enforcer/<TipName>/{enforcer.md,main.md}`（121 tip 目录） | `behavior-diagnosis` | tip = diagnosis 检测边界（ENFORCER-001 等）；distribution 保证 rulebook 目录随 artifact 完整可得。 |
| `resources/provider/role/<11 角色>/{en,zh-CN}.md` | `office-capability`、`provider-language`、`cognitive-environment` | Role Law 定义 office 后果、语言契约与认知层；双语对必须随包。 |
| `resources/provider/world/common-law`、`library/{ingress,closing,kolmogorov,scarcity,reviewer}` | `cognitive-environment` | Common Law / Office Library 是长期知识组织层。 |
| `resources/provider/tool/**`（30+ 目录） | `action-affordance`（+ `capability-enforcement` 交叉） | 工具 description 是调用瞬间 act contract。 |
| `resources/provider/delegation/**` | `delegation` | fork/commission/sync 委托散文。 |
| `resources/provider/lifecycle/**` | `managed-session-lifecycle`、`obligation-ledger`、`review-assurance`、`finality` 等 | lifecycle/finality/magic-todo 散文。 |
| `resources/provider/review/challenge` | `review-judgement` | challenge 散文。 |
| `resources/provider/host/pair-programming-guideline` | `cognitive-environment`（+ `interaction-authority` 交叉） | pair hint 正文属认知环境。 |
| `resources/provider/runtime/**` | `interaction-authority`、`dispatch-protocol`、`degeneration-guard` 等 | runtime 续期/重试/loop 散文。 |

本包只依赖「上述资源**在 artifact 中可得**」，不获得任何一行的语义 ownership。语义包负责内容正确性（Gate C 双语锚点、rulebook 契约、office 后果等）。

## 7. 历史与弃权

迁移历史由 Git 保存；当前实现边界见 §1–§6，当前 proof 见下方落点表。

## 8. 已知 GAP 与 cutover 待办

- **tarball membership 只由 release proof 验**：unit/integration 层不 spawn `npm pack`（与 `requirements/distribution/tests/integration/package/*` 头注释同一设计决定：测试里跑 npm 受 3s watchdog 约束且慢）。发布依赖 `format-build-test` L5 `npm pack --dry-run` 把关。若未来要把它收进自动化 suite，需独立 runner（非 unit watchdog 约束），见 `HOW.md` SPLIT@cutover。
- integration 层 4 文件 + 2 资源文件 + 1 unit 资源文件的 owner 拆分计划见 `HOW.md` §SPLIT@cutover。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`NEW`（本包新写）/ `REUSE`（现有测试留在原处，integration 本轮不迁）/ `MOVE`（物理移入）。
> 单跑命令以 `node --test <file>` 为准；全套 `node requirements/verification-system/tests/run.mjs`；L0 门 `node scripts/check.mjs`。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| `DISTRIBUTION-001` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_artifact_carries_compiled_code_and_runtime_resources_together`（编译入口 + runtime semantic resources 同一 files 白名单携带）；交叉 `requirements/distribution/tests/integration/package/contents.test.mjs` → `PACKAGE_contents_tarball_includes_manifest_dist_resources`；`requirements/distribution/tests/integration/package/install.test.mjs` → `PACKAGE_install_layout_matches_manifest_and_main` | NEW + REUSE | `node --test requirements/distribution/tests/pack-closure.test.mjs` |
| `DISTRIBUTION-002` | `requirements/distribution/tests/cwd-independent-resources.test.mjs` → 两个 test（`DISTRIBUTION_resource_reads_resolve_under_package_root_regardless_of_cwd` / `DISTRIBUTION_fresh_process_with_foreign_cwd_imports_entry_and_reads_resources`）；交叉 `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs` → `ENFORCER_resource_rulebook_load_independent_of_process_cwd` | NEW + REUSE | `node --test requirements/distribution/tests/cwd-independent-resources.test.mjs` |
| `DISTRIBUTION-003` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_manifest_entry_matches_exports_and_shipped_path`；交叉 `requirements/distribution/tests/integration/package/install.test.mjs`（main 存在）+ `requirements/distribution/tests/integration/package/import.test.mjs` → `PACKAGE_import_wanxiangshu_main_exits_zero` | NEW + REUSE | `node --test requirements/distribution/tests/pack-closure.test.mjs` |
| `DISTRIBUTION-004` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_files_whitelist_is_explicit_and_excludes_dev_test_legacy`；交叉 `requirements/distribution/tests/integration/package/contents.test.mjs` → `PACKAGE_contents_tarball_excludes_source_tests_docs_scripts` | NEW + REUSE | `node --test requirements/distribution/tests/pack-closure.test.mjs` |
| `DISTRIBUTION-005` | `requirements/distribution/tests/cwd-independent-resources.test.mjs` → `DISTRIBUTION_lookup_is_single_fixed_relative_path_not_candidate_search`（资源只解析到包根、无 dist 双副本、无 dist/src fallback）；`requirements/verification-system/tests/run.mjs` fresh-dist staleness gate（runner 机制，`dist/` 陈旧拒绝运行）；`scripts/build.mjs`（先清空再编译、不复制 resources） | NEW + REUSE | `node --test requirements/distribution/tests/cwd-independent-resources.test.mjs`；`node requirements/verification-system/tests/run.mjs`；`node scripts/build.mjs` |
| `DISTRIBUTION-006` | `requirements/distribution/tests/cwd-independent-resources.test.mjs` → `DISTRIBUTION_resource_missing_fails_fast_no_fallback`（缺失抛 `package resource missing`、`catalog.json` 不存在）；`requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_resource_io_lives_only_under_infrastructure_resources`（`PackageResources.` 引用仅限 `src/Wanxiangshu/Resources/`，镜像 architecture.mjs 门 ⑥）；`scripts/checks/architecture.mjs` 门 ⑥ `resource-boundary`；交叉 `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs` → `ENFORCER_resource_missing_package_path_throws` + `ENFORCER_resource_catalog_json_is_not_runtime_ssot` | NEW + REUSE | `node --test requirements/distribution/tests/cwd-independent-resources.test.mjs`；`node --test requirements/distribution/tests/pack-closure.test.mjs`；`node scripts/checks/architecture.mjs` |
| `DISTRIBUTION-007` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_release_proof_covers_build_package_packing_and_artifact_checks`（本地 pin：`format-build-test` 含 build + `integration/package/run.mjs` + 末级 `npm pack --dry-run`）；发布手动关：`npm run format-build-test` 末级 `npm pack --dry-run`（实际 tarball membership 清单） | NEW + REUSE（发布关） | `node --test requirements/distribution/tests/pack-closure.test.mjs`；`npm pack --dry-run` |
| `DISTRIBUTION-008` | `requirements/distribution/tests/pack-closure.test.mjs` → `DISTRIBUTION_enforcer_rulebook_closure_is_complete`（`resources/enforcer/**` → `behavior-diagnosis`）+ `DISTRIBUTION_provider_resource_closure_is_language_complete`（`resources/provider/**` → 各 provider 语义包）；交叉 `requirements/distribution/tests/integration/package/resources.test.mjs` → `PACKAGE_resources_provider_role_laws_and_rulebook_present_after_install` + `PACKAGE_resources_fixed_relative_path_from_PackageResources_module` | NEW + REUSE | `node --test requirements/distribution/tests/pack-closure.test.mjs` |

### 统计

```text
WHAT 命题：8（DISTRIBUTION-001..008）
落点：   NEW  10（cwd-independent-resources ×4 + pack-closure ×6 个 test 块，覆盖 001/002/003/004/005/006/007/008）
        REUSE 11（integration/package ×4、integration/resources ×2 文件、unit/run.mjs staleness、architecture.mjs 门 ⑥、build.mjs、npm pack --dry-run 发布关）
        MOVE  0（integration 本轮不迁）
GAP：    0
```

### semantic anchor 归属（semantic-anchors.mjs）

本包在 `scripts/checks/semantic-anchors.mjs` 中 **拥有 0 个 anchor id**。该 catalog（`ROLE_SEMANTIC_ANCHORS` / `TOOL_DESCRIPTION_ANCHORS` / `OFFICE_CAPABILITY_ANCHORS`）的逐 ID owner 是各 provider 语义包：`provider-language`（Gate C 结构/双语）、`office-capability`（Gate F 后果）、`action-affordance`（工具描述）、`cognitive-environment`、`behavior-diagnosis`（tip 散文）等。distribution 的 proof 面是 pack-level 结构 oracle（本表 NEW 测试 + integration/package），不是 prompt 锚点正则——anchor 锁散文内容，本包锁「散文在 artifact 里」。

### SPLIT@cutover 计划（现有测试的 owner 拆分）

| 现有文件 | 当前 owner 混合 | cutover 动作 |
|---|---|---|
| ~~`tests/integration/package/{contents,install,import,resources}.test.mjs`~~（4） | 全部断言归 distribution（PROOF-MAP：integration/package → KEEP distribution） | 已整族迁入本包 `tests/integration/package/`（Wave 2b） **MOVE** 入 `requirements/distribution/tests/`（只 import node: 内置，深度适配零成本），test 名 `PACKAGE_*` 保持为锚点 |
| `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs` | distribution（cwd 独立、路径存在、missing throws）+ `behavior-diagnosis`（rulebook 内容/ordinal/zh 撰写） | **SPLIT**：distribution 侧断言（`ENFORCER_resource_rulebook_load_independent_of_process_cwd`、`ENFORCER_resource_missing_package_path_throws`、`ENFORCER_resource_catalog_json_is_not_runtime_ssot`、路径存在性）移入本包；rulebook 语义断言移入 `behavior-diagnosis` |
| `requirements/cognitive-environment/tests/integration/resources/prompts.test.mjs` | distribution（`PROMPT_resources_load_from_package_independent_of_cwd`、路径存在）+ `provider-language`（双语/zh-authored/parity）+ `cognitive-environment`（组合序） | **SPLIT**：cwd 独立断言移入本包；parity/composition 归 `provider-language`/`cognitive-environment` |
| ~~`tests/unit/resources/prompt-semantic-depth.test.mjs`~~ | 无 distribution 断言（读 Role Law 内容锚点，owner = `provider-language`/`office-capability`/`action-affordance`/`cognitive-environment`） | **SPLIT**：全部断言归各语义包；distribution 不取（路径存在只是读取的附带条件） |

### 红意味着什么（每类落点）

- **NEW cwd 测试红**：资源查找开始依赖 cwd / 出现候选搜索 / 回退副本 → 安装后行为与仓库不一致（DISTRIBUTION-002 世界破坏）。
- **NEW pack-closure 测试红**：`main`/`exports` 漂移、whitelist 混入开发资产、某语义包新增资源目录没进闭包、`format-build-test` 丢失 build/package/packing 阶段、编译代码与 runtime resources 不再同 artifact 携带、资源 I/O 散落到 `src/Wanxiangshu/Resources/` 之外 → 发布物缺代码/资源、泄源码或 release proof 不再把关（DISTRIBUTION-001/003/004/006/007/008）。
- **NEW cwd fail-fast 测试红**：资源缺失不再抛 `package resource missing`（fallback/静默降级回潮）、`catalog.json` 复活为第二真源 → 坏包"看起来能跑"掩盖打包错误（DISTRIBUTION-006）。
- **REUSE integration 红**：tarball 内容假设被破坏（`files` 改、legacy 路径回潮、`catalog.json` 复活）。
- **architecture.mjs 门 ⑥ 红**：资源读取散落到 `Infrastructure/Resources/` 之外。
- **run.mjs staleness 红**：`dist/` 陈旧——测试将跑在过期字节上，拒绝运行。
- **发布关（`npm pack --dry-run`）红**：真实 tarball membership 与预期不符（缺 dist/resources 或混入开发资产）——release closure 未通过。
