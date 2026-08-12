# blind-edit — Main

暂停 symptom patch，先把因果链补齐到足以定位 owner。

最小调查不是“把仓库读一遍”，而是完成四件事：

1. 说清 observable failure；
2. 找到真正拥有这条 behavior 的 contract；
3. 从 input/事实来源一路追到 observation；
4. 找到第一个违反 invariant 的位置。

然后只在这个 owner 上改，downstream guard 只有在它本身就是 contract 所要求的 defense 时才保留。

常见假修复：

- renderer/service 末端加 fallback，让 wrong fact 不再 visible；
- 同时改多个可疑点，最后不知道哪一处真正修了 cause；
- 按命名猜某 API “应该这样工作”；
- 把 failing test expectation 改成当前实现；
- 看到旧 workaround 就继续叠 workaround，而不问原 owner 是否已经变化；
- AI patch 一次 green 后保留所有 speculative edits。

验证要能反向解释 old failure：旧机制为什么会产生这个 symptom？新 edit 恢复了哪条 invariant？如果回答只能是“tests now pass”，还缺 causal proof。

好的修复通常有很强的局部性：一处 owner 变正确，多个 downstream symptom 一起消失。若必须在每个 consumer 都补洞，往往说明错误 fact 仍在上游被生产。

若调查证明 symptom 本身就是 owner（例如 formatter 的纯展示 bug），那就直接在那里改；规则不奖励无意义的深挖。

完成时 patch 是一条解释：从 violated contract 到 owning state/transition，再到 observable correction；不是一组“看起来可能有用”的行。

> 最小改动不是 diff 最短，而是只改变真正拥有错误语义的地方。