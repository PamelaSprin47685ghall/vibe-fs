# finality — HOW

## 1. 终结流转与状态投影

`finality` 统一管理 Manager 生命周期的终结判定与审查编排：

- **状态投影模型**：`LifeProjection` 与 `FinalityRequestProjection` 增量维护活跃请求、审查员名册、最新 Blessing、完成状态以及决议状态。状态由事实事件纯推导，不包含内存程序计数器。
- **终结意图分类（classifyEnding）**：纯函数将调用上下文分派为 `ContinuePlanning`、`AlreadyCompleted`、`ResumeRequest`、`RecoverRequestWithoutReviewers`、`WaitForCurrentRequest`、`CompleteBlessedLife` 或 `BeginFinality`。分派结果由 Finality 内部处理，不向外层暴露操作码。
- **JS 语义边界**：`FinalitySurface` 统一对外暴露生命周期与任务历史的投影视图，测试与外围系统仅消费结构化数据，F# 内部 union 与实现细节封装在领域内部。

## 2. 审查编排与收束机制

- **Cohort 组装与毕业推导**：每次终审请求由纯函数 `rosterOf` 组装花名册，包含一名新审查员与历史未毕业审查员。Dedicated Reviewer 首次入组后按普通规则随合格 witness 毕业。
- **REVISE 双轨收束**：任一审查员给出 REVISE 后，工作流立即关闭该 cohort；首个 REVISE 作为工具结果返回，后续到达的 REVISE 物化为独立 steer continuation 交付。
- **Blessing 与 Rest 路径**：全员达成双重 PERFECT 且代码树一致后，物化规范 LWR bundle 并写入 `FinalityBlessed`；二次调用 `suicide` 时在排查阻塞 REVISE 后直接落盘 `LifeCompleted`，输出逐字等于 `last_words`。

## 3. 依赖声明

```text
DEPENDS ON: obligation-ledger, review-assurance, participant-horizon
```

## 4. 边界（DOES NOT OWN）

- 账本维护、计划承诺与过程评审 1:1 节拍 → `obligation-ledger`
- 见证结构、因果确认代数与 record-ready 判定 → `review-assurance`
- 隐藏机制的词汇过滤与信息准入规则 → `participant-horizon`
- 规范工作记录 LWR 的物化与格式 → `work-record`
- 进程级故障终止与崩溃恢复 → `crash-reconciliation`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| FINALITY-001 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-002 | `requirements/finality/tests/manager-finality-disposition.test.mjs`, `requirements/finality/tests/blessing-admission.test.mjs` |
| FINALITY-003 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-004 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-005 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-006 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-007 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-008 | `requirements/finality/tests/lifecycle.test.mjs` |
| FINALITY-009 | `requirements/finality/tests/finality-cohort-law.test.mjs` |
| FINALITY-010 | `requirements/finality/tests/finality-cohort-law.test.mjs` |
| FINALITY-011 | `requirements/finality/tests/lifecycle.test.mjs` |
| FINALITY-012 | `requirements/finality/tests/lifecycle.test.mjs` |
| FINALITY-013 | `requirements/finality/tests/lifecycle.test.mjs` |
| FINALITY-014 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-015 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-016 | `requirements/finality/tests/lifecycle.test.mjs`, `requirements/finality/tests/blessing-admission.test.mjs` |
| FINALITY-017 | `requirements/finality/tests/lifecycle.test.mjs` |
| FINALITY-018 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-019 | `requirements/finality/tests/lifecycle.test.mjs` |
| FINALITY-020 | `requirements/finality/tests/lifecycle.test.mjs` |
| FINALITY-021 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-022 | `requirements/finality/tests/life-admission.test.mjs` |
| FINALITY-023 | `requirements/finality/tests/rewrite-consistency.test.mjs` |
| FINALITY-024 | `requirements/finality/tests/rewrite-consistency.test.mjs` |
| FINALITY-025 | `requirements/finality/tests/manager-finality-disposition.test.mjs` |
| FINALITY-026 | `requirements/finality/tests/finality-fatal-contract.test.mjs` |
| FINALITY-027 | `requirements/finality/tests/finality-background-obligation.test.mjs` |
| FINALITY-028 | `requirements/finality/tests/manager-job-no-resurrection.test.mjs` |
