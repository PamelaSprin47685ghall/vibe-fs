# HOW — 实现模型与约束（非 normative）

> 本文件描述当前实现长什么样、为什么长这样；不另造 normative owner。命题以 `WHAT.md` 为准。

## 1. artifact 形态（package.json）

```json
{
  "name": "wanxiangshu",
  "main": "./dist/Infrastructure/OpenCode/Plugin/Plugin.js",
  "exports": { ".": "./dist/Infrastructure/OpenCode/Plugin/Plugin.js" },
  "files": ["dist/", "resources/"],
  "engines": { "node": ">=20" },
  "peerDependencies": { "@opencode-ai/plugin": ">=1.17.4" }
}
```

- `main` == `exports["."]` == 同一 entry（DISTRIBUTION-003）。
- `files` 是唯一打包 whitelist：`dist/`（编译代码）+ `resources/`（runtime semantic resources）；`package.json`/`README.md`/`LICENSE` 由 npm 自动带进 tarball。**无** `.npmignore`。
- 私有不公开：`private: true`、`publishConfig.access: restricted`、`license: SEE LICENSE IN LICENSE`；从 tarball 或私有 registry 安装（`README.md` §获取与安装）。
- 用户可读安装说明见 `README.md`（`npm install ./wanxiangshu-0.8.2.tgz`；OpenCode 配置按包名或 `main` 挂载；启动时资源缺失/非法 → 启动失败，无代码内置副本兜底）。

## 2. 构建（scripts/build.mjs）

```text
rm -rf dist/
dotnet tool run fable precompile src/Wanxiangshu/Wanxiangshu.fsproj -o dist
   → 递归删除 dist/ 内的 .gitignore 与 .fs/.fsproj 残留（防源文件混入产物）
   → 校验：
       dist/Infrastructure/OpenCode/Plugin/Plugin.js 存在（entry）
       resources/enforcer/ 至少一个 tip 目录，且 <tip>/{enforcer.md,main.md} 存在
       resources/enforcer/catalog.json 不存在（已废止）
       resources/provider/role/<11 角色>/{en,zh-CN}.md 存在
       resources/provider/{world/common-law,library/ingress,library/closing}/{en,zh-CN}.md 存在
       dist/Sphinx/McpServer.js 存在
```

- 先清空再编译：`dist/` 不留上次构建的旧字节（DISTRIBUTION-005）。
- 不把 `resources/` 复制进 `dist/`：单份发布，杜绝双副本漂移（`docs/why/enforcer.md` 分发裁决）。
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
- 编译后模块位于 `dist/Infrastructure/Resources/`，`../../../resources` 恰好落在包根 `resources/`（`tests/integration/package/resources.test.mjs` 与本包 NEW oracle 双面锁）。
- `RuntimeResources.install` 是唯一安装点：plugin 构造器先 `load()` 再 `install()`，任何 consumer 运行前资源已就位；未安装即 `current()` 抛错（fail fast）。
- 资源读取只允许出现在 `Infrastructure/Resources/`：`scripts/checks/architecture.mjs` 门 ⑥ `resource-boundary`（`PACKAGE_RESOURCE_READ` 扫描，`src/Wanxiangshu/` 内 `PackageResources.` 引用只能出现在该目录）。

## 4. fresh-dist 门（测试消费发布字节）

`tests/unit/run.mjs` staleness gate：`dist/**`（除 `fable_modules/`）最新产物必须不早于任何 `src/Wanxiangshu/**/*.fs`/`.fsproj` 源，否则拒绝运行（`dist/ is stale by Ns — run: npm run format-build-test`）。这是「测试与发布消费同一份编译字节」的执行时保证（DISTRIBUTION-005）；机制实现属 `verification-system` 层（runner harness），命题归本包。

## 5. release proof（L5）

`package.json` `format-build-test` 全链：

```text
fantomas → scripts/check.mjs（L0 静态门）
        → scripts/build.mjs（编译）
        → tests/unit/run.mjs → tests/integration/run.mjs → tests/integration/package/run.mjs
        → scripts/warmup-opencode.mjs → tests/e2e/entry.test.mjs（Long Stroke）
        → npm pack --dry-run（L5：真实 tarball membership 清单）
```

