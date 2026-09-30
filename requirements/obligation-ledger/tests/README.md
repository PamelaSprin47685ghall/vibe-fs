# obligation-ledger 测试证明范围

本包只守住 OpenCode 原生 todowrite 的边界：todos 由 Host executor 原样消费，插件只处理 provider-facing retainCheckpoints，不再维护 TodoSink、画板或第二份待办真相。

- 001：真实 before hook 只剥离 retainCheckpoints，原生 todos 数组、行内容与顺序保持原对象；definition 不复活旧规划字段。
- 002：空列表与重复行穿透插件不改写；安装版 Host 真正替换 TodoTable 的物理行为仍留给 Host 集成证明。
- 003–006：不维护第二份 desired/applied 待办投影、checkpoint 不可反推 TodoTable、失败执行不形成 checkpoint、清单无裁决权。
- 007：旧 Magic Todo decoder 当前明确拒绝；若未来提供历史审计读取，也不得激活旧门禁或写回。

旧施工迁移输入仍原样保留在 historical-tests/，不进入活动测试发现集。

局部命令：node --test requirements/obligation-ledger/tests/*.test.mjs。集成用例需 WXS_TIER_INTEGRATION=1。
