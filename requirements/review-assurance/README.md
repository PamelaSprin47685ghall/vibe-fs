# review-assurance

> Finality judgement 何时可被消费，必须由 direct CE 的 fresh typed evidence 建立。

## 一句话 WHY

Reviewer 输出 `PERFECT`/`REVISE` 不等于 Finality 已完成。`review-assurance` 保证：第一次 PERFECT 之后，Finality CE 必须先注册第二 judgement waiter，再调用 first delivery 的 `Challenge()` capability 完成第一次 `judge`；第二次 judgement 必须来自同一 physical review prompt、但 fresh ProviderRun/ToolCall。每个 judgement wait 都与 start 前已订阅的 reviewer terminal 竞争，terminal/timeout 先到即 fail closed。整个 first → challenge tool result → second 时序只由 F# CE 调用结构拥有，不能从文本、日志积分或状态机推导。

## WHAT 概览

- dual-PERFECT 同 reviewer / barrier / tree，ProviderRun 与 ToolCall 均不同。
- challenge 由第一次 `judge(PERFECT)` delivery 的 `Challenge()` capability 完成当前 tool result，不另发 user continuation，也不存在业务 `Reply` DU。
- first/second judgement 的 `PhysicalUserMessageId` 必须相同，ProviderRun/ToolCall 必须 fresh；challenge 文本只负责展示，禁止解析/hash/扫描文本判断状态。
- 第一次 PERFECT 是 CE 局部值；禁止 durable pending-review 程序位置。
- completed `ReviewWitness` 自包含；confirmation 是证据上的派生谓词。
- tree/barrier 变化使旧 witness 不再满足 Guard，但历史 witness 不删除。
- Finality 与 TodoProcessReview 代数分离；process review 一次 judge，不参与 dual-PERFECT。
- VerdictKnown / ConsumableReview 仍保持两段式、同 snapshot record-ready、事件驱动等待。

## HOW 概览

- completed witness：`src/Wanxiangshu/Mission/Review/Judgement/Witness.fs`。
- provider-visible challenge：`src/Wanxiangshu/Mission/Review/Judgement/Challenge.fs`。
- Finality direct CE：`src/Wanxiangshu/Mission/Review/Barrier/Reverify.fs`。
- typed judgement rendezvous：`src/Wanxiangshu/Mission/Review/OpenCode/JudgementInbox.fs`。
- generic Host run / physical identity：`src/Wanxiangshu/OpenCode/Host/ProviderRunBinding.fs`。
- JudgeTool delivery：`src/Wanxiangshu/Mission/Review/OpenCode/JudgeTool.fs`。
- completed fact fold：`src/Wanxiangshu/Mission/Review/ReviewFactFold.fs`。
- process record-ready：`src/Wanxiangshu/Mission/Review/TodoProcess.fs`。

## proof 概览

| 文件 | 覆盖 |
|---|---|
| `tests/finality-direct-ce-contract.test.mjs` | 禁文本判断 / 禁 durable program position / CE temporal ownership |
| `tests/witness.test.mjs` | typed physical causality、attempt identity、自包含 witness、tree/barrier 失效 |
| `tests/seal-bind.test.mjs` | HOST-010 generic ProviderRunBinding fail-closed（历史文件名，内容已不属于 seal 协议） |
| `tests/shared-state.test.mjs` | 无 parked provider-input state；JudgementInbox 仅物理 rendezvous |
| `tests/review-guard.test.mjs` | process-review missing-judge repair；Finality challenge 不再由 idle guard 发送 |
| `tests/consumable-review.test.mjs` | process VerdictKnown → record-ready → ConsumableReview |
| `tests/review-requirement.test.mjs` | AuthorityRoot-keyed review requirements；confirmation replay 不清除后来 requirement |

## 阅读顺序

1. `WHY.md`
2. `WHAT.md`（唯一 normative）
3. `HOW.md`
4. `PROOF.md`
5. `tests/`

## 边界

- judgement 内容哲学 → `review-judgement`。
- Finality cohort/rejection/blessing/rest → `finality`。
- Host ProviderRun/physical binding 与 tool-call delivery → `host-boundary`。
- LWR 表示 → `work-record`。
- durable substrate → `durable-events`。
- causal waiting → `causal-wait`。

`DEPENDS ON: review-judgement, host-boundary, durable-events, causal-wait`。
