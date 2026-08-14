# finality

> mission 的不可逆结束资格必须在当前义务、当前 tree 与合格 review 证据上建立，而不能由 participant 自宣告完成。

## 一句话 WHY

participant 自己认为 mission 完成 ≠ 世界允许不可逆结束。
终结资格必须建立在当前义务（obligation-ledger）、当前被审对象与合格 review evidence
（review-assurance）上，同时把 rejection、accepted-but-still-finishing、rest 分成不同经验，
并把 acceptance/rejection/rest 压成单一含混 terminal state 视为 RED。

## WHAT 概览（→ WHAT.md）

| 组 | 命题 | 保证 |
|---|---|---|
| 终结资格 | FINALITY-001/002/003/005/006 | suicide 只属 Manager；受理前置 + TODO-010；零 checkpoint fail closed；无机械 completeness gate |
| 终结入口 | FINALITY-004/007/016/025 | 唯一 tail drain；受理顺序 durable；Manager deferred completion；后台 JoinGuard |
| 评审收束 | FINALITY-008/009/010/011 | roster；graduate 由 witness 推导；REVISE 立即关闭；双轨交付 sibling steer |
| 三种经验 | FINALITY-012/013/014/015/024 | rejection / blessed / rest / undecided 分型；Acceptance ≠ rest |
| 隐藏机制 | FINALITY-017/018/019 | Manager 面无 Review Guard；隐藏评审不变成 checklist；状态只来自 typed facts |
| Life 合同 | FINALITY-020/021/022/023/026 | Life 开启/隔离/Reawakening；Opening durable 顺序；旧 journal；ManagerJob |

## HOW 概览（→ HOW.md）

- wiring：`src/Wanxiangshu/Application/Finality/{FinalityWorkflow,CohortWorkflow,BlessingWorkflow,RevisionWorkflow,RecordWorkflow,Ports,Types}.fs`、`Application/Manager/ManagerFinality.fs`
- type：`src/Wanxiangshu/Domain/{FinalityPrompt,MagicTodoFinalityCohort}.fs`、`Journal/FinalityReviewCohort.fs`
- Life 事实：`Domain/ManagerLifecycle.fs` + `Journal/ManagerLifecycleProjection.fs`
- 终结工具：`Infrastructure/OpenCode/Tools/FinalityTool.fs`（`suicide` 唯一入口）

## proof 概览（→ PROOF.md）

- MOVE：无（glory 测试族是多 owner 家族，按 PROOF-MAP 保留原位，SPLIT@cutover）
- REUSE：`requirements/finality/tests/lifecycle.test.mjs`（lifecycle 事实代数）、`requirements/finality/tests/finality-cohort-law.test.mjs`（roster/graduate）、`requirements/finality/tests/rewrite-consistency.test.mjs`（Opening 改写幂等）、~~`tests/unit/glory/manager-lifecycle-gate.test.mjs`~~（Activation 缺席，GARBAGE 侧，已 DELETE@cutover）
- NEW：`manager-finality-disposition.test.mjs`（`ManagerFinality.classifyEnding` / `admitLabor` 纯代数：drain 门禁、in-motion、rejection 续命、blessed-rest、Life 隔离）

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、历史上 RED 过什么
2. `WHAT.md` —— 唯一 normative 合同（编号命题）
3. `HOW.md` —— 实现模型 + 历史与弃权
4. `PROOF.md` —— 每条命题的测试落点与运行命令

## DEPENDS ON

- `obligation-ledger`：终结资格依赖「当前仍欠什么」的唯一真相源与 1:1 过程评审节拍；drain 消费的是其 ConsumableReview 义务。
- `review-assurance`：cohort 收束必须消费「对当前 request/barrier/tree 有资格」的 dual-PERFECT witness；rejection 记录必须 record-ready。
- `participant-horizon`：隐藏 Reviewer / barrier / witness / cohort 不进 Manager 面是信息准入约束（与 delegation 同型）——本包只拥有「隐藏机制不变成 Manager checklist、只暴露 consequence」的 finality 侧。

## 边界（DOES NOT OWN）

- Reviewer judgement standard（PERFECT/REVISE 的语义）→ `review-judgement`
- assurance primitive 的内部实现（witness/seal/challenge、record-ready、同 snapshot）→ `review-assurance`
- obligation account 语义与评审节拍 → `obligation-ledger`
- 当前 `suicide` 字面工具名或叙事风格必须永久保持 → HOW（不是永久 contract）
- generic session lifecycle：life completion 触发的 dedicated reviewer session 退休由 `managed-session-lifecycle` owner-closure 消费
- OpeningMaterial / LWR 物化与三段标题 → `work-record`
- system prompt 字节稳定 / Persona 冻结 → `participant-identity` / `prefix-stability`
- infra fatal 的进程级处理 → `crash-reconciliation`
