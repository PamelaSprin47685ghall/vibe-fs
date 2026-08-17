# 万象术 Herdchestra：让一万只大象一起跳舞

> Orchestration for agents that don't like being orchestrated.

写程序这件事，长久以来是一个人的手艺。一个工程师，一台电脑，一条思路，从需求走到代码，饿了吃饭，困了睡觉。后来 AI 来了，我们忽然有了一群不会疲倦的程序员。它们读代码，查资料，写功能，补测试，做代码审查；来自不同的厂商，用着不同的模型，脾气也各不相同——有的谨慎，有的激进；有的写起代码来像钟表匠，有的像推土机。而且，每一个都还在变聪明。

于是有了一个新问题：如果一个智能体能写程序，一百个、一千个、一万个一起写，会怎么样？

这件事，有点像让一万只大象跳舞。

大象并不蠢。恰恰相反，它们聪明、有力、记性惊人，问题只在于太重。一只大象往左挪一步，那只是一步；一万只大象同时往左挪一步，地都要震。工程里也是如此：这边刚改完一个文件，那边的智能体已经把它覆盖了；这个还在查问题，那个已经按自己的理解动了手；有的以为任务做完了，有的还欠着测试；有的掉线，有的重试，有的忘了自己干过什么。要是再闯进来一个带着别家模型习惯的，场面就更热闹了。

对付这样的场面，喊是喊不动的，给每头象拴一根更长的绳子也没用。现在不少智能体编程的做法，说到底还是一个指挥站在台中央，拼命挥棒子，指望几十头越来越有主见的象恰好踩在同一个小节上。两三头的时候，这法子看着不错；十头，开始手忙脚乱；一万头，指挥棒就成了道具。

人们爱说，站在风口上，猪也能飞。风确实起来了：模型越来越强，价格越来越低，速度越来越快智能体越来越多。可是风托得起一只猪，托不起一万头没有队形的象。到了那个规模，能不能飞，不再取决于哪头象最聪明，而取决于地上有没有路，有没有红绿灯，有没有账本、分工和规矩；有人跌倒了，队伍还走不走；有人走散了，还能不能找回来；各支队伍能不能踩着自己的步子，在同一个世界里把同一件事做完。

这就是我们做万象术的原因。“万象”，既是一万头象，也是世间万象。我们不想去造世界上最聪明的那头象，也不想把所有的象驯成同一个姿势。我们想做的事更笨一些，也更基础一些：让不同的智能体各尽其长，又能共同成事。今天最好的模型能来，明天更好的也能来。

它们性格不同，能力不同，上下文不同，干活的法子也不同。但只要进了同一个工程，就知道自己是谁，正在做什么，能做什么、不能做什么；知道哪些事已经发生，哪些事还没有完成；知道什么时候可以并行，什么时候必须等待；知道怎样把手里的活交给另一个智能体—也知道，万一哪个智能体消失了，世界不该跟着失忆。

这里没有永远正确的总指挥，也没有一根拴住所有象的绳子。有的只是一套基础设施，让协作这件事本身变得可靠。

于是，一头象可以跑，一百头象可以协作，一万头象可以跳舞。而且跳的不是同一种舞：爵士、芭蕾、街舞，还有没人见过的新舞步——各有各的节奏，各有各的长处，谁也不踩谁的脚。

这就是万象术：不是驯服智能的法术，而是驾驭智能规模的技术。我们相信，AI 编程的下一个时代，不属于拥有最强智能体的人，而属于能让无数不同的智能体自由加入、可靠协作、把真正复杂的工作做完的人——就像今天的进程、服务和机器那样。当智能成为充沛的资源，协同就是新的稀缺品。万象术想做的，就是这一层。

让一万头大象一起跳舞。更快，更好，更省。

万象皆可用，众智自成事。

---

