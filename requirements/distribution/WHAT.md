# WHAT — distribution 的唯一 normative 合同

> 命题 = 当前世界必须同时成立的事实。每条命题有测试落点（见 `PROOF.md`）。
> 边界（DOES NOT OWN）写在各条「边界」；更完整的弃权记录在 `HOW.md` §历史与弃权。

## DISTRIBUTION-001 — artifact 同时携带编译代码与 runtime semantic resources（closure 单点）

**规范陈述**：安装产物（npm tarball / 已安装包）必须同时包含 production entrypoint 的编译代码与全部 runtime semantic resources；两者作为同一个 artifact 交付，不存在「代码从 A 渠道、资源从 B 渠道」的分发。

**含义/动机**：Wanxiangshu 运行时依赖 `dist/**`（入口 `dist/Infrastructure/OpenCode/Plugin/Plugin.js`）与 `resources/**`（provider 双语散文 + enforcer rulebook）。缺任何一边，安装后的插件世界不完整。closure 单点=消费者只需安装一个 artifact。

**边界**：不拥有 `resources/**` 内部 prose/rule 的业务意义（→ 各 semantic owner）；不决定具体打包工具/介质（→ HOW）。

**证据**：→ `PROOF.md` DISTRIBUTION-001 行。

## DISTRIBUTION-002 — runtime resource lookup 独立于 caller cwd

**规范陈述**：运行期读取 package resource 必须相对包自身定位（fixed package-relative path），不得依赖 `process.cwd()`、不得做候选路径搜索、不得回退到 `dist/` 或 `src/` 下的副本。

**含义/动机**：OpenCode Host 可以从任意工作目录加载插件（用户项目目录、`/`、临时目录）。若查找依赖 cwd，同一包在不同启动目录下行为不同——"源码树本地绿"会伪装成"安装后绿"。`PackageResources.readText` 经 `import.meta.url` 上溯到包根拼 `resources/<rel>`，是唯一实现形态。

**边界**：不拥有「cwd 具体是什么」——只保证查找结果与 cwd 无关。

**证据**：→ `PROOF.md` DISTRIBUTION-002 行（本包 NEW oracle 主战场）。

## DISTRIBUTION-003 — manifest main/exports 与实际 shipped paths 一致

**规范陈述**：`package.json` 的 `main` 与 `exports["."]` 必须指向同一个、实际存在于 artifact 内的文件（当前：`./dist/Infrastructure/OpenCode/Plugin/Plugin.js`）；消费者按 manifest 解析入口必须成功。

**含义/动机**：manifest 是消费者与工具链（Host plugin loader、bundler、`import "wanxiangshu"`）定位入口的唯一契约。manifest 与实际路径漂移 = 装上了但 import 失败。

**边界**：不要求 `main`/`exports` 的字符串格式（`./` 前缀等）固定——HOW；只要求一致性 + 可达性。

**证据**：→ `PROOF.md` DISTRIBUTION-003 行。

## DISTRIBUTION-004 — package contents 由明确 whitelist 决定

**规范陈述**：打包内容由 `package.json` `files` 白名单显式决定（当前 `["dist/", "resources/"]`）；不属于 consumer runtime 的开发/测试/legacy authority（`src/`、`tests/`、`scripts/`、`artifacts/`、`spec/`、`.fs`/`.fsproj` 源文件、已废止的 `resources/prompts/`、`catalog.json`）不得因偶然目录打包进入 artifact。

**含义/动机**：无 whitelist 时，npm 默认规则会把不该进 tarball 的源码/测试/文档带进去（体积、泄源码、维护负担）。白名单是"显式只装 runtime"的机制；任何新进入 artifact 的内容必须先在 `files` 里声明。

**边界**：`files` 的具体成员集合是 HOW（当前恰好两个 root，未来可合法增加 runtime root）；本命题只要求"明确白名单 + 排除禁令类"。

**证据**：→ `PROOF.md` DISTRIBUTION-004 行。

