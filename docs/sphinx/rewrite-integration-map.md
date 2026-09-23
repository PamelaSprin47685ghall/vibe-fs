# Sphinx clean-break 接入清单（WP-00 产物）

本文件是实施过程记录，对应 `proposals/Sphinx.md` 第 23 章。所有路径与符号均在真实仓库核对过，不是猜测。

## 真实 Sphinx 目录与编译清单

- 源码根：`src/Wanxiangshu/Sphinx/`（对应提案的 `$SPHINX/`）。
- Host 侧接入：`src/Wanxiangshu/OpenCode/Host/Sphinx*.fs`、`src/Wanxiangshu/OpenCode/Tools/SphinxTool.fs`、`src/Wanxiangshu/OpenCode/Plugin/Sphinx*.fs`。
- 编译采用 owner-locality 分片 `.fsproj`，由 `src/Wanxiangshu/compile-order.txt` 声明权威顺序：

| 分片 | 覆盖内容 |
|---|---|
| `Wanxiangshu.Owner.epistemic-reasoning.sphinx-event-vocabulary-contract.fsproj` | `Sphinx/EventVocabulary.fs` |
| `Wanxiangshu.Owner.epistemic-reasoning.sphinx-runtime.fsproj` | 54 对旧生产文件的主体 |
| `Wanxiangshu.Owner.epistemic-reasoning.sphinx-integration-rules.fsproj` | `Sphinx/Inquiry.fs`、`Sphinx/IntegrationRules.fs` |
| `Wanxiangshu.Owner.epistemic-reasoning.sphinx-serve-runtime.fsproj` | InquiryRuntime、McpServer、Surface、GenericDurability |
| `Wanxiangshu.Owner.epistemic-reasoning.sphinx-serve-entry.fsproj` | `Sphinx/ServeEntry.fs` |
| `Wanxiangshu.Owner.epistemic-reasoning.sphinx-mcp.fsproj` | `Sphinx/Mcp.fs` |
| `Wanxiangshu.Owner.epistemic-reasoning.sphinx-tool.fsproj` | OpenCode 宿主 execution/tool/surface |
| `Wanxiangshu.Owner.host-boundary.sphinx-host-adapter.fsproj` | OpenCode 宿主 MCP 配置注入 |

新目录按同一风格建分片，`compile-order.txt` 是权威顺序，缺项会直接报错。

## 权威事件与投影 owner

- `IEventStore` 接口：`src/Wanxiangshu/Persistence/EventStore/Port.fsi`（`Append`、`WritePayload`、`ReadPayload`、`TryCurrent`、`TryEvent`、`TryHeads`、`TryHead`、`AllHeads`）。
- `IntegrationRule` / `ICanonicalIntegrator`：`src/Wanxiangshu/Persistence/EventStore/IntegrationKernel.fsi`。
- 注入点：`CanonicalIntegrator.createWithRules`（`src/Wanxiangshu/Persistence/EventStore/CanonicalIntegrator.fsi`），`CanonicalIntegrator.baseRules` 是结构规则 + journal fold，域规则由 owner 显式传入。
- 已知事件白名单：`AuthoritativeEventTypes.isKnown`（`src/Wanxiangshu/Persistence/EventStore/AuthoritativeVocabulary.fs`）。
- 本地工厂：`EventStore.createLocal commonDir writerId integrator`。
- Sphinx 旧注入点：`Sphinx/ServeEntry.fs` 的 `serveDurableDir`；新 v2 注入点必须是同一个 canonical owner，不新建存储。

## EventStore 原子性现状（实施必须实测）

接口层只提供 `Append: EventEnvelope list -> Task<Result<AppendReceipt, AppendError>>`。单个 `EventEnvelope` 是否原子、receipt 是否 durable、cut 语义如何，需在 WP-04 用故障注入测试确认。在确认前，Sphinx v2 采用「一个 TransitionBatch 封在一个 canonical EventEnvelope 内」的折中，不假设 `Append [e1;e2;e3]` 天然业务原子。

## Host / 会话 / capacity owner

- 受管 session 生命周期与 capacity：`src/Wanxiangshu/Host/` 与 `src/Wanxiangshu/Execution/Session/`。
- Sphinx 复用点：`OpenCode/Tools/SphinxTool.fs` 通过 `ISphinxEngineerPort` 调用共享 delegate runtime（`Execution/Delegation/SyncDelegate`），不新建 session 池。
- Sphinx 工作区事件存储：`OpenCode/Host/WorkspaceEventStore.fs` + `RuntimePath.gitCommonDir`。
- 真实 Host receipt 的判定必须落在实际 adapter 输出上，不允许用 Runtime 生成的 hash 冒充 childSessionId。

## 共享预算 owner

旧实现把预算语义放在 `Sphinx/TurnBudget.fs`（`expectTurns → 1.44/(expectTurns-1)^2`），由 `Sphinx/Inquiry.fs` 的 `RootBudget` 建模。该价格模型随旧内核一起删除；新 v2 由 `Core/Budget.fs` 的资源账本承担，成本来自 Host/provider 实际用量。

## 公开 JS surface 与测试

- 生成面清单：`scripts/lib/test-surface-scan.mjs` 的 `SURFACE_MANIFEST`（当前登记 `Sphinx/Surface.js`、`Sphinx/InquirySurface.js`、`Sphinx/GecSurface.js`、`OpenCode/Host/SphinxExecutionSurface.js`、`OpenCode/Plugin/SphinxCommandSurface.js`）。
- 旧测试入口：`requirements/epistemic-reasoning/tests/*.test.mjs` + `support.mjs` / `gec-support.mjs`，直接 import `dist/Sphinx/Surface.js`、`dist/Sphinx/GecSurface.js`、`dist/Sphinx/InquirySurface.js`。
- 统一 runner：`requirements/verification-system/tests/run.mjs`（unit）、`.../integration/run.mjs`；打包检查 `scripts/verify-package.mjs`；格式门禁 `wireit` + fantomas。
- 构建：`npm run build` → `node scripts/build.mjs`（Fable），产物 `dist/`。

## 发布入口

- `package.json` 只有 `.` → `./dist/OpenCode/Plugin/Plugin.js`，无 bin。
- MCP 入口 `Sphinx/Mcp.fs`：`relativeServerEntry = "dist/Sphinx/ServeEntry.js"`，工具前缀 `sphinx_`，权限键 `sphinx_*`。
- 内存回退：`ServeEntry.serveDefault` 在缺 `SPHINX_COMMON_DIR` 时退回 `Session.defaultStore`。新实现必须在此处显式失败。

## 规范关系

`requirements/epistemic-reasoning/` 是当前 Sphinx 的规范 owner，`WHAT[epistemic-reasoning-0NN]` 锚点被源码注释和测试引用。clean-break 只替换其中属于旧内核的条款，新条款在 `requirements/sphinx-v2/` 登记 supersede 关系，不删除历史数据，不改其它功能的规范。