万象术以 OpenCode 插件形式落地。它不替换 Host 的对话模型，而是在其上叠加一层结构化编排：Orchestrator 统筹全局，Manager 分解任务并管理子会话，Coder 修改源码，Inspector 只读调查，DevOps 管控进程与环境，Reviewer 裁决质量。每个角色有自己的工具面与权限边界，Companion 在会话级提供认知上下文，Fallback 与 Review 各有明确写入口。智能体不必彼此信任，只需遵守同一套事实与边界。

Wanxiangshu is proprietary commercial software.
Use, copying, modification, and distribution are governed by LICENSE.

## 用户指南

### 产品简介

万象术作为 OpenCode 插件加载后，为 Host 会话提供多角色协作、任务分叉与汇合、审阅与恢复。公开入口：

```text
import "wanxiangshu"
→ dist/OpenCode/Plugin/Plugin.js
```

`package.json` 的 `main` / `exports["."]` 指向同一路径。npm 包含 `dist/` 与 `resources/`，不含源码与测试树。

### 系统要求

- Node.js `>= 20`（`engines.node`）
- OpenCode Host，peer 依赖 `@opencode-ai/plugin`（`>= 1.17.4`）
- 从源码构建时：.NET SDK（`global.json`）与 `dotnet tool restore`（Fable、Fantomas）

`packageManager` = `npm@11.12.1`。安装依赖使用 `npm ci`（已提交 `package-lock.json`）。

### 获取与安装

`private: true` 商业软件，不从公共 npm 默认源分发。从 tarball 或私有 registry 安装：

```bash
npm install ./wanxiangshu-0.8.2.tgz
# 或
npm install wanxiangshu --registry <your-private-registry>
```

版本以 `package.json` 的 `version` 为准（当前 **0.8.2**）。

### OpenCode 配置

1. 在可解析 peer 插件 API 的环境中安装本包。
2. 按 Host 的 plugin 配置挂载入口（包名 `wanxiangshu` 或已安装包的 `main`）。
3. 启动 Host。插件初始化时加载 `resources/` 下 system prompt 与 Enforcer catalog；资源缺失或非法则启动失败（fail fast），无代码内置副本兜底。

配置以 Host 文档与 `peerDependencies` 为准。角色与 Prompt 语义以 [requirements/README.md](requirements/README.md) 为高级参考；安装与挂载不依赖阅读条款正文。

可选环境变量：

| 变量 | 作用 |
|------|------|
| `WANXIANGSHU_SKIP_AUTO_INJECTED=1` | 跳过 HOST-013 新的 `auto-injected` 伪工具注入；已落盘历史 pair 仍会 replay（`provider=cursor` 时同样跳过新注入） |
| `WANXIANGSHU_CHAT_MAX_RETRIES` | 覆盖 Host `experimental.chatMaxRetries`（非负整数） |
| `WANXIANGSHU_PROCESS_HARD_LIMIT_SECS` | executor 单进程硬超时上限（秒） |
| `WANXIANGSHU_NO_FATAL_EXIT=1` | 诊断路径禁止 `process.exit`（测试用） |

### 快速开始

安装并在 OpenCode 注册插件后，Orchestrator 发起任务，Manager 分解并管理子会话，子角色按工具面分工：

```bash
npm install ./wanxiangshu-0.8.2.tgz
# 在 OpenCode 注册插件后启动会话
```

```text
Orchestrator
  └── Manager
        ├── Coder
        ├── Inspector
        ├── DevOps
        ├── Browser / Inquiry
        └── Reviewer
```

Distiller、Blogger 等由编排路径调用，不作为单独“安装角色”配置。

### 智能体角色

十个 public Role，与 `requirements/participant-identity`、`requirements/cognitive-environment` 一致。工具面由 `Roles.permissions` 定义（`requirements/capability-enforcement` 四层同构）：

