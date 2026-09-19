# external-investigation — HOW

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
