# office-capability — HOW

## 架构与核心机制

`office-capability` 作为领域语义事实，由唯一 typed owner、静态门禁、提示词投影与运行时矩阵共同承载：

```text
Office Consequence Model (语义唯一事实源)
       │
       ├──► Foundation/OfficeCapability.fs (ToolPermission + exhaustive Role matrix)
       ├──► 提示词投影 (Manager Role Law, fork description, 各 Office 自我模型)
       ├──► 静态门禁 (Gate F: scanOfficeCapabilityIntegrity 校验双语跨角色一致性)
       └──► 运行时权限投影 (经 capability-enforcement 落地为 Host schema 与执行 Gate)
```

1. **单一语义所有权与投影**：
   - `Foundation/OfficeCapability.fs` 唯一定义 `ToolPermission`、`permissions` 与 `isAllowed`。`Foundation/Roles.fs`/`RolesSurface.fs` 只拥有 identity vocabulary，不含 capability matrix。
   - `Participant/Persona/OfficeCapabilitySurface.fs` 把 typed consequence 投影为 JS-native label array；跨 owner 测试不读取 F# DU representation。
   - 域模型定义五大可 fork 职位的 Entitled Consequence 与 Non-consequence 清单。
   - 同一后果事实通过 `office-capability-integrity.test.mjs` 确保 Manager、fork 工具描述及各角色 Role Law 中双语表达完全一致。

2. **不可互换性防护**：
   - 提示词与工具描述中明确携带各 Office 的负边界（negatives）。
   - 跨 Office 的越权调用在决策面被边界镜像（caller-facing boundary mirrors）拦截，在执行面被 ToolRegistry 门禁阻断。
