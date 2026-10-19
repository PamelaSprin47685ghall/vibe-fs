# unrecorded-decision — Lesson

## 评价

### 定义

Code 通常只保存“最后选了什么”，不会保存“为什么可信的其它方案被拒绝”。当 architecture/compatibility/operational tradeoff 会影响未来工程师可做的假设，而 rationale 只存在于会议/chat/当事人记忆中，就是 unrecorded decision。

### 支配原则

Code 保留被选择的东西；它很少保留为什么备选被拒绝。被拒绝的备选只活在对话里。没有 rationale，未来维护者只遇到幸存下来的 shape，可能理性地把它“简化”回一个被放弃的设计，因为排除那个设计的约束不可见。Decision record 携带 implementation 自身无法表达的 counterfactual knowledge。

### 何时触发

- 一个有争议的架构方向确定后没有 ADR/decision note；
- clean break、consistency model、single-region、provider choice 等重大边界没有记录 rejected alternatives；
- 未来人仅看代码，很合理地可能重新选择当初被否决的方案；
- 约束变化后没人知道 decision 是否应 revisit。

### 不应触发

- trivial local choice 没长期后果；
- rationale 已在 authoritative ADR；
- correctness rule 本身没记录，应归 `missing-invariant-documentation`；
- 事故中新学到可复用事实而非 deliberate tradeoff，应归 `unrecorded-lesson`。

### 与相邻规则区分

`unrecorded-lesson` 在经验之后捕捉可复用的发现；`missing-invariant-documentation` 捕捉 correctness rule。Tie-break：如果缺失的是“为什么选了一条 durable path 而非可信备选”，用本规则；如果学到的是可复用 operational/debug 事实而未存储，用 `unrecorded-lesson`。

### 判定程序

一个 competent maintainer 只看当前 code，是否可能合理地“优化”回某个我们已经认真否决过的方案？如果会，counterfactual knowledge 没有被保存。

### 例子

- positive：团队因 consistency 理由拒绝 multi-region writes，shipping single-region，却没留 ADR。
- near-miss：ADR 已记录约束，code change 只是实现它。
- counterexample：incident 揭示 provider retry quirk，从未写进 runbook——那是 `unrecorded-lesson`。

## 建议

### 现在该做什么

在项目真正做 architecture decision 的地方记录：context/constraints、chosen path、credible alternatives、为什么拒绝、consequences、什么新证据会触发 revisit。链接 governing contracts，不复制整份 specification。

### 为什么重要

没有 rationale 的代码只是一种“ unexplained equilibrium”。未来人看到它，很可能为了简化而恢复曾经失败的设计，然后重新支付同一轮分析甚至事故成本。

### 修复策略

让记录与后果成比例：陈述问题、约束、决定、考虑的备选，以及什么会正当化 revisit。链接 governing contracts 而不是复制它们。

### 决策分支

- 若未来 maintainer 仅凭 code 就能合理地复活被拒绝的备选，写 decision record。
- 若 rationale 已在 authoritative ADR 或 executable invariant 里，更新那个 artifact，而不是加一条平行故事。

### 常见假修复

- 贴 chat transcript，不提炼 decision。
- 只描述最终实现，不记录 alternatives 与 constraints。
- 把 contract 全复制进 ADR，制造第二 truth source。
- 每个小决定都写长 ADR，导致真正重要记录被噪声淹没。

### 验证

未来 reader 应能回答：这个 decision 在哪些 assumptions 下成立？当什么 evidence 出现时应重新考虑？当初哪些 credible alternatives 因什么被拒绝？

### 完成条件

架构不仅保存形状，也保存足够的反事实知识，避免未来把历史重新当成未知问题。

### Nudge

Implementation 记得赢家，不记得辩论。Decision record 的价值就是保存那些代码无法自然表达的“为什么不是另一条路”。
