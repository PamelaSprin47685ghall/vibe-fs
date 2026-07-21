# 08 — 工具目录与权限

## 两层 SSOT

| 层 | 模块 | 内容 |
| :--- | :--- | :--- |
| 语义与文案 | `Kernel/ToolCatalog/Registry.fs` | `ToolSpec`：name、description、paramDocs、requiredFields |
| 执行与 schema | `src/Runtime/Tooling/*` + 宿主 `Tools.fs` | Zod / TypeBox / Mux JSON schema 生成与 `execute` |

架构测试：每个工具族在 Catalog + Tooling codec 双处维护，禁止宿主私自复制 description。

## 核心内置工具（`Registry.all`）

`src/Kernel/ToolCatalog/Registry.fs`：

- **子代理**：`coder`、`inspector`、`browser`、`continue`
- **审查**：`submit_review`、`return_reviewer`
- **文件**：`read`、`write`、`edit`、`swap`
- **搜索**：`fuzzy_find`、`fuzzy_grep`、`fuzzy_continue`
- **网络**：`websearch`、`webfetch`
- **执行**：`executor`（**必填** `max_bytes`）
- **PTY**（OpenCode/Mimocode）：`pty_spawn`、`pty_write`、`pty_read`、`pty_list`、`pty_kill`
- **方法论**：`methodology_<id>` 批量注册（`Methodology/Registry.fs`）

## 角色矩阵

| 角色 | 典型允许 | 典型禁止 |
| :--- | :--- | :--- |
| Manager | todowrite/task、子代理、submit_review | 直接 fuzzy_grep、随意 shell |
| Coder | read/write/edit、patch、executor | 委派 web/submit |
| Inspector | read、fuzzy_*、executor(RO) | 写族工具 |
| Meditator | read、methodology_* | 写族 |
| Browser | browser、websearch | 改文件 |
| Reviewer | read、return_reviewer | 改文件 |

精确表以 `ToolPermission.fs` 为准。

## HostTools 命名矩阵

| 概念 | Opencode | Mimocode | Mux | Omp |
| :--- | :--- | :--- | :--- | :--- |
| 待办写入 | todowrite | task | todowrite | todowrite |
| 子代理 task 工具 | task | actor | task | task |

`normalizeToolNameForMux`（`HostTools.fs`）将 Mux 原生名映射到 canonical 名（`file_edit_*` → `edit`、`file_read` → `read`、`web_fetch` → `webfetch`、`web_search`/`google_search` → `websearch`）。

## 方法论注册

`src/Kernel/Methodology/Registry.fs` 从 `Catalog.all`（6 个条目模块聚合）派生 `enumValues`。单一派生保证三处不复制。

## warn_tdd / warn_reuse

`src/Kernel/WarnTdd.fs` + `src/Runtime/Tooling/ToolArgumentCoercion.fs`：key 存在 value 为 undefined 的软协议字段，注入 Schema description 强制 LLM 注意，宿主不剥离。