- `npm pack --dry-run` 输出实际将被打包的文件清单（206KB JSON），是 artifact closure 的**终极 membership oracle**：integration/unit 层只做静态前置，真实 membership 以 pack 清单为准（DISTRIBUTION-007）。
- 阶梯分层/晋级纪律/No-Go 红线（`repeat-until-pass` 永久禁止）归 `verification-system`（`docs/proof/verify.md` VERIFY-001/002/006）。
- 发布流程：`npm ci` → `dotnet tool restore` → `npm run format-build-test` → `npm pack --pack-destination artifacts/package`；Git 工作树须干净（`README.md` §发布）。

## 6. 依赖（DEPENDS ON — 特殊 edge）

`requirements-design/INDEX.md` 依赖骨架末行：

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

## 7. 历史与弃权（非 normative 收纳）

| 源 | 信息 | 裁决 | 落点 |
|---|---|---|---|
| `docs/why/enforcer.md`「分发：单一打包 vs dist 双副本/代码 fallback」 | 拒双副本（掩盖打包错误）；拒代码内 fallback catalog（坏包静默成功）；resource 随 npm pack 单份发布 | EVIDENCE | WHY.md 考古；DISTRIBUTION-005/006；本文件 §2/§3 |
| `docs/why/enforcer.md`「元数据：catalog.json vs 目录即清单」 | 拒 JSON 第二真相；lexical order 由扫描派生 | EVIDENCE | DISTRIBUTION-006；§3 |
| `changes/completed/repository-warm-start.md` §10 Resource bounds | `MaxKeywords=8 / TopKPerKeyword=4 / MaxHintsTotal=24 / MaxWarmStartBytes=64KiB`——warm-start **hint 预算**，非 artifact 资源闭包 | 弃权（GARBAGE 于本包） | 归 `repository-investigation`/`knowledge-reuse`；本包不拥有 |
| `docs/proof/verify.md` VERIFY-001/002 第 5 层、Release gate、VERIFY-006 | 分层结构、晋级纪律、`repeat-until-pass` 禁令、watchdog 治理 | 弃权（HOW 于本包，机制归 `verification-system`） | DISTRIBUTION-007 只取「release proof 覆盖 closure」 |
| `docs/shape/architecture.md` / `docs/how/architecture.md` | 分层所有权、资源读取仅在 `Infrastructure/Resources/`、入口 `dist/Infrastructure/OpenCode/Plugin/Plugin.js` | HOW | §3 |
| `README.md` §构建与打包/运行时资源/故障排查 | tarball = dist + resources + metadata；不得含 src/tests/scripts/docs/artifacts；`resources/prompts/*` 已删 | HOW | §1/§2 |
| npm 必须永久是分发介质 | 当前实现用 npm package；换介质不改命题 | HOW（独立 change test 通过） | WHY.md |
| `dist/`/`resources/` 路径名必须永久 | 现行 HOW/contract evidence；改名需同步全部测试与 README | HOW | §1 |
| version bump / release cadence | 版本号、发布节奏是发布管理，非 closure 命题 | HOW | 不落命题 |
| release/package proof 强度（分层多强、watchdog 多严） | 横向治理 | 弃权 | `verification-system` |

## 8. 已知 GAP 与 cutover 待办

- **tarball membership 只由 release proof 验**：unit/integration 层不 spawn `npm pack`（与 `tests/integration/package/*` 头注释同一设计决定：测试里跑 npm 受 3s watchdog 约束且慢）。发布依赖 `format-build-test` L5 `npm pack --dry-run` 把关。若未来要把它收进自动化 suite，需独立 runner（非 unit watchdog 约束），见 `PROOF.md` SPLIT@cutover。
- integration 层 4 文件 + 2 资源文件 + 1 unit 资源文件的 owner 拆分计划见 `PROOF.md` §SPLIT@cutover。
