# knowledge-reuse 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含 Casebook 经验复用、双基线维护、实质访问与事实存储的验证测试。

## 测试覆盖重点

- **001, 005, 011, 015**：验证 `fetch` 查询流程、EventStore 交互及 Bookkeeper 维护逻辑。覆盖无差异返回旧正文、文件差异时触发单次刷新、刷新失败保留双基线及并发 fetch 的串行合并。
- **002, 004, 007, 008, 016**：验证事实存储的完整文件状态（Present/Missing）、内容寻址（SHA-256 与 PayloadRef）、双基线推进、访问事件单调顺序及基于 LRU 事件的条目淘汰。
- **003**：验证工具执行层的真实文件读写（如 read、write、move、delete）对实质访问路径的精确记录，排除无副作用的模式匹配与路径提及。
- **006**：验证 Bookkeeper 的原子变换语义：`setQuestion` 与 `setAnswer` 的单次调用约束、零修改合法推进基线，以及异常时的完整回滚。
- **009**：验证未启用 Casebook（缺少 marker 目录）时对 fetch 请求的显式拒绝与非侵入保证。
- **010**：验证逻辑工程任务完成时的单次归档结算，及多分支任务完全收敛后的单次冻结。
- **012**：验证公开索引向模型暴露的字段仅限于 `{shelfmark, canonical question}`，不泄漏内部状态与会话拓扑。
- **013**：验证案例语义冲突时的故障隔离：冲突限制于单个案例自身，不终止宿主进程、不撤销已完成交付物，并向调用方返回未完成状态。
- **014**：验证超预算时对调用轨迹与 diff 的截断保护，保留变更路径与截断声明。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行本包测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/knowledge-reuse/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
若测试涉及真实存储与端到端集成，需启用 `WXS_TIER_INTEGRATION=1`。
