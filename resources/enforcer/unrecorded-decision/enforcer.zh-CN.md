# unrecorded-decision — Enforcer 中文版

## 定义

Code 通常只保存“最后选了什么”，不会保存“为什么可信的其它方案被拒绝”。当 architecture/compatibility/operational tradeoff 会影响未来工程师可做的假设，而 rationale 只存在于会议/chat/当事人记忆中，就是 unrecorded decision。

## 支配原则

Code 保留被选择的东西；它很少保留为什么备选被拒绝。被拒绝的备选只活在对话里。没有 rationale，未来维护者只遇到幸存下来的 shape，可能理性地把它“简化”回一个被放弃的设计，因为排除那个设计的约束不可见。Decision record 携带 implementation 自身无法表达的 counterfactual knowledge。

## 何时触发

- 一个有争议的架构方向确定后没有 ADR/decision note；
- clean break、consistency model、single-region、provider choice 等重大边界没有记录 rejected alternatives；
- 未来人仅看代码，很合理地可能重新选择当初被否决的方案；
- 约束变化后没人知道 decision 是否应 revisit。

## 不应触发

- trivial local choice 没长期后果；
- rationale 已在 authoritative ADR；
- correctness rule 本身没记录，应归 `missing-invariant-documentation`；
- 事故中新学到可复用事实而非 deliberate tradeoff，应归 `unrecorded-lesson`。

## 与相邻规则区分

`unrecorded-lesson` 在经验之后捕捉可复用的发现；`missing-invariant-documentation` 捕捉 correctness rule。Tie-break：如果缺失的是“为什么选了一条 durable path 而非可信备选”，用本规则；如果学到的是可复用 operational/debug 事实而未存储，用 `unrecorded-lesson`。

## 判定程序

一个 competent maintainer 只看当前 code，是否可能合理地“优化”回某个我们已经认真否决过的方案？如果会，counterfactual knowledge 没有被保存。

## 例子

- positive：团队因 consistency 理由拒绝 multi-region writes，shipping single-region，却没留 ADR。
- near-miss：ADR 已记录约束，code change 只是实现它。
- counterexample：incident 揭示 provider retry quirk，从未写进 runbook——那是 `unrecorded-lesson`。

## Nudge

Implementation 记得赢家，不记得辩论。Decision record 的价值就是保存那些代码无法自然表达的“为什么不是另一条路”。