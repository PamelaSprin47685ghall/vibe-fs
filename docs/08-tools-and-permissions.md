# 08 — 工具目录与权限

## 两层 SSOT

| 层 | 模块 | 内容 |
| :--- | :--- | :--- |
| 语义与文案 | `Kernel.ToolCatalog` | `ToolSpec`：name、description、paramDocs、requiredFields |
| 执行与 schema | `Shell.*ToolsCodec` + 宿主 `Tools.fs` | Zod / TypeBox / Mux JSON schema 生成与 `execute` |

架构测试要求：每个工具族在 **Catalog + Shell codec** 双处维护，禁止宿主私自复制 description。

## 核心内置工具（Registry.all）

非穷举，以 `src/Kernel/ToolCatalog/Registry.fs` 为准：

- **子代理**：`coder`、`investigator`、`browser`、`continue`
- **审查**：`submit_review`、`return_reviewer`
- **文件**：`read`、`write`（及 edit/patch 族在 Classification）
- **搜索**：`fuzzy_find`、`fuzzy_grep`、`fuzzy_continue`
- **网络**：`websearch`、`webfetch`
- **执行**：`executor`、`executor_wait`、`executor_abort`

**方法论**：`methodology_<id>` 不在 `Registry.all` 单列表中，而由 `Methodology/Registry` 批量注册（见 [09-methodology.md](./09-methodology.md)）。

## 角色矩阵（摘要）

| 角色 | 典型允许 | 典型禁止 |
| :--- | :--- | :--- |
| Manager | todowrite/task、子代理、submit_review | 直接 fuzzy_grep、随意 shell |
| Coder | read/write/edit、patch、executor | 委派 web/submit |
| Investigator | read、fuzzy_*、executor(RO) | 写族工具 |
| Meditator | read、methodology_* | 写族 |
| Browser | browser、websearch | 改文件 |
| Reviewer | read、return_reviewer | 改文件 |
| Executor | executor* | 改工作区文件 |

精确表以 `ToolPermission.fs` 为准。

## 权限模型

`ToolPermission.classifyTool` → `ToolSemantic`；`canUseForHost` 结合角色与宿主。

- 用 **Set 精确匹配** 为主，避免子串误判
- Mux 工具名先 `HostTools.normalizeToolNameForMux`（如 `file_read` → `read`）

## HostTools 命名矩阵（摘要）

| 概念 | Opencode | Mimocode | Mux | Omp |
| :--- | :--- | :--- | :--- | :--- |
| 待办写入 | todowrite | task | todowrite | todowrite |
| 子代理 task 工具 | task | actor | task | task |

`isTodoWriteTool` 使用预构建 `Set` 做 O(log N) 成员检测。

## 子代理工具参数

- 多意图并发：`intents[]`，每项含 `objective`、`background`、`targets[]`（`file`+`guide`）
- TDD：`tdd` = red | green，带 warn 确认字段
- 架构测试：`opencodeHookSchemaUsesIntentsRawFromArgs` 等保证 schema 与执行一致

## 特殊工具行为

| 工具 | 说明 |
| :--- | :--- |
| `continue` | 续跑子代理会话 iterator（见 [11-subagents.md](./11-subagents.md)） |
| `submit_review` | 仅 loop 活跃时有效；与 review FSM 联动 |
| `return_reviewer` | 仅 reviewer 角色；驱动 `review_verdict` |
| `fuzzy_continue` | 搜索 iterator 翻页 |
| `amend` | 工具参数纠偏回溯（见 [10-message-transform.md](./10-message-transform.md)） |

## warn_tdd / warn_reuse

### WarnTdd / warn（契约）

**修改类工具**须 `warn_tdd`，规范值：

`i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated`

**高风险执行**（`executor`、`pty_*`）另须 `warn`：

`it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it`

SSOT：`Kernel/WarnTdd.fs`、`ToolCatalog`；测试 `WarnTddKernelFactsTests`。

## Mux 特有包装

`Mux/Wrappers.fs`、`HostTools.fs`、`HostToolsFuzzy.fs`：将 Mux 原生工具名映射到万象术执行链。

## OMP 注册

`ArchitectureTests.ompToolsRegisterAll`：须注册与 Catalog 对齐的工具子集 + 全部 `methodology_*`。

## 执行分发（概念）

```text
toolName + args(obj)
  → ToolArgsDecode
  → ToolPermission 检查
  → ToolExecute / SubagentToolExecute / 方法论 handler
  → ToolResult 编码
```

## 相关文档

- [09-methodology.md](./09-methodology.md)
- [11-subagents.md](./11-subagents.md)
- [14-host-opencode.md](./14-host-opencode.md) / [15](./15-host-mux.md) / [16](./16-host-omp.md)