# review-judgement — HOW

## 1. 工具契约与判断面

- **`judge` 接口定义**：工具参数仅包含单个必填强类型字段 `verdict`（枚举值为 `PERFECT` 与 `REVISE`），禁止包含描述、说明或附加元数据。
- **回执语义分型**：
  - 过程评审（TodoProcessReview）成功后返回 `tool/judge/received`，明确提示判断已被收下并指示会话收束，回执中绝不回显 verdict。
  - 终局评审（FinalityReview）首次 PERFECT 触发 `Challenge` 逻辑，仅渲染质疑提示（`resources/provider/review/challenge`），严禁拼接收束回执；仅在第二次判断或 REVISE 时完成工具调用。
- **防重与幂等范围**：已裁决状态（already judged）的作用域限定于当前 `(ReviewerSessionId, PhysicalUserMessageId)` 物理请求。单次请求内的重复调用被收束以避免空转；当同一 dedicated 会话接收新的物理请求时，自动重置并恢复单次裁决资格。
- **terminal 回执的物理收束**：首次 terminal judgement durable 后只写 request-scoped submitted 标记，不在 `judge` 的 duplicate 分支中等待第二次提交再杀。普通 provider transform 在完成消息投影后检查“当前 PhysicalUserMessageId 是否已有 submitted judgement”；命中则调用 managed-session `InterruptAttempt`，忽略其返回值并让 transform 正常返回。这样 tool result 文本仍由 Host 正常形成，但同一物理请求不得再进入 LLM continuation。新物理请求因 identity 不同自然恢复正常执行；Finality 首次 PERFECT challenge 未建立 submitted 标记，因此继续正常二次评估。

## 2. 判断哲学载体与引导机制

- **Role Law 与质量基准**：Reviewer 的判断哲学由 Role Law 及 Examiner's Ledger 承载，提供区分力、实质性缺陷、非阻断工艺以及不确定性处理等规范原则。
- **提示词组装与权威**：系统提示词在会话启动时由认知环境统一组装注入，Reviewer 在执行判断时遵循引导方向，但不把质量基准固化为机械的填表格式。

## 3. 过程评审与终结流转分型

- **过程评审单次收束**：过程评审通过单次 durable `judge` 形成 `VerdictKnown`，当对应的 `ProcessReviewLWR` 在相同 snapshot 下满足 record-ready 后，生成 `TodoReviewConcluded`。
- **终审双重确认解耦**：终审的双重 PERFECT 编排由独立的因果工作流驱动，过程评审的 verdict 不进入终审 witness 代数。

## 4. 依赖声明

```text
DEPENDS ON: cognitive-environment, participant-horizon
```

## 5. 边界（DOES NOT OWN）

- 评审结论的因果确认、witness 结构与 seal 绑定 → `review-assurance`
- 过程评审 1:1 节拍与义务派生 → `obligation-ledger`
- 终局 cohort 编排、rejection 与 blessing 经验 → `finality`
- 提示词资源的组装与装载权威 → `cognitive-environment`
- 隐藏 Reviewer 视野与信息准入隔离 → `participant-horizon`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REVIEW-JUDGEMENT-001 | `requirements/review-judgement/tests/judge-tool-contract.test.mjs` |
| REVIEW-JUDGEMENT-002 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-003 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-004 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-005 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-006 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-007 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-008 | `requirements/review-judgement/tests/process-review-judgement.test.mjs` |
| REVIEW-JUDGEMENT-009 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-010 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
