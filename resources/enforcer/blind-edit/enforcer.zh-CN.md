# blind-edit — Enforcer

Blind edit 的病，不是“改得太快”，而是 mutation 发生在**ownership 与 causal path 尚未被证明**之前。

一个 symptom 出现在 UI、test、renderer、adapter，不代表那个文件拥有 defect。Source line 是系统的 witness，不是天然 root cause。没有先回答“哪个 contract 被违反、错误 fact 从哪里首次产生、如何一路到 observable”，就直接改最近的地方，本质是在赌 textual proximity 等于 causal ownership。

以下情形触发：

- 看见 failing line 就立刻 patch，尚未读 owning contract；
- downstream 加 guard/fallback 把错误 fact 藏起来；
- 只凭 function/file 名猜 API/lifecycle；
- 同一个 symptom 在多个地方出现，于是到处 spray 相似修补；
- patch 能让当前 test green，却解释不了原 failure mechanism；
- AI 根据 error message 直接生成 diff，没有先确认真实 owner/source path。

不要把它变成“改一行前必须做半小时考古”。如果当前 context 已经清楚给出 owner、contract、causal path，local edit 可以立刻做。Mechanical rename/format 也不属于 behavioral diagnosis。

与 `guess-based-fix` 区分：blind edit 发生得更早——连 causal territory 都没画就开始变更；guess-based fix 是已经在试不同 repair 直到 symptom 消失。与 `guessed-not-verified` 区分：后者可以只是在 reasoning 中把未证 premise 当 fact，尚未 mutation。

最决定性的停手条件不是“我还没读很多文件”，而是：**我能不能用一句因果句说明 first violated invariant 在哪里？**

> 不要修 symptom 最响的地方；修第一个让世界开始说错话的 owner。