# crash-reconciliation 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含崩溃恢复、孤儿工作结算、重启对齐与生命周期重新接入的测试。

## 测试覆盖重点

| 用例编号 | 验证重点 | 说明 |
|---|---|---|
| **001, 006, 008** | 静默门禁与许可隔离 | 验证 gate owner 隔离、单次消费、活跃工具感知与撤销。 |
| **002, 005, 009, 010, 012** | 句柄投影与恢复映射 | 验证 child 解析、完成状态解码、handle 投影及恢复映射。 |
| **003, 007** | 恢复决策与未知状态隔离 | 验证 TurnUnknown 不作为业务终态发布，避免未确认结果污染局面。 |
| **011, 013, 014** | 闭包与合并授权 | 验证恢复过程中的授权函数与成员合并逻辑。 |
| **015** | 附加会话分类与创建端口 | 验证并发创建与会话分类保持。 |
| **018** | 重启续传配置检查 | 验证配置 hook 不注册多余命令，中断历史不触发无意副作用。 |
| **020** | 孤儿子工作作废与 Journal 容错 | 验证加载期对孤儿子工作的幂等作废（`ChildWorkVoided`），以及底层写入受阻时 writer 保持健康。 |
| **021** | 句柄持久查询与 Fission 分路恢复 | 验证 durable handle 按 ID / Byname 的冷恢复与 Fission fact 状态重建。 |

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/crash-reconciliation/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
