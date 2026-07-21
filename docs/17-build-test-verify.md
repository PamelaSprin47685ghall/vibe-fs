# 17 — 构建、测试与验证

## 环境

- .NET SDK（`net10.0`）
- `dotnet tool restore` → Fable
- Node.js + npm

## 命令

| 命令 | 作用 |
| :--- | :--- |
| `npm run build` | Fable → `build/` |
| `npm test` / `npm run test:unit` | `node tests/runner.js` |
| `npm run build-and-test` | 构建 + 全量测试（约数十秒） |

## 测试分层

| 层 | 位置 | 说明 |
| :--- | :--- | :--- |
| 纯内核 | `tests/*Tests.fs` | 无 IO，时间无关 |
| Runtime / Codec | `tests/` codec 条目 | 边界解析 |
| 行为/契约 | `*Tests.fs`、`Integration*` | 公共输入/输出/状态事实 |
| 集成 | `Integration*` | 工具契约 |
| OMP 专项 | `ompTestEntries` | 扩展边界 |
| OpenCode E2E | `e2e/opencode/` | `opencode serve`、HTTP 探针 |

新增纪律：优先加行为测试，再加实现；测试断言公共输入、输出、事件或状态，不断言源码布局。

## 非功能需求

- Kernel 纯度：F# 编译边界与纯函数行为测试
- 宿主隔离：各宿主公开插件行为与集成测试
- SSRF：`webfetch` 经 `Kernel.WebFetchGuard` 拒绝私网/回环
- 热路径禁无界 `@` 列表追加
