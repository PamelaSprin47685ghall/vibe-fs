# obligation-ledger 测试证明范围

本包守住待办消极边界与宿主原生 `todowrite` 退役边界：原生 `todowrite` 不再暴露，编程面调用不产生 checkpoint，上下文重置由 `resetScope` 统一管理。

- 001：原生 `todowrite` 已从静态工具面、权限矩阵、调查估计分类和 ablation 映射退役；原视图声明与 checkpoint 提交逻辑已退役，待重写适配消极边界。
- 002：原 checkpoint 窗口（`PhaseWindow`）与 canary 调用用例待重写。
- 003–006：不维护第二份待办投影、失败执行不形成持久事实且重放/并发/WriterUnavailable 重试符合合同、声明无裁决权。
- 007：旧 Magic Todo decoder 当前明确拒绝；若未来提供历史审计读取，也不得激活旧门禁或写回。

旧施工迁移输入仍原样保留在 historical-tests/，不进入活动测试发现集。

局部命令：node --test requirements/obligation-ledger/tests/*.test.mjs。集成用例需 WXS_TIER_INTEGRATION=1。