| 角色 | 典型工具面 | 说明 |
|------|------------|------|
| Orchestrator | `fork-manager`, `join`, `horizon` | 顶层编排 |
| Manager | `fork-manager`, `join`, `horizon`, `todowrite`, `fission` | 任务分解与子会话 |
| Coder | `read`, `write`, `edit`, `glob`, `grep`, `inspect`, `fetch`, `fission` | 源码修改 |
| Inspector | `read`, `glob`, `grep`, `query-shell`, `fetch`, `fission` | 只读调查 |
| DevOps | `fork-pty`, `executor`, `inspect`, `behavior` 等 | 进程与环境 |
| Browser | `read`, `glob`, `grep`, `stealth-browser-mcp`, `fission` | 浏览类任务 |
| Inquiry | `inspect`, `sphinx`, `fission` | 语义调查与分析 |
| Reviewer | `read`, `glob`, `grep`, `judge` | 审阅与裁决 |
| Distiller | 无工具 | 输出蒸馏/摘要 |
| Blogger | `chronicle` | Companion 叶子，写认知上下文 |

Bookkeeper 是内部叶子角色（有独立 Role Law，不进 public Role DU）。每个 managed work session 配套叶子 Companion（Blogger）。精确权限见 `requirements/participant-identity` 与 `requirements/capability-enforcement`。

### 运行时数据

领域事实写入 Git common directory 下插件私有 runtimes 路径中的 journal（按 runtime 的 NDJSON），不在业务 workspace 强制创建插件私有目录。随包资源：

- `resources/provider/`（Common Law / Role Law / Tool Law / Delegation Law / Office Library；EN + zh-CN）；`resources/enforcer/<TipName>/{enforcer,main}{,.zh-CN}.md`；`resources/git/wanxiang-hook.mjs`；`resources/wanxiangshu.mjs`（model routing 模板）。**无** `resources/prompts/*`；**无** `catalog.json` SSOT。
- journal 与事实名默认冻结；升级前阅读 [CHANGELOG](CHANGELOG.md)。


### 商业许可与支持

使用、复制、修改与分发受 [LICENSE](LICENSE) 约束。`license` 为 `SEE LICENSE IN LICENSE`，`publishConfig.access` 为 `restricted`。

商业授权与支持请联系版权方。本 README 不承诺开源时间表。

---

## 贡献者指南

面向维护者。法律上仍为专有商业软件，内部工程纪律见 [AGENTS.md](AGENTS.md)。

### 仓库结构

```text
src/           生产源码
resources/     随包运行时资源
requirements/  48 包 normative 语义树：每包 WHY/WHAT/HOW/PROOF + 包自有测试
proposals/     deferred 未来材料（用户管理）
scripts/       构建与少量仓库检查
cleanup/       退役追踪与 migration 报告
dist/          最终编译输出，不提交
artifacts/     中间产物与本地发布产物，不提交
```

- 生产 F# 唯一根：`src/Wanxiangshu/`
- 规范导航 [requirements/README.md](requirements/README.md)；历史 Clause 与变更工作流已归档（2026-08-14 cutover；git 历史可回溯）
- 测试全部包自有：`requirements/<package>/tests/`；共享 harness 在 `requirements/verification-system/tests/`（含 `support/`、unit runner、integration orchestrator、Long Stroke e2e）
- 脚本：`scripts/build.mjs`、`scripts/check.mjs`、`scripts/checks/*`、`scripts/lib/walk.mjs`

### 开发环境

Node.js ≥ 20，`npm@11.12.1`；.NET SDK（`global.json`）；本地工具 `.config/dotnet-tools.json`（Fable、Fantomas）。

### 首次设置

```bash
npm ci
dotnet tool restore
npm run format-build-test
```

请用 `npm ci`。`bun-pty` 经 `overrides` 固定（见 `package.json` / [AGENTS.md](AGENTS.md)）。

### 常用命令

```bash
npm ci
dotnet tool restore
npm run format-build-test
```

| 命令 | 作用 |
|------|------|
| `npm run format-build-test` | Fantomas 写盘 → `scripts/check.mjs` → 编译 → unit → integration → package → warmup → Long Stroke e2e → `npm pack --dry-run` |

### 测试分层

