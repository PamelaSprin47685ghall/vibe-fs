# Agent — 所有权与边界

角色矩阵与工具语义见 `what/agent.md`。

## AGENT-007：工具权限双层边界

两层都必须存在，且都只读同一个 `AttemptExecutionProfile`：

| 层 | 职责 |
|----|------|
| Host-final Agent permission | 无权工具不进入 provider-visible schema |
| ToolRegistry execution gate | Host 配置异常时仍拒绝越权执行 |

普通角色从 `CanonicalRole` 取固定工具集；Student 从
`CanonicalRole × RequestKind` 取 AGENT-020 的两种工具面。Role、RequestKind 或 profile 无法确定
→ 模型可见插件工具集为空。
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

## AGENT-021：Student request-specific 双门

Student 每次请求的 provider schema 与 ToolRegistry execution gate 必须消费同一个不可变
`AttemptExecutionProfile.ToolCapabilitySet`。`StudentLearn` / `StudentCompile` 的 Host Session permission
是该 profile 的 wire 投影，不是第二份权限来源。

切换到编译必须在发送 continuation 前先构造完整 profile 并安装整套 permission；任一步失败都不得发送
一个工具面不完整的请求。执行时 `ToolContext.messageID` 必须命中该 attempt；旧 Learn attempt 伪造
`return`、Compile attempt 伪造 `teacher` 均 fail closed。

## Student SKILL 制品门（AGENT-022）

Student runtime 是制品形态门的唯一所有者。执行 gate 从当前 StudentCompile attempt 的 write/edit 参数中
取得路径，只接受 `.agent/skills/<skill-name>/SKILL.md`，并在 StudentRun 中记录解析后的绝对目标与目录名；
它不从文件正文反推 skill 身份。

最终 `return` 对记录集合做全量、fatal UTF-8 读取，并以目录名校验 frontmatter `name`、非空
`description` 与正文。集合为空或任一校验失败时 fail closed；QA 删除只能发生在全量校验之后。
