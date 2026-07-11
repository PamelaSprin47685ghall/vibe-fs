# 17 — 构建、测试与验证

## 环境

- .NET SDK（`net10.0`，`Directory.Build.props`）
- `dotnet tool restore` → Fable
- Node.js + npm

## 命令

| 命令 | 作用 |
| :--- | :--- |
| `npm run build` | Fable → `build/`，`postbuild.mjs` 整理 package |
| `npm test` | `node tests/runner.js` |
| `npm run build-and-test` | 构建 + 全量测试（约 20s，见 `AGENTS.md`） |
| `npm run build-and-test-e2e` | 含 e2e |
| `npm run format` | 预提交格式化 |

产物：`build/src/**/*.js`；测试加载 `build/tests/Tests.js`。

## 测试分层

| 层 | 位置 | 说明 |
| :--- | :--- | :--- |
| 纯内核 | `tests/*Tests.fs` | 无 IO，时间无关 |
| Shell / Codec | `tests/` codec 条目 | 边界解析 |
| 架构探针 | `ArchitectureTests*.fs` | 源码文本/结构不变量 |
| 集成 | `Integration*`、`IntegrationToolSpecCatalog` | 工具契约 |
| OMP 专项 | `ompTestEntries` | 扩展边界 |
| 万象阵 | `wanxiangzhenTestEntries` | 协调器（若启用） |
| E2E | `e2e/` | harness + mock LLM |

`tests/runner.js` 调用 `runAll(selectors)`；可传前缀过滤用例。

## ArchitectureTests 主题（摘要）

注册于 `TestsArchitectureRegistry.fs` + `PartB`：

- Kernel/Shell 分层、文件行数、无 Dyn in Kernel
- Opencode↔Mux 无交叉引用、Omp 隔离
- EventLog fold 强制用于 nudge
- MessageTransform 用 projection + caps cache
- Tool catalog / wire hook / wire pipeline
- Subagent、executor、tree-sitter 边界
- `parallelToolPromptSSOTGuard`、`noQuadraticListAppend`
- Omp 全工具注册、子会话、review runtime
- 万象阵：`wanxiangzhenBoundary`、`wanxiangzhenGitQueue`、`wanxiangzhenReconcile`（`ArchitectureTestsFoundationB.fs`）
- Fallback：`fallback_continue_injected` kind 声明（`ArchitectureTestsFallback.fs`）
- E2E：`e2e/OpencodePluginTests.fs`（`pty_spawn` 注册契约）

**新增纪律**应优先加架构测试，再加实现。

## E2E

- `e2e/wanxiangzhen-harness/` 等
- 时间无关：轮询状态而非固定 sleep（见 [01-first-principles.md](./01-first-principles.md) §7）

## 非功能需求与架构不变量

由 `ArchitectureTests*` 与内核模块共同约束（原 master-spec §5 并入）：

| 类别 | 规约 |
| :--- | :--- |
| Kernel 纯度 | `src/Kernel/` 禁止 Shell、Dyn、Node、`JS.Promise` |
| 宿主隔离 | Opencode↔Mux 禁互引；Omp 禁 Opencode/Mux/`engine/` |
| 文件体量 | `src/`、`tests/` 单文件有效行 ≤300（200 起警惕） |
| 标记禁令 | 禁止 TODO/FIXME/HACK 等悬空标记（探针扫描） |
| Codec | 宿主 tool args 必经 Shell codec；禁业务层裸 `Dyn.get` |
| Fallback | Fallback 路径禁 `setTimeout`/`setInterval`/`Date.now` |
| 性能纪律 | 热路径禁无界 `@` 列表追加（见 [10](./10-message-transform.md)） |
| SSRF | `webfetch` 经 `Kernel.WebFetchGuard`：私网、回环、链路本地、元数据地址等拒绝 |

EventLog 目标（实现与压测以代码为准）：万行级 replay 与单次 append 宜保持毫秒级；见 `EventLogRuntimeTests`。

## 验证矩阵（摘要）

| 层级 | 代表模块 |
| :--- | :--- |
| 单元 | `ReviewTests`、`EventLogFoldTests`、`FallbackKernelTests` |
| Codec | `ToolArgsDecodeTests`、`WorkBacklogToolsCodecTests` |
| 架构 | `TestsArchitectureRegistry` |
| 集成 | `IntegrationToolSpecCatalog`、`IntegrationMuxMethodologySpecs` |
| E2E | `e2e/` + `wanxiangzhen-harness` |

## 实施顺序（AGENTS.md）

文档 → 测试 → 代码（durable 状态以 `.wanxiangshu.ndjson` 为 SSOT）。

## 清理

```bash
rm -rf build artifacts
```

中间 MSBuild 输出在根 `artifacts/`。

## 相关文档

- [02-architecture.md](./02-architecture.md)
- [18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md)