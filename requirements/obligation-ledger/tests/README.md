# obligation-ledger 测试说明

本包守住待办边界：原生 `todowrite` 不再暴露，编程面调用不产生 checkpoint，上下文重置由 `resetScope` 统一管理。

- 001–004：原生 `todowrite` 从静态工具面、权限矩阵与 ablation 映射移除；编程面调用无 checkpoint、无第二份投影；压缩事实不反推待办。
- 005：失败执行不形成持久事实，重放/并发/WriterUnavailable 重试按合同结算。
- 006：声明无裁决权。
- 007：legacy Magic Todo decoder 明确拒绝；未来若提供历史审计读取，也不得激活旧语义或写回。

旧施工输入仍原样保留在 historical-tests/，不在活动测试发现集内。

局部命令：`node --test requirements/obligation-ledger/tests/*.test.mjs`。集成用例需 `WXS_TIER_INTEGRATION=1`。