| 层 | 入口 | 范围 |
|----|------|------|
| unit | `requirements/verification-system/tests/run.mjs` | 对 `dist/` 的契约；经 `requirements/verification-system/tests/support/` |
| integration | `requirements/verification-system/tests/integration/run.mjs` | resources、plugin、persist、strength、package、harness（套件在 owner 包 `tests/integration/` 下） |
| e2e | `requirements/verification-system/tests/e2e/entry.test.mjs` | `scenarios/long-stroke.toml` + `support/` oracles；单次连续生命周期 |

`dist/` 陈旧时 unit 拒绝运行。资源路径由包内 `dist/` 相对定位到 `resources/`，不依赖 `process.cwd()`。

### 规范与文档体系

规范是万象术的语义根：每条行为命题有稳定 ID、测试落点和 owner 包。规范不跟踪实现进度，只定义正确性。

- **规范**：`requirements/<package>/{WHY,WHAT,HOW,PROOF}.md`（48 包 normative 树；WHAT 命题 ID 稳定寻址，每条有测试落点）。
- **历史 Clause 与变更记录**：2026-08-14 cutover 已归档（含 Kolmogorov 工程纪律与 completed change 考古；git 历史可回溯）。
- 测试全部包自有（`requirements/<package>/tests/`），直接引用 WHAT 命题 ID。规范不跟踪实现进度。

导航：[requirements/README.md](requirements/README.md)。治理见 [AGENTS.md](AGENTS.md) 与归档文档。

### 运行时资源

```text
resources/provider/
  world/common-law/{en,zh-CN}.md
  role/<role>/{en,zh-CN}.md
  tool/<tool>/{en,zh-CN}.md
  delegation/<scenario>/{en,zh-CN}.md
  host/<guideline>/{en,zh-CN}.md
  lifecycle/<phase>/{en,zh-CN}.md
  review/challenge/{en,zh-CN}.md
  runtime/<scenario>/{en,zh-CN}.md
  library/<office>/{en,zh-CN}.md
resources/enforcer/<TipName>/{enforcer,main}{,.zh-CN}.md
resources/git/wanxiang-hook.mjs
resources/wanxiangshu.mjs
```

加载：`Resources/`（`PackageResources`、`ProviderResources`、`PromptResources`、`EnforcerCatalogResource`、`RuntimeResources`）；插件初始化 load/install 一次。
旧 `resources/prompts/*-system.md` 已删除；生产 system 仅由 Common Law → Role Law → Office Library 组成。

### 构建与打包

- **构建**：`scripts/build.mjs`（清空 `dist/` → Fable → 校验入口与资源）。不把 `resources/` 复制进 `dist/`。
- **打包**：仓库根 `npm pack`（或 `--pack-destination artifacts/package`）。tarball = `dist/` + `resources/` + metadata（`package.json`、`README.md`、`LICENSE`）。不得含 `src/`、`requirements/`、`scripts/`、`artifacts/`。

发布预检：`npm run format-build-test`（干净工作树；验证日志进 CI artifact）。

### 提交要求

1. 源码变更后运行 `npm run format-build-test`。
2. 优先 stage 具体路径；保留 hooks；不用 `--no-verify`。
3. 用户可见变化写入 [CHANGELOG.md](CHANGELOG.md)。
4. 不推送对 `main`/`master` 的破坏性历史改写；force push 等需显式许可。

### 发布

```bash
npm ci
dotnet tool restore
npm run format-build-test
npm pack --pack-destination artifacts/package
```

Git 工作树须干净。验证输出放 CI artifact 或发布附件，不提交进仓库。

### 安全与保密

源码与内部脚本默认不进 tarball。勿提交密钥与私有 registry 凭证。漏洞与授权走版权方私有渠道。

### 许可证

专有商业软件。见 [LICENSE](LICENSE)。`private: true`；分发受 LICENSE 与商业合同约束。

更多：[requirements/README.md](requirements/README.md) · [CHANGELOG.md](CHANGELOG.md) · [LICENSE](LICENSE) · [AGENTS.md](AGENTS.md)