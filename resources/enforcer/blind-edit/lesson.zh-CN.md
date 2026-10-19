# blind-edit — Lesson

## 评价

### 定义

Blind edit 的病，不是“改得太快”，而是 mutation 发生在**ownership 与 causal path 尚未被证明**之前。

一个 symptom 出现在 UI、test、renderer、adapter，不代表那个文件拥有 defect。Source line 是系统的 witness，不是天然 root cause。没有先回答“哪个 contract 被违反、错误 fact 从哪里首次产生、如何一路到 observable”，就直接改最近的地方，本质是在赌 textual proximity 等于 causal ownership。

### 支配原则

Source 是系统的证据，不是系统本身。一行代码的意义来自它上方的 invariant 与下方的 caller。改第一个看起来合理的位置，是把 textual proximity 误当成 causal ownership。这类修复常常去掉一个 symptom，却保留甚至加重了产生它的 mechanism。

### 何时触发

以下情形触发：

- 看见 failing line 就立刻 patch，尚未读 owning contract；
- downstream 加 guard/fallback 把错误 fact 藏起来；
- 只凭 function/file 名猜 API/lifecycle；
- 同一个 symptom 在多个地方出现，于是到处 spray 相似修补；
- patch 能让当前 test green，却解释不了原 failure mechanism；
- AI 根据 error message 直接生成 diff，没有先确认真实 owner/source path。

### 不应触发

不要把它变成“改一行前必须做半小时考古”。如果当前 context 已经清楚给出 owner、contract、causal path，local edit 可以立刻做。Mechanical rename/format 也不属于 behavioral diagnosis。

### 与相邻规则区分

与 `guess-based-fix` 区分：blind edit 发生得更早——连 causal territory 都没画就开始变更；guess-based fix 是已经在试不同 repair 直到 symptom 消失。与 `guessed-not-verified` 区分：后者可以只是在 reasoning 中把未证 premise 当 fact，尚未 mutation。

### 判定程序

最决定性的停手条件不是“我还没读很多文件”，而是：**我能不能用一句因果句说明 first violated invariant 在哪里？**

### 例子

- positive：一个失败的 UI symptom 在最近的 renderer 里被 patch，却还没追查哪个 service 拥有错误 fact。
- near-miss：owner、contract、call path 都已到手，edit 落在第一个 violated invariant 上。
- counterexample：停下，命名 observed failure，定位 owning contract，然后只改那个 owner。

## 建议

### 现在该做什么

暂停 symptom patch，先把因果链补齐到足以定位 owner。

### 为什么重要

好的修复通常有很强的局部性：一处 owner 变正确，多个 downstream symptom 一起消失。若必须在每个 consumer 都补洞，往往说明错误 fact 仍在上游被生产。

### 修复策略

最小调查不是“把仓库读一遍”，而是完成四件事：

1. 说清 observable failure；
2. 找到真正拥有这条 behavior 的 contract；
3. 从 input/事实来源一路追到 observation；
4. 找到第一个违反 invariant 的位置。

然后只在这个 owner 上改，downstream guard 只有在它本身就是 contract 所要求的 defense 时才保留。

### 决策分支

若调查证明 symptom 本身就是 owner（例如 formatter 的纯展示 bug），那就直接在那里改；规则不奖励无意义的深挖。

### 常见假修复

常见假修复：

- renderer/service 末端加 fallback，让 wrong fact 不再 visible；
- 同时改多个可疑点，最后不知道哪一处真正修了 cause；
- 按命名猜某 API “应该这样工作”；
- 把 failing test expectation 改成当前实现；
- 看到旧 workaround 就继续叠 workaround，而不问原 owner 是否已经变化；
- AI patch 一次 green 后保留所有 speculative edits。

### 验证

验证要能反向解释 old failure：旧机制为什么会产生这个 symptom？新 edit 恢复了哪条 invariant？如果回答只能是“tests now pass”，还缺 causal proof。

### 完成条件

完成时 patch 是一条解释：从 violated contract 到 owning state/transition，再到 observable correction；不是一组“看起来可能有用”的行。

> 最小改动不是 diff 最短，而是只改变真正拥有错误语义的地方。

### Nudge

> 不要修 symptom 最响的地方；修第一个让世界开始说错话的 owner。
