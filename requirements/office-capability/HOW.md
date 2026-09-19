# office-capability — HOW

## 架构与核心机制

`office-capability` 作为领域语义事实，由唯一 typed owner、静态门禁、提示词投影与运行时矩阵共同承载：

```text
Office Consequence Model (语义唯一事实源)
       │
       ├──► Foundation/OfficeCapability.fs (ToolPermission + exhaustive Role matrix)
       ├──► 提示词投影 (Manager Role Law, fork/resume description, 各 Office 自我模型)
       ├──► 静态门禁 (Gate F: scanOfficeCapabilityIntegrity 校验双语跨角色一致性)
       └──► 运行时权限投影 (经 capability-enforcement 落地为 Host schema 与执行 Gate)
```

1. **单一语义所有权与投影**：
   - `Foundation/OfficeCapability.fs` 唯一定义 `ToolPermission`、`permissions` 与 `isAllowed`。`Foundation/Roles.fs`/`RolesSurface.fs` 只拥有 identity vocabulary，不含 capability matrix。
   - `Participant/Persona/OfficeCapabilitySurface.fs` 把 typed consequence 投影为 JS-native label array；跨 owner 测试不读取 F# DU representation。
   - 域模型定义四大核心角色（Orchestrator, Manager, Engineer, DevOps）与内部辅助角色的 Entitled Consequence 与 Non-consequence 清单：
     - **Engineer**：拥有 Read, Write, Edit, Glob, Grep, Move, Remove, Fetch, BashHoneypot 以及全系统唯一的 **Fission** 权能；
     - **DevOps**：拥有 Read, Write, Edit, Glob, Grep, Move, Remove, Exec, Pty, Join, Horizon；具备角色固有非架构级自修授权，无 Fission；
     - **Manager**：拥有 Fork (仅 Engineer), Resume (已有 Engineer 续做或固定 DevOps), Join, Horizon, TodoWrite, ReviewAssessment, Finality；无 Fission；
     - **Orchestrator**：拥有 Fork/Commission (仅 Manager 道路), Join, Horizon；无 Fission。
   - 同一后果事实通过 `tests/005.test.mjs` 与 Role Law 契约测试，确保 Manager、fork/resume 工具描述及各角色 Role Law 中双语表达完全一致。

2. **不可互换性防护**：
   - 提示词与工具描述中明确携带各 Office 的负边界（negatives）。
   - 跨 Office 的越权调用在决策面被边界镜像（caller-facing boundary mirrors）拦截，在执行面被 ToolRegistry 门禁阻断。
   - Fission 准入仅放行已证明为 Engineer 的执行上下文，Manager、DevOps 及其他角色调用在入口即被 fail-closed 拦截。

## 资源工作链

系统资源覆盖共同法、六份角色说明、派工与 resume、Fission、Manager 阶段提示、结对指引、案例整理与读取、运行输出及完成纪律，中英文同步。Engineer 调查与实现不交接给另一个职位；DevOps 可作普通工程判断并直接修复；Manager 分清源码完成、最后改动后的运行证据和验收。

`PromptResources` 不将已撤销角色映射到 Engineer。公开 `PromptSurface` 只返回五个活跃角色；尚存于内部旧 record 的字段为空，不作为公开提示词槽位。DevOps 同时具备源码工程与资源调度能力。

资源和运行接点规范：Bookkeeper 两阶段加载同语言的共同法、角色法和阶段提示；JS 示例按 Engineer 的读写能力和 DevOps 的直接修复职责选择；原始输出只进数据字段，截断说明从双语资源加载。案例 freshness 仅说明关联文件与维护状态，不声称已经重新验证正确性。

`tests/005.test.mjs` 全量扫描分发资源并检查双语职责投影。实际装配由 `provider-language-012`（`provider-language/tests/012.test.mjs`）检查；案例、命令输出和生成示例另由其 owning package 的正式测试证明。字符串断言不代表模型行为 canary、运行时权限或完整迁移已经通过。

