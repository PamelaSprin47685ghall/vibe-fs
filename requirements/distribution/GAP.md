# distribution — GAP

## GAP-DIST-001: 打包资源与活动注册同步闭包

- **现状**：资源目录与 surface 导出中仍存在历史角色文件；需在打包与门禁脚本中全面同步新工具 surface 并清除废弃注册。
- **目标合同**：DISTRIBUTION-010 保证打包资源与活动注册严格同步，无废弃活跃注册与死资源。
- **影响范围**：`resources/`、`scripts/checks/`、`package.json`。
