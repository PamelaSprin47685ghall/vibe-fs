# external-investigation — HOW

## 演进与清理架构

`external-investigation` 包已转为撤销声明与演进记录，所有历史功能条款均已废止：

| 旧条款编号 | 旧条款主题 | 演进状态 | 新规范落点 |
| --- | --- | --- | --- |
| EXTERNAL-INVESTIGATION-001 | 外部事实溯源建立 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-002 | 可达性不决定所有权 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-003 | 外部证据远岸属性 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-004 | 优先接近源头来源 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-005 | 视觉特有事实观察 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-006 | 限定条件严格保留 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-007 | 推断不冒充直接观察 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-008 | 来源分歧如实保留 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-009 | 确定性受限于源头 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-010 | 外部与本地权限隔离 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-011 | 外部事实不转为义务 | 撤销 | 无（功能彻底删除） |
| EXTERNAL-INVESTIGATION-012 | 角色与集成全链撤销 | 新增 | 负向撤销断言 |
| EXTERNAL-INVESTIGATION-013 | 职责不转交与禁复活 | 新增 | 负向防御断言 |

## 物理清理与负向防御

1. **集成项清除**：
   - 清理所有专用实现文件（`StealthBrowserMcp.fs/.fsi`、`StealthBrowserMcpConfig.fs/.fsi` 等）；
   - 清除角色提示词目录 `resources/provider/role/browser/`；
   - 清除 `ManagedAgentConfig` 中的 Browser 注入与 `StaticTools` 中的 Browser MCP 映射。
2. **负向测试保护**：
   - 负向测试 `stealth-browser-role-lock.test.mjs` 验证系统中所有活跃角色均无 Browser MCP 权限，且无 Browser 角色配置；
   - 确保未来没有空壳角色或兼容别名引入。

## DEPENDS ON

- `office-capability`
- `capability-enforcement`
