# Wanxiangshu

OpenCode managed multi-agent orchestration plugin.

Proprietary commercial software. See [LICENSE](LICENSE).

## 用户指南

### 产品简介

万象术（Wanxiangshu）是 OpenCode 的结构化多 Agent 编排插件：Orchestrator / Manager 调度，Coder / Inspector / DevOps / Reviewer 等角色分工，Companion Blogger 提供认知上下文，Fallback 与 Review 有明确写入口与证据链。

公开入口：

```text
import "wanxiangshu"
→ dist/Infrastructure/OpenCode/Plugin/Plugin.js
```

### 系统要求

- Node.js `>= 20`
- OpenCode Host 提供 `@opencode-ai/plugin`（peer 语义；开发依赖见 `package.json`）
- 构建源码时：.NET SDK（见 `global.json`）与 `dotnet tool restore`（Fable / Fantomas）

### 安装

本包为 `private: true` 商业软件。从私有 registry 或交付的 tarball 安装：

```bash
npm install ./wanxiangshu-0.5.3.tgz
# 或私有源
npm install wanxiangshu --registry <your-registry>
```

在 OpenCode 中注册插件（以 Host 文档为准），使插件入口指向包的 `main` / `exports["."]`。

### 配置与快速开始

1. 确保 OpenCode 可解析 peer 插件 API。
2. 安装本包后，按 Host 的 plugin 配置挂载 `wanxiangshu`。
3. 角色与工具面由插件静态装配；精确权限与语义以 [`spec/`](spec/) 条款为准。

最小心智模型：

```text
Orchestrator
  └── Manager
        ├── Coder
        ├── Inspector
        ├── DevOps
        ├── Browser / Meditator
        └── Reviewer
```

### 运行时数据

领域事实写入 Git common directory 下私有 `wanxiangshu-next/runtimes` 路径的 journal（per-runtime NDJSON），不在业务 workspace 强行创建 `node_modules` 或插件私有目录。

journal 格式与事实名默认冻结；升级前请阅读 [CHANGELOG](CHANGELOG.md)。

### 商业许可与支持

使用、复制、修改与分发受 [LICENSE](LICENSE) 约束。商业支持与授权请联系版权方。

---

## 贡献者指南

面向内部维护者与未来可能的贡献者。工程按可复现、可审查的标准建设；法律上仍为闭源商业软件。

### 仓库结构

```text
src/Wanxiangshu/   生产 F# 源码（唯一源码根）
resources/         运行时静态资源（prompts、enforcer catalog）
spec/              绑定规范 + conformance 账本
docs/              解释性文档、RFC、归档
tests/unit/        第 1–3 层测试（mjs，import dist）
tests/e2e/         OpenCode harness 与 canary
tests/support/     测试支撑
scripts/           构建与门禁入口
dist/              Fable 输出（不提交；npm pack 包含）
```

### 开发环境

```bash
npm ci
dotnet tool restore
```

可选：`global.json` 固定 SDK；`dotnet-tools.json` 固定 Fable / Fantomas。

### 常用命令

```bash
npm run build          # Fable → dist/
npm test               # unit（tests/unit）
npm run test:harness   # harness 自检
npm run test:e2e       # canary 单轮
npm run check          # gate:static + build + test + harness
npm run check:release  # check + e2e×3 + npm pack --dry-run
npm run lint           # Fantomas / XML 格式化（提交前）
```

内部 `gate:*` 脚本由 `gate:static` 聚合，日常优先用 `check` / `check:release`。

### 构建与测试分层

| 层 | 命令 | 含义 |
|----|------|------|
| 0 静态 | `npm run gate:static` | layout / ssot / conformance / architecture / … |
| 1–3 unit | `npm test` | 纯函数、契约、Fake Host 轨迹 |
| harness | `npm run test:harness` | mock 森林与隔离自检 |
| e2e | `npm run test:e2e` | 真实场景 canary |
| 发布 | `npm run check:release` | 全链 + 三轮 e2e + pack dry-run |

`test` 在 `dist` 陈旧时 fail closed：先 `npm run build`。

### 规范与资源

- 产品语义：[`spec/00.md`](spec/00.md) 导航，条款 ID 寻址
- 合规账本：[`spec/conformance.toml`](spec/conformance.toml)（生成 [`spec/conformance.md`](spec/conformance.md)）
- 运行时资源：[`resources/prompts/`](resources/prompts/)、[`resources/enforcer/catalog.json`](resources/enforcer/catalog.json)
- 架构 DNA 与工程纪律：[`AGENTS.md`](AGENTS.md)、[`docs/decisions/kolmogorov.md`](docs/decisions/kolmogorov.md)
- 未来设计：[`docs/rfcs/`](docs/rfcs/)

### 提交与发布

1. `npm run lint`
2. `npm run check`（改动涉及 e2e 则 `test:e2e` 或 `check:release`）
3. 优先 stage 具体文件；保留 hooks，不用 `--no-verify`
4. 版本与用户可见变化写入 [CHANGELOG](CHANGELOG.md)
5. 发布前：`npm run check:release`，再 `npm pack`；tarball 应仅含 `dist/` + `resources/` 及 manifest 元数据

更多：[`docs/development.md`](docs/development.md)、[`docs/releasing.md`](docs/releasing.md)、[`docs/architecture.md`](docs/architecture.md)。

### 许可证

本项目为专有商业软件（proprietary）。`package.json` 中 `private: true`，`license` 为 `SEE LICENSE IN LICENSE`。
