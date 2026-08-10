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

## Meditator 边界（见 AGENT-025）

Meditator 正式工具面只有 SyncDelegate `inspector`。所有权边界：

1. Host-final permission 与 ToolRegistry 两层都必须拒绝 Meditator 的 `read` / `glob` / `grep` /
   write/edit/executor/coder/fork/join/list/PTY/network（与 AGENT-006/025 同集）。
2. Epistemic style 只属于 Meditator system prompt / prompt ownership，**不是** LearningState、QA、
   Compile、RequestKind 或 final `return` 协议；禁止为 Meditator 新造 Student 式双门 profile。
3. 不得把 `student`/`teacher` 旧名 alias 到 Meditator（AGENT-004）。
