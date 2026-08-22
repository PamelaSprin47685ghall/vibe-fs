# review-assurance — HOW

## 1. 见证模型与因果证明

- **Witness 数据模型**：`ReviewWitness` 仅包含 `NoReview`、`RevisionWitness` 与 `Confirmed` 三种终态。不存在“已第一次 PERFECT、等待第二次”的持久化状态。`Confirmed` 结构自包含两次判断标识、代码树哈希与物理交互消息标识，有效性通过纯函数派生计算。
- **物理因果绑定**：工具层通过 `ToolRuntimeScope` 提取当前执行绑定的 `PhysicalUserMessageId`，并在向工作流投递强类型 judgement 时传递 `Accept`、`Challenge` 与 `Reject` 完成能力。第二判断等待器在触发首次 `Challenge` 之前预先就位，因果关系由调用顺序天然保证。

## 2. 终审双重确认与状态流转

终审因果工作流直接由宿主语言控制流驱动：
1. 注册终止观察与首次判断等待器，启动审查。
2. 捕获首次 `judge(PERFECT)` 并持久化事实；预先注册第二判断等待器，随后调用首次投递的 `Challenge()` 能力返回质疑提示。
3. 审查者在同一物理交互或经由 nudge 续接后发起第二次判断；工作流校验两次调用的独立性（不同 ProviderRun/ToolCall）与物理提示一致性，校验通过后一次性写入 `ConfirmedReviewWitness` 并完成调用。
4. 任一阶段收到 REVISE 或发生代码树漂移立即以失败关闭，不持久化半程位置。

## 3. 过程评审与就绪判定机制

- **两段式事实收束**：过程评审首先将 durable `judge` 记录为 `VerdictKnown` 并触发 turn 结束；随后在同一 Snapshot 下执行 record-ready 判定与 `ProcessReviewLWR` 物化，就绪后写入 `TodoReviewConcluded`。
- **事件驱动等待**：下游消费方通过 `AgentJournal.awaitChangeFrom` 订阅事件，严格避免轮询；等待器被中断后直接基于持久事实重新构建。
- **物理发送 fence**：过程 `judge` 成功持久化时按 Reviewer Session arm process-local interrupt fence。下一 checkpoint 可继续建立 durable assignment，但 continuation 在调用 Host `SendPrompt` 前必须等待 fence；`InterruptAttempt` 成功后 release，失败则 fail closed。由此新 review 请求不会与上一轮 tool result 一起进入 Host 队列。

## 4. 依赖声明

```text
DEPENDS ON: review-judgement, semantic-trace, durable-events, causal-wait
```

## 5. 边界（DOES NOT OWN）

- 裁决词的语义与判定哲学 → `review-judgement`
- 过程评审 1:1 节拍与账本消费门槛 → `obligation-ledger`
- 终结前置条件与经验分类 → `finality`
- 规范工作记录 LWR 的表示与格式 → `work-record`
- 事件存储与快照机制 → `durable-events`
- 因果等待底座 → `causal-wait`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REVIEW-ASSURANCE-001 | `requirements/review-assurance/tests/witness.test.mjs` |
| REVIEW-ASSURANCE-002 | `requirements/review-assurance/tests/host-reverify.test.mjs` |
| REVIEW-ASSURANCE-003 | `requirements/review-assurance/tests/witness.test.mjs` |
| REVIEW-ASSURANCE-004 | `requirements/review-assurance/tests/witness.test.mjs` |
| REVIEW-ASSURANCE-005 | `requirements/review-assurance/tests/witness.test.mjs` |
| REVIEW-ASSURANCE-006 | `requirements/review-assurance/tests/witness.test.mjs` |
| REVIEW-ASSURANCE-007 | `requirements/review-assurance/tests/seal-bind.test.mjs` |
| REVIEW-ASSURANCE-008 | `requirements/review-assurance/tests/consumable-review.test.mjs` |
| REVIEW-ASSURANCE-009 | `requirements/review-assurance/tests/consumable-review.test.mjs` |
| REVIEW-ASSURANCE-010 | `requirements/review-assurance/tests/witness.test.mjs` |
| REVIEW-ASSURANCE-011 | `requirements/review-assurance/tests/consumable-review.test.mjs` |
| REVIEW-ASSURANCE-012 | `requirements/review-assurance/tests/consumable-review.test.mjs` |
| REVIEW-ASSURANCE-013 | `requirements/review-assurance/tests/review-requirement.test.mjs` |
