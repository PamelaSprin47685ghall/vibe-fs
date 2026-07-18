# 17 — 构建、测试与验证

## 环境

- .NET SDK（`net10.0`，`Directory.Build.props`）
- `dotnet tool restore` → Fable
- Node.js + npm

## 命令

| 命令 | 作用 |
| :--- | :--- |
| `npm run build` | Fable → `build/`，`postbuild.mjs` 整理 package |
| `npm test` / `npm run test:unit` | `node tests/runner.js` |
| `npm run test:integration` | `node tests/integration.js` |
| `npm run test:e2e:opencode:p0` | `node tests/e2e.js opencode-e2e-p0` |
| `npm run test:e2e:opencode:full` | `node tests/e2e.js opencode-e2e-full` |
| `npm run validate:coverage` | 运行 `e2e/opencode/scripts/validate-manifest.mjs`，校验 `behavior-coverage.ts` |
| `npm run build-and-test` | 构建 + `tests/runner.js` 全量测试（见 `AGENTS.md`） |
| `npm run build-and-test-e2e` | 构建 + E2E 编排脚本 |
| `npm run format` | 预提交格式化 |

产物为 `build/src/**/*.js`；F# 测试编译为 `build/tests/Tests.js`，由 `tests/runner.js` 加载。

## 测试分层

| 层 | 位置 | 说明 |
| :--- | :--- | :--- |
| 纯内核 | `tests/*Tests.fs` | 无 IO，时间无关 |
| Runtime / Codec | `tests/` codec 条目 | 边界解析 |
| 行为/契约 | `*Tests.fs`、`Integration*`、`e2e/` | 公共输入、输出与状态事实 |
| 集成 | `Integration*`、`IntegrationToolSpecCatalog` | 工具契约 |
| OMP 专项 | `ompTestEntries` | 扩展边界 |
| 万象阵 | `wanxiangzhenTestEntries` | 协调器（若启用） |
| OpenCode E2E | `e2e/opencode/` | `opencode serve`、HTTP、事件探针与 strict mock；通过 `tests/e2e.js` 选择 P0/full |
| 兼容/契约 harness | `integration/`、`e2e/Tests*.fs`、`e2e/*-harness.js` | 按各测试入口运行，不等同于真实 OpenCode E2E |

`tests/runner.js` 调用 `runAll(selectors)`；可传前缀过滤用例。

新增纪律应优先加行为测试，再加实现；测试断言公共输入、输出、事件或状态，不断言源码布局与模块引用。

## E2E

- OpenCode P0/full：`e2e/opencode/` + `tests/e2e.js`
- 万象阵：`e2e/wanxiangzhen-harness/`
- Mux/OMP：`e2e/mux-harness.js`、`e2e/omp-harness.js`
- 时间无关：轮询事件与状态，不用固定 sleep（见 [01-first-principles.md](./01-first-principles.md) §7）

## 非功能需求与架构不变量

由行为测试、编译器与运行时共同约束：

| 类别 | 规约 |
| :--- | :--- |
| Kernel 纯度 | F# 编译边界与纯函数行为测试 |
| 宿主隔离 | 各宿主公开插件行为与集成测试 |
| 文件体量 | 设计审查与自然模块边界，不删空行规避复杂度 |
| Codec | 输入解码、结果编码与 hook 输出契约测试 |
| Fallback | 可重复状态转移、事件重放与集成测试 |
| 性能纪律 | 热路径禁无界 `@` 列表追加（见 [10](./10-message-transform.md)） |
| SSRF | `webfetch` 经 `Kernel.WebFetchGuard`：私网、回环、链路本地、元数据地址等拒绝 |

EventLog 目标（实现与压测以代码为准）：万行级 replay 与单次 append 宜保持毫秒级；见 `EventLogRuntimeTests`。

## 验证矩阵（摘要）

| 层级 | 代表模块 |
| :--- | :--- |
| 单元 | `ReviewTests`、`EventLogFoldTests`、`FallbackKernelTests` |
| Codec | `ToolArgsDecodeTests`、`WorkBacklogToolsCodecTests` |
| 行为 | `TestsEntries*`、`Integration*`、`e2e/` |
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
