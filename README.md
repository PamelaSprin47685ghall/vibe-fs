# Wanxiangshu

OpenCode 上的结构化多 Agent 编排插件：Orchestrator / Manager 调度，Coder、Inspector、DevOps、Reviewer 等角色分工，Companion 提供会话级认知上下文，Fallback 与 Review 有明确写入口。

Wanxiangshu is proprietary commercial software.
Use, copying, modification, and distribution are governed by LICENSE.

## 用户指南

### 产品简介

万象术（Wanxiangshu）作为 OpenCode 插件加载后，为 Host 会话提供多角色协作、任务分叉与汇合、审阅与恢复。公开入口：

```text
import "wanxiangshu"
→ dist/Infrastructure/OpenCode/Plugin/Plugin.js
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

配置以 Host 文档与 `peerDependencies` 为准。角色与 Prompt 语义以 [docs/README.md](docs/README.md) 为高级参考；安装与挂载不依赖阅读条款正文。

可选环境变量：

| 变量 | 作用 |
|------|------|
| `WANXIANGSHU_SKIP_AUTO_INJECTED=1` | 跳过 HOST-013 新的 `auto-injected` 伪工具注入；已落盘历史 pair 仍会 replay（`provider=cursor` 时同样跳过新注入） |
| `WANXIANGSHU_CHAT_MAX_RETRIES` | 覆盖 Host `experimental.chatMaxRetries`（非负整数） |
| `WANXIANGSHU_PROCESS_HARD_LIMIT_SECS` | executor 单进程硬超时上限（秒） |
| `WANXIANGSHU_NO_FATAL_EXIT=1` | 诊断路径禁止 `process.exit`（测试用） |

### 快速开始

```bash
npm install ./wanxiangshu-0.8.2.tgz
# 在 OpenCode 注册插件后启动会话
# Orchestrator / Manager 发起任务；子角色按工具面分工
```

```text
Orchestrator
  └── Manager
        ├── Coder
        ├── Inspector
        ├── DevOps
        ├── Browser / Meditator
        └── Reviewer
