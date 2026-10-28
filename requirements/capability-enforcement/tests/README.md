# capability-enforcement 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含系统各角色权限边界、静默门禁（Quiescence Gate）与执行租约的测试。

## 测试覆盖重点

- **001/003/004**：通过生产请求规划器验证各角色的可见能力集，确保窄通道请求的只读收窄生效。
- **002/006/007/009—011/023**：验证工具注册与权限矩阵，确保无权操作在门禁处被拒；007 覆盖 MCP 工具的可见性与权限边界。
- **012**：验证同角色合法调用的正向通行。
- **013/014**：验证伪造外形不能替代真实 permit（WrongOwner），以及 manifest 精确锚点准入的待办（见 WHAT [013]【程序待改】）。
- **015/016**：验证准入的版本冲突、终态后旧准入拒绝及单次生效规则。
- **017/018**：验证静默许可（Quiescence Permit）的申请、消费与精确归还机制。
- **019**：验证许可令牌（Token）与执行租约的原生 JSON 序列化防御（拒绝越界序列化）。
- **020/024**：验证致命错误边界与 DevOps 派发门禁。
- **025/026**：验证 Manager 的只读窗口，确保未获 Review 接纳时 DevOps 拒绝执行且零文件修改。

## 运行方式

先执行标准构建，再使用验证系统运行器执行：
```sh
TESTS_MJS_FILES="$(rg --files requirements/capability-enforcement/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
部分涉及真实插件启动的集成用例需启用 `WXS_TIER_INTEGRATION=1`。
