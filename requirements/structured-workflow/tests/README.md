# structured-workflow 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含结构化控制流、知识边界及编译依赖的回归测试。

## 测试覆盖重点

- **003—009**：验证控制结构与流程调度逻辑，避免引入隐式全局计数器或无序副作用。
- **004**：验证控制结构扫描器能够识别非法的嵌套或违规分支结构。
- **010**：验证并发顺序、上限控制与排队期间取消后的屏障拦截。
- **011—014, 016**：验证源码清单、依赖图与编译计划。
- **012**：验证 Fable 增量编译时的依赖图正确性，防止共享输出目录中的缓存串扰。
- **019**：验证顺序遍历的基数、错误短路、空输入及取消传播。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/structured-workflow/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
若测试涉及真实 Fable 增量编译，需启用 `WXS_TIER_INTEGRATION=1`。