```

Executor、Blogger 等由编排路径调用，不作为单独“安装角色”配置。

### Agent 角色

与 `docs/what/agent.md` 一致（十个 system prompt 角色）：

| 角色 | 典型工具面 | 说明 |
|------|------------|------|
| Orchestrator | `fork-manager`, `join` | 顶层编排 |
| Manager | `fork-agent`, `join`, `list` | 任务分解与子会话 |
| Coder | `read`, `write`, `edit`, `glob`, `grep`, `inspector` | 源码修改 |
| Inspector | `read`, `glob`, `grep`, `executor` | 只读调查 |
| DevOps | `fork-pty`, `executor`, 检索与 `inspector` 等 | 进程与环境 |
| Browser | 检索与网络相关工具 | 浏览类任务 |
| Meditator | 检索与 `inspector` | 分析类任务 |
| Reviewer | 检索、`verdict` | 审阅与裁决 |
| Executor | 无工具 | 内部执行/摘要 |
| Blogger | `blog` | Companion 叶子，写认知上下文 |

每个 managed work session 配套叶子 Companion（Blogger）。精确权限见 `docs/what/agent.md`。

### 运行时数据

- 领域事实写入 Git common directory 下插件私有 runtimes 路径中的 journal（按 runtime 的 NDJSON），不在业务 workspace 强制创建插件私有目录。
- 随包资源：`resources/provider/`（Common Law / Role Law / Office Library；EN + zh-CN）；`resources/enforcer/<TipName>/{enforcer.md,main.md}`。**无** `resources/prompts/*`；**无** `catalog.json` SSOT。
- journal 与事实名默认冻结；升级前阅读 [CHANGELOG](CHANGELOG.md)。

### 升级与兼容性

- 0.5.3 无运行时协议变更：布局、资源打包与仓库整理为主，公开行为与 wire 语义与 0.5.2 产品合同一致。
- 0.5.4：DSL 全面主导化与门禁收紧（控制流/测试 harness）；journal/wire 协议与 0.5.3 兼容。
- 0.6.0：Causal CE / Finality / HOST-013 / Student–Teacher / Projection Algebra 收口；文档治理与 canary 可信度；相对 0.5.4 兼容方向见 CHANGELOG。
- 0.8.2：持久化 / Git / session 工作流全面异步化，SyncDelegate 语义批处理与 Host/Fork/Magic Todo 行为收口；相对 0.8.1 的兼容性说明见 CHANGELOG。
- 0.8.1：REVIEW-003 challenge 跟 session ProviderLanguage；英文 canonical 字节不变；相对 0.8.0 无 domain protocol 破坏，见 CHANGELOG。
- 0.8.0：Provider-visible prose 全部经 ProviderLanguage（PROMPT-019）；Gate E 0；Gate C Role Law semantic-anchor 现行；相对 0.7.0 无 domain protocol 破坏，见 CHANGELOG。
- 0.7.0：Kolmogorov 所有权二级拆分（LWR / ManagerLife / PluginTransforms / HostSignal / Reconciler）；G6/G9 Exit；Strength / JS tools / MCP；相对 0.6.0 无 domain protocol 破坏，见 CHANGELOG。
- 升级：安装新版本 → 确认 Node ≥ 20 与 Host peer → 重启 OpenCode。
- 破坏性变更见 CHANGELOG；跳版本时按条目顺序阅读。

### 故障排查

| 现象 | 处理方向 |
|------|----------|
| 插件无法加载 / import 失败 | 确认 `dist/.../Plugin.js` 存在；tarball 须含 `dist/` 与 `resources/` |
| 启动即失败（资源） | 检查 `resources/provider/` 语言对与 `resources/enforcer/<tip>/` 完整合法 |
| peer 依赖报错 | 安装与 Host 匹配的 `@opencode-ai/plugin` |
| 行为与预期不符 | 对照 CHANGELOG 与 [docs/README.md](docs/README.md)；商业支持见下节 |

源码排查见贡献者指南与 `AGENTS.md`。

### 商业许可与支持

使用、复制、修改与分发受 [LICENSE](LICENSE) 约束。`license` 为 `SEE LICENSE IN LICENSE`，`publishConfig.access` 为 `restricted`。

商业授权与支持请联系版权方。本 README 不承诺开源时间表。

---

## 贡献者指南

面向维护者。法律上仍为专有商业软件。

### 仓库结构

```text
src/         生产源码
resources/   随包运行时资源
docs/        当前有效的分域规范 why/what/shape/how/proof
changes/     已批准变更的 proposed/active/completed 生命周期记录
tests/       unit / integration / e2e
scripts/     构建与少量仓库检查
dist/        最终编译输出，不提交
artifacts/   中间产物与本地发布产物，不提交
```

- 生产 F# 唯一根：`src/Wanxiangshu/`
- 规范导航 [docs/README.md](docs/README.md)；词汇表 `docs/what/glossary.md`；变更工作流见 [changes/README.md](changes/README.md)
- 测试：`tests/unit/`、`tests/integration/`（resources / journal / plugin / package / harness）、`tests/e2e/`（单一 Long Stroke：`scenarios/` + `support/`）
- 脚本：`scripts/build.mjs`、`scripts/check.mjs`、`scripts/checks/*`、`scripts/lib/walk.mjs`

### 开发环境

Node.js ≥ 20，`npm@11.12.1`；.NET SDK（`global.json`）；本地工具 `.config/dotnet-tools.json`（Fable、Fantomas）。

### 首次设置

```bash
npm ci
dotnet tool restore
npm run format-build-test
```

请用 `npm ci`。`bun-pty` 经 `overrides` 固定（见 `package.json` / `AGENTS.md`）。

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
| unit | `tests/unit/run.mjs` | 对 `dist/` 的契约；经 `tests/unit/support/domain.mjs` |
| integration | `tests/integration/run.mjs` | resources、journal、plugin、package、harness |
| e2e | `tests/e2e/entry.test.mjs` | `scenarios/long-stroke.toml` + `support/` oracles；单次连续生命周期 |

`dist/` 陈旧时 unit 拒绝运行。资源路径由包内 `dist/` 相对定位到 `resources/`，不依赖 `process.cwd()`。

### 规范与文档体系

- **行为 / 边界 / 目标实现 / 证明**：`docs/what` · `docs/shape` · `docs/how` · `docs/proof`（条款 ID 稳定寻址）。
- **理由**：`docs/why/`（含 Kolmogorov 工程纪律）。
- **变更记录**：`changes/proposed/` 保存用户管理、已批准且等待启动的工作；
  `changes/active/` 保存已启动但未闭环的工作；`changes/completed/` 保存完成历史。
- `changes/` 不定义当前产品语义；Active Change 也不能代替正式文档。
- 测试直接引用条款 ID。规范不跟踪实现进度。

导航：[docs/README.md](docs/README.md)。治理：[docs/what/document-governance.md](docs/what/document-governance.md) · [docs/how/document-governance.md](docs/how/document-governance.md)。

### 运行时资源

```text
resources/provider/
  world/common-law/{en,zh-CN}.md
  role/<role>/{en,zh-CN}.md
  library/...
resources/enforcer/<TipName>/{enforcer.md,main.md}
```

加载：`Infrastructure/Resources/`（`PackageResources`、`ProviderResources`、`PromptResources`、`EnforcerCatalogResource`、`RuntimeResources`）；插件初始化 load/install 一次。
旧 `resources/prompts/*-system.md` 已删除；生产 system 仅由 Common Law → Role Law → Office Library 组成。

### 构建与打包

- **构建**：`scripts/build.mjs`（清空 `dist/` → Fable → 校验入口与资源）。不把 `resources/` 复制进 `dist/`。
- **打包**：仓库根 `npm pack`（或 `--pack-destination artifacts/package`）。tarball = `dist/` + `resources/` + metadata（`package.json`、`README.md`、`LICENSE`）。不得含 `src/`、`tests/`、`scripts/`、`docs/`、`artifacts/`。

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

更多：[docs/README.md](docs/README.md) · [CHANGELOG.md](CHANGELOG.md) · [LICENSE](LICENSE) · `AGENTS.md`