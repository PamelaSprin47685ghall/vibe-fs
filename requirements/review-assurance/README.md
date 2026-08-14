# review-assurance

> 一个 review judgement 何时有资格被消费，必须由 bounded evidence、fresh witness 与因果确认建立。

## 一句话 WHY

Reviewer 输出了 `PERFECT`/`REVISE`，不等于系统已经证明这个判断**针对正确对象、消费了必要 challenge、并带着可供 caller 使用的完整证据**。`review-assurance` 保证：只有被因果确认、绑定当前 tree/barrier/request、且 record-ready 的 judgement 才能被下游消费。

## WHAT 概览（详见 WHAT.md）

- 第二次 PERFECT 必须满足 REVIEW-003 九条件；禁止 same-root / physical-message 确认。
- 单次 PERFECT 不足；challenge 必须被证明出现在第二次输入 seal 里（因果消费）。
- ReviewAttemptIdentity 五元组；同 run 额外 PERFECT 不计数。
- confirmation 是派生谓词，不是存储布尔。
- ReviewWitness 自包含；Guard 不依赖外围 Map。
- 任意 tree 变化使 witness 失效（pending 拒、confirmed 不再满足 Guard），不删除历史。
- ProviderInputSeal fail-closed；无法绑定 ProviderRunIdentity 则不确认。
- VerdictKnown 与 ConsumableReview 两段式；禁止提前 append 空壳 Concluded。
- record-ready 同 snapshot、排他 frontier、事件驱动、无轮询、waiter 可恢复。
- 基础设施失败永远不是 PERFECT/REVISE（review-side 负边界）。
- process verdict 与 terminal witness 代数分离，互不计数。
- 可消费证据 request-range bounded；session head 不能冒充。

## HOW 概览（详见 HOW.md）

- witness/challenge 代数：`src/Wanxiangshu/Domain/{ReviewWitness,ReviewChallenge}.fs`。
- 投影与 fold：`src/Wanxiangshu/Journal/{ReviewProjection,ReviewBarrier,ReviewFactFold,FinalityReviewCohort}.fs`。
- 确认写入：`src/Wanxiangshu/Application/Review/{VerdictWorkflow,ReviewBarrierWorkflow,ReviewerContinuation,ReviewerWorkflow}.fs`。
- seal 绑定：`src/Wanxiangshu/Application/Reconciliation/ReviewSeal.fs`。
- record-ready 等待：`src/Wanxiangshu/Application/Review/TodoProcessReviewProgram.fs`。

## proof 概览（详见 PROOF.md）

| 文件 | 覆盖 |
|---|---|
| `tests/witness.test.mjs`（MOVE） | REVIEW-ASSURANCE-001..007（九条件 / challenge / attempt / 派生 / 自包含 / tree 失效 / seal 窗口） |
| `tests/seal-bind.test.mjs`（MOVE） | REVIEW-ASSURANCE-007（HOST-010 bindableRun 四条件 fail-closed） |
| `tests/consumable-review.test.mjs`（NEW） | REVIEW-ASSURANCE-008..012（两段式 / 禁伪造 / 绑定 / 代数分离 / waiter fail-closed） |

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、RED 长什么样。
2. `WHAT.md` —— 唯一 normative 合同，13 条命题。
3. `HOW.md` —— 实现模型；历史与弃权。
4. `PROOF.md` —— 落点表 + 运行命令 + cutover 拆分计划。
5. `tests/` —— 可执行 proof（42 条，全部可单文件跑绿）。

## 边界（DOES NOT OWN）

- PERFECT/REVISE 的判断哲学、materiality、checklist 禁令 → `review-judgement`。
- obligation account、Rk 节拍、CurrentObligations → `obligation-ledger`。
- 终末 cohort/rejection/blessing/rest/drain → `finality`。
- canonical LWR 的表示/物化 → `work-record`（本包只拥有「record-ready 才可消费」）。
- Host 因果读的传输侧 → `host-boundary`；等待的因果可观测性 → `causal-wait`。
- 事件 substrate / append / fold → `durable-events`；XTrace 历史 → `semantic-trace`。
- tool 语法红字分类 → `capability-enforcement`；infra fatal fail-fast → `host-boundary`/`crash-reconciliation`（本包只拥有 review-side「不把 infra 伪装成 REVISE」负边界）。

## 依赖

`DEPENDS ON: review-judgement, semantic-trace, durable-events, causal-wait`（理由见 HOW.md）。
