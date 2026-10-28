# dispatch-protocol 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含请求分派、许可流转、准入交接及执行结果的测试。

## 测试覆盖重点

- **001**：验证 Host 端的 claim 注册与观察，确保消息来源真实有效。
- **002/005/006**：验证生产投影、序列号递增与参数哈希，支持确定性输入与幂等性检查。
- **002 边界**：验证 `ManagerWorkflow.observeIdle` 发送链，确保新 Human 消息到达时撤销旧许可，避免多余物理调用；验证 Journal 关闭时无法持久化 claim 的 fail-closed 防御。
- **003/004/007/008**：验证收据（Receipt）发送与持久化，验证重开 Journal 时的状态读取与核对。
- **009**：验证异步超时与完成通知，确保当 SDK 未决时 managed ingress 能够正确处理完成态或拒绝态。
- **010/011**：验证使用有效 Root Profile 续行时的 PromptKey、Origin 及元数据传递；通过类型级约束隔离 Model 参数。
- **012**：验证 Claim 的接纳与回合调和边界。
- **013/015**：验证非法 PromptKey 与非法 Agent 访问的防御，防止权限意外升格。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/dispatch-protocol/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
