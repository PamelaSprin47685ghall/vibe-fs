# unrecorded-decision — Enforcer 中文版

## 定义
Code 通常只保存“最后选了什么”，不会保存“为什么可信的其它方案被拒绝”。当 architecture/compatibility/operational tradeoff 会影响未来工程师可做的假设，而 rationale 只存在于会议/chat/当事人记忆中，就是 unrecorded decision。

## 何时触发
- 一个有争议的架构方向确定后没有 ADR/decision note；
- clean break、consistency model、single-region、provider choice 等重大边界没有记录 rejected alternatives；
- 未来人仅看代码，很合理地可能重新选择当初被否决的方案；
- 约束变化后没人知道 decision 是否应 revisit。

## 不要误判
- trivial local choice 没长期后果；
- rationale 已在 authoritative ADR；
- correctness rule 本身没记录，应归 `missing-invariant-documentation`；
- 事故中新学到可复用事实而非 deliberate tradeoff，应归 `unrecorded-lesson`。

## 刀口
一个 competent maintainer 只看当前 code，是否可能合理地“优化”回某个我们已经认真否决过的方案？如果会，counterfactual knowledge 没有被保存。

## 提醒
Implementation 记得赢家，不记得辩论。Decision record 的价值就是保存那些代码无法自然表达的“为什么不是另一条路”。