## DISTRIBUTION-005 — build/test 消费与发布相同的 compiled production bytes

**规范陈述**：`dist/**` 是唯一编译产物：构建先清空再编译（不留旧产物混入），测试只消费这份 `dist/`，发布也只打包这份 `dist/`；资源单份发布（不复制进 `dist/` 形成双副本）；测试运行时存在 fresh-dist 门（`dist/` 比任何 `.fs` 源新，否则拒绝跑）。

**含义/动机**：测试另一份实现（读 `src/` 副本、或 dist 里混入旧字节）会让"绿"失去对发布物的证明力。双副本是历史明确拒绝的方案（历史 why/enforcer 条款）：掩盖打包错误。

**边界**：Fable/fantomas 等构建工具的具体实现是 HOW；本命题只要求"唯一产物 + 测试与发布同源"。

**证据**：→ `PROOF.md` DISTRIBUTION-005 行。

## DISTRIBUTION-006 — 资源加载仅经 Infrastructure/Resources/，缺失 fail fast

**规范陈述**：package resource 的 I/O 只发生在 `src/Wanxiangshu/Infrastructure/Resources/`（`PackageResources`/`ProviderResources`/`PromptResources`/`EnforcerCatalogResource`/`RuntimeResources`）；资源缺失必须抛错终止（`package resource missing: <full>`），不得代码内 fallback catalog、不得静默降级；rulebook 元数据不以 `catalog.json` 为第二真源（目录即清单）。

**含义/动机**：散布的资源读取无法审计 closure；fallback 让坏包"看起来能跑"从而掩盖打包错误（历史被拒方案）；`catalog.json` 与目录双写必漂（历史 why/enforcer 条款）。

**边界**：资源缺失时上层具体如何反应（启动失败 vs 其它）由消费方语义决定；本命题只要求"读不到就失败，不伪装"。

**证据**：→ `PROOF.md` DISTRIBUTION-006 行。

## DISTRIBUTION-007 — release proof 覆盖 artifact closure

**规范陈述**：release 级 proof（`package.json` `format-build-test` 末级）必须包含 build/package/packing，并验证 install/import/resource availability；当前形态 = `npm run format-build-test` 全链 + 末级 `npm pack --dry-run`（证明阶梯 L5，历史 verify 条款 VERIFY-001/002 第 5 层）。

**含义/动机**：closure 命题若只在 unit/integration 层验，发布前没有任何一步真正面对"打包后的样子"。L5 是唯一站在 artifact 面的一次性确定性 full proof；发布由它把关（No-Go 红线：`repeat-until-pass` 永久禁止）。

**边界**：阶梯的分层结构、晋级纪律、watchdog 等横向治理归 `verification-system`；本命题只要求 release proof **覆盖** closure 各面。

**证据**：→ `PROOF.md` DISTRIBUTION-007 行。

## DISTRIBUTION-008 — 特殊依赖：semantic packages 的 runtime resources 在 artifact 中可得

**规范陈述**：所有声明 runtime resource 的 semantic packages，其资源必须在 shipped artifact 中完整可得——`resources/enforcer/<TipName>/{enforcer.md,main.md}`（→ `behavior-diagnosis`）、`resources/provider/**` 双语树（→ 各 provider 语义包：`office-capability`/`provider-language`/`cognitive-environment`/`action-affordance`/`delegation`/…）。本包保证"存在且被 artifact 携带"，**不获得**这些资源的语义 ownership。

**含义/动机**：这是依赖骨架的特殊 edge（`requirements/INDEX.md`）：distribution 对每个声明 runtime resource 的包有一条薄依赖——"你的资源必须随 artifact 可得"。语义包负责资源**内容**；本包负责资源**到达**。二者缺一不可，且不能合并 ownership。

**边界**：资源内容的正确性/双语锚点/语义深度归各 semantic owner（`provider-language` Gate C、`behavior-diagnosis` rulebook 契约等）。

**证据**：→ `PROOF.md` DISTRIBUTION-008 行。
