# office-capability — HOW

## 架构与核心机制

`office-capability` 作为领域语义事实，由静态门禁、提示词投影与运行时矩阵共同承载：

```text
Office Consequence Model (语义唯一事实源)
       │
       ├──► 提示词投影 (Manager Role Law, fork description, 各 Office 自我模型)
       ├──► 静态门禁 (Gate F: scanOfficeCapabilityIntegrity 校验双语跨角色一致性)
       └──► 运行时权限投影 (经 capability-enforcement 落地为 Host schema 与执行 Gate)
```

1. **单一语义所有权与投影**：
   - 域模型定义五大可 fork 职位的 Entitled Consequence 与 Non-consequence 清单。
   - 同一后果事实通过 `semantic-anchors.mjs` 中的 `OFFICE_CAPABILITY_ANCHORS` 锚点绑定，由 Gate F 确保 Manager、fork 工具描述及各角色 Role Law 中双语表达完全一致。

2. **不可互换性防护**：
   - 提示词与工具描述中明确携带各 Office 的负边界（negatives）。
   - 跨 Office 的越权调用在决策面被边界镜像（caller-facing boundary mirrors）拦截，在执行面被 ToolRegistry 门禁阻断。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| OFF-001 | `requirements/office-capability/tests/office-capability-integrity.test.mjs` |
| OFF-002 | `requirements/office-capability/tests/office-capability-integrity.test.mjs` |
| OFF-003 | `requirements/office-capability/tests/office-capability-integrity.test.mjs` |
| OFF-004 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-005 | `requirements/office-capability/tests/office-capability-integrity.test.mjs` |
| OFF-006 | `requirements/office-capability/tests/office-capability-integrity.test.mjs` |
| OFF-007 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-008 | `requirements/office-capability/tests/office-capability-integrity.test.mjs` |
| OFF-009 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-010 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-011 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-012 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-013 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-014 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
