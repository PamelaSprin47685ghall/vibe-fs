# Agent — 所有权与边界

角色矩阵与工具语义见 `what/agent.md`。

## AGENT-007：工具权限双层边界

两层都必须存在，且都只读同一个 `AttemptExecutionProfile`：

| 层 | 职责 |
|----|------|
| Host-final Agent permission | 无权工具不进入 provider-visible schema |
| ToolRegistry execution gate | Host 配置异常时仍拒绝越权执行 |

角色从 `CanonicalRole` 取固定工具集（AGENT-006）。Role 或 profile 无法确定 → 模型可见插件工具集为空。
禁止「role unresolved 时暂时允许 inspector」类放行。

本条只约束**角色工具**。Host 元权限（`external_directory`、`doom_loop`、`question` 等）不进 `ToolPermission` / AGENT-006。

## AGENT-019：`external_directory` 写入边界

`external_directory` 是 Host 路径边界元权限，不是角色工具。

每一个 managed agent（AGENT-002 的 20 名）的 Host-final permission 必须显式：

```text
external_directory = "allow"
```

且排在 Host 默认 `external_directory:* = ask` **之后**（flat merge + `findLast`），使任意外部 path 求值为 allow。

**唯一生产写入点**：`StaticTools.permissionObj` → `ManagedAgentConfig.applyOwnedFields`。  
禁止第二处散落覆盖。

禁止：

1. 省略覆盖、依赖用户 always-allow  
2. 编入 `Roles.permissions` / `ToolPermission` / AGENT-006  
3. 用全局 `permission: { external_directory: "allow" }` 顶替 agent 级写入  
4. 借本条放宽 bash / write / edit 等角色工具  

验证：用 Host ruleset 语义证明每个 managed agent 对任意外部 path 为 allow。

## Companion 资格边界

Companion 是否存在由 Session 种类决定，不由 Role、Tier、工具面或当前 Logical Run 决定（COMPANION-001/002）。  
Agent 矩阵不得隐含「某角色无 Companion」。

## AGENT-021：（空缺）Student request-specific 双门 — G3 已删除

**编号永久空缺。** StudentLearn / StudentCompile 双门与 `teacher` / Compile `return` 协议已删除。
不得为 Meditator 重建 request-kind 双门；见 AGENT-025。

## SyncDelegate 边界（见 AGENT-024）

SyncDelegate 工具（`inspector` / `coder` 作为 dedicated callee）的 Session 所有权与双 await 生命周期
属 HOST-008 / EXEC-026/028，不由 Agent 角色矩阵另立一套 parent/child map。DAG 与
`InvocationMode` 定义见 `what/agent.md` AGENT-024。

边界：

1. DAG 边仅 AGENT-024 what 所列为合法；配置不得暴露未列边的 sync 工具面。
2. `InvocationMode = SynchronousDelegate` 给 callee 增加的 `return` 只经 AttemptExecutionProfile 投影；
   禁止用可变「阶段 / PC」槽位表达是否已 return。
3. Owner effective tier → 确定性 delegate tier（`fast→fast`，`deep→deep`）；模型不可每轮选 Agent。
4. Teacher leaf / no-Companion 拓扑**不**套到 Dedicated Inspector/Coder（后者是 Work + Attached，HOST-008）。
   Student/Teacher 角色已删除（AGENT-020 空缺）。

## Meditator 边界（见 AGENT-025 / AGENT-028）

Meditator 正式工具面 = SyncDelegate `inspector` + Host MCP `sphinx_*`。所有权边界：

1. Host-final permission 与 ToolRegistry 两层都必须拒绝 Meditator 的 `read` / `glob` / `grep` /
   write/edit/executor/coder/fork/join/list/PTY/`stealth-browser-mcp_*`（与 AGENT-006/025 同集）。
   `sphinx_*` 仅经 `ToolPermission.Sphinx` 放行（AGENT-028）；仍禁止 filesystem 直读。
2. Epistemic style 只属于 Meditator system prompt / prompt ownership，**不是** LearningState、QA、
   Compile、RequestKind 或 final `return` 协议；禁止为 Meditator 新造 Student 式双门 profile。
3. 不得把 `student`/`teacher` 旧名 alias 到 Meditator（AGENT-004）。
4. Sphinx 认识状态 / Closure / Canonical Answer 的 writer 在 `src/sphinx`（SPHINX-005）；
   万象术只写 identity / launch / permission。

## stealth-browser MCP 所有权（见 AGENT-026）

| 面 | writer | 不可写 |
|----|--------|--------|
| 服务器名 / 工具前缀 / uvx command / `isTool` | `Kernel/StealthBrowserMcp.fs` | env、Host 对象 |
| 启动判定（disabled / fixture / uvx）+ `config.mcp` 写入 | `StealthBrowserMcpConfig` ← `ManagedAgentConfig.applyOwnedFields` | agent permission 矩阵 |
| Host schema `stealth-browser-mcp_*` allow/deny | `StaticTools.permissionObj` ← `ToolPermission.Network` | 第二套 role→MCP 表 |

禁止第二处注入 `mcp.stealth-browser-mcp`。禁止把 MCP 工具名写进 `Roles.permissions` 字符串集；域能力只留 `ToolPermission.Network`。

## Sphinx MCP 所有权（见 AGENT-028）

| 面 | writer | 不可写 |
|----|--------|--------|
| 服务器名 / 工具前缀 `sphinx_` / 生产 node 入口 / `isTool` | `Kernel/SphinxMcp` identity | env、Host 对象、Sphinx 闭包副本 |
| 启动判定（disabled / fixture / test / 生产）+ `config.mcp.sphinx` 写入 | `SphinxMcpConfig` ← `ManagedAgentConfig.applyOwnedFields` | agent permission 矩阵 |
| Host schema `sphinx_*` allow/deny | `StaticTools.permissionObj` ← `ToolPermission.Sphinx` | 第二套 role→MCP 表 |
| EpistemicState / Closure / Canonical Answer | `src/sphinx` Inquiry Kernel | 万象术 domain、Meditator prompt |

禁止第二处注入 `mcp.sphinx`。禁止把 MCP 工具名写进 `Roles.permissions` 字符串集；域能力只留 `ToolPermission.Sphinx`。禁止万象术内嵌闭包逻辑。

## Semble MCP 所有权（见 AGENT-027）

| 面 | writer | 不可写 |
|----|--------|--------|
| 服务器名 / uvx / fixture / Launch / `launchFrom` / Hit | `Kernel/SembleMcp.fs` | env I/O、Host 对象 |
| MCP payload → Hit | `SembleSearchCodec` | spawn |
| stdio JSON-RPC `tools/call` | `SembleMcpStdio` | Host mcp、MCP SDK |
| search 编排 | `SembleMcpClient` | `StrengthSpeculate`、`ManagedAgentConfig` |

禁止第二处 spawn。禁止 Host config hook 写入 semble。禁止把 Semble 编入 AGENT-006 / permission schema / ToolRegistry / `js-*`。
