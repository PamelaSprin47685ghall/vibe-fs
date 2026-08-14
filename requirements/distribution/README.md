# distribution

## 一句话 WHY

> 可安装 artifact 必须携带运行所需代码与 semantic resources（runtime closure），同时排除不属于交付面的源码/开发资产；否则源码树本地绿并不能证明消费者安装后拥有同一个产品世界。

## 阅读顺序

1. `WHY.md` — 为什么这个包必须独立存在；RED 长什么样。
2. `WHAT.md` — 唯一 normative 合同：8 条编号命题（`DISTRIBUTION-001..008`）。
3. `HOW.md` — 实现模型（package.json / build.mjs / PackageResources）与资源→语义包映射；含历史与弃权。
4. `PROOF.md` — 每条命题的可执行落点表；SPLIT@cutover 计划；semantic anchor 归属。

## WHAT 概览

| ID | 命题（压缩） |
|---|---|
| `DISTRIBUTION-001` | 单一可安装 artifact 同时含 production entrypoint 编译代码与全部 runtime semantic resources。 |
| `DISTRIBUTION-002` | runtime resource lookup 独立于 caller cwd（fixed package-relative path）。 |
| `DISTRIBUTION-003` | manifest `main`/`exports` 与实际 shipped paths 一致。 |
| `DISTRIBUTION-004` | package contents 由明确 whitelist 决定；开发/测试/legacy authority 不因偶然打包进入。 |
| `DISTRIBUTION-005` | build/test 消费与发布相同的 compiled production bytes（dist 单一编译产物；无 dist 双副本）。 |
| `DISTRIBUTION-006` | 资源加载仅经 `Infrastructure/Resources/`；缺失 fail fast；无代码内 fallback catalog。 |
| `DISTRIBUTION-007` | release proof 覆盖 build/package/packing + install/import/resource availability（证明阶梯 L5）。 |
| `DISTRIBUTION-008` | 特殊依赖：所有声明 runtime resource 的 semantic packages 的资源在 artifact 中可得，但不获其语义 ownership。 |

## HOW 概览

- **artifact 形态**：npm package `wanxiangshu`（`package.json`），`files: ["dist/", "resources/"]`；`main`/`exports["."]` 均指向 `./dist/OpenCode/Plugin/Plugin.js`。tarball = `dist/` + `resources/` + metadata（`package.json`/`README.md`/`LICENSE`）。
- **编译**：`scripts/build.mjs`（清空 `dist/` → Fable precompile → 删 `.gitignore`/`.fs` 残留 → 校验入口、rulebook、Role Law 双语文档）。不把 `resources/` 复制进 `dist/`（单份发布）。
- **资源加载**：`src/Wanxiangshu/Infrastructure/Resources/{PackageResources,RuntimeResources}.fs`；`PackageResources.readText` 经 `import.meta.url` 上溯 3 层到包根拼 `resources/<rel>`，无 cwd walk、无候选搜索、无 fallback；缺失抛 `package resource missing: <full>`。
- **运行前装配**：plugin init 调 `RuntimeResources.load()` → `install()` 一次，之后 `current()` 只读。
- 细节见 `HOW.md`。

## proof 概览

- 本包测试：`tests/cwd-independent-resources.test.mjs`（NEW，DISTRIBUTION-002 核心 oracle）、`tests/pack-closure.test.mjs`（NEW，DISTRIBUTION-003/004/005/007/008）。
- 复用现有 proof：`requirements/distribution/tests/integration/package/{contents,install,import,resources}.test.mjs`（Wave 2b 已迁入本包）、`requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs`、`scripts/checks/architecture.mjs`（resource-boundary gate）、`requirements/verification-system/tests/run.mjs`（fresh-dist staleness）；release 发布关 = `format-build-test` 末级 `npm pack --dry-run`。
- 单跑：`node --test requirements/distribution/tests/<file>`。全套：`node requirements/verification-system/tests/run.mjs`。

## 边界（DOES NOT OWN）

- resources 内部 prose/rule 的业务意义 → 各 semantic owner（`behavior-diagnosis`、`office-capability`、`provider-language`、`cognitive-environment`、`action-affordance`、`delegation` 等）。
- compiler/build tool 的具体实现、npm 是否永久是分发介质、`dist/`/`resources/` 路径名是否永久、version bump/release cadence → HOW。
- release/package proof 的阶梯强度 → `verification-system`。

## DEPENDS ON

- 特殊依赖（`requirements/INDEX.md` 依赖骨架末行）：**所有声明 runtime resource 的 semantic packages**——`resources/enforcer/**` 归 `behavior-diagnosis`；`resources/provider/**` 各子树归对应 provider 语义包。逐条理由见 `HOW.md` §依赖。
