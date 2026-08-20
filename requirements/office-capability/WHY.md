# office-capability — WHY

## 领域动力与核心张力

系统内协作的基础在于将工作托付给有资格产生对应后果的 Office。核心张力在于**后果模型（Consequence Model）**与**表面清单（工具白名单/Persona 名称）**之间的区分：

```text
有权产生的后果 (Entitled Consequence) ──► Coder 改变代码 / Inspector 确立既有事实证据 / DevOps 行动并获取运行证据
表面投影 (Projections)               ──► 权限矩阵 / 工具列表 / Prompt 文案
```

若以工具可达性或 Persona 名字来定义 Office，会诱发严重退化：
- **工具清单冒充权能**：认为 Inspector 只是「权限受限的 Coder」，或将 DevOps 视为「万能逃生通道」。
- **认知边界漂移**：调用方因看不清被委托方的 Role Law，把修复代码的任务错误指派给 Inspector，或将外部调研混同于本地调查。

`office-capability` 的核心不变量：
- **后果定义职位**：Office 仅由其有权产生的后果（Entitled Consequence）及其明确禁止的非后果（Non-consequence）定义。
- **五分法正交性**：Manager 可 fork 的五类 Office（Coder、Inspector、DevOps、Browser、Inquiry）构成完备正交的权能模型。
- **单一语义所有权、多处投影**：同一后果事实在 Manager Role Law、fork 描述、各 Office 自我模型中保持严格一致，不得漂移。
- **不可互换性**：各 Office 之间严禁作为通用代理相互替代。

## 破裂后果

- Office 职责边界模糊重叠，产生越权操作或无效托付。
- 同一 Office 的权能在不同提示词与工具描述中产生歧义。
- 参与者根据工具列表反推职责，破坏系统分层保证。

## 边界与关系

- `participant-identity`：提供 Role 身份定义；本包定义各 Role 有资格产生的后果。
- `capability-enforcement`：负责将后果模型投影为 Host schema 与运行时执行 gate。
- `delegation`：消费本包的后果模型以执行按后果托付。
- `participant-horizon` 与 `action-affordance`：引用本包的后果定义组织认知视界与动作契约。

## DEPENDS ON

- `participant-identity`
