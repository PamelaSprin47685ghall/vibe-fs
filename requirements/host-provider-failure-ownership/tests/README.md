# host-provider-failure-ownership 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含提供方（Provider）故障归属、重试决策及界面呈现的测试。

## 测试覆盖重点

- 验证提供方错误与本地配置错误、网络瞬态故障的分类界限。
- 验证重试策略的执行边界，确保不可恢复的致命故障及时反馈，不进入无限循环重试。
- 验证故障发生时对调用方清晰报错，不伪造成功结果。

## 运行方式

先执行标准构建，再使用验证系统运行器执行：
```sh
TESTS_MJS_FILES="$(rg --files requirements/host-provider-failure-ownership/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
