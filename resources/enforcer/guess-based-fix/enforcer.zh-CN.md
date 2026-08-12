# guess-based-fix — Enforcer

## 定义
Guess-based fix 是一种 patch：它被留下来的理由是“symptom 变了”，而不是“一个 causal explanation 经受住了反驳”。

它的样子非常熟：调 timeout、加 retry、换调用顺序、clear cache、加 lock、改 flag、放宽 parser、catch exception——test 绿了就停。Patch 可能碰巧有效，但 repository 没有因此获得知识，因为**没人知道为什么有效**。

## 支配原则
Passing configuration 不是 causal explanation。

复杂系统里有无数 intervention 会改变 timing、scheduler order、cache state、resource pressure、retry、error visibility 或 race probability。很多动作都能让 symptom 暂时消失，却完全没有修 violated invariant。

真正 repair 至少要回答两件事：

1. 旧系统为什么会产生已经观察到的 failure？
2. 当前 change 为什么阻止那个 mechanism，而不只是把 symptom 藏起来？

如果解释除了“tests 现在 green”之外不能预测任何东西，那当前工作仍然只是一个披着 repair 外衣的 experiment。

## 何时触发
当 mutation 被当成 solution-space blind search，并在第一个 favorable outcome 出现后停止，却没有 causal discrimination 时触发。常见形式：

- 多个互不相关的 edits 一起 landing，bundle green 后大家把整体叫作“fix”；
- timeout/retry/cache/concurrency knobs 被反复调整，直到 flakiness 消失；
- catch exception / ignore error 只因为“这样不 crash”，没人证明 operation 是否开始 silent fail；
- race 出现就加 global lock，却没指出哪个 shared invariant 真正需要 exclusion；
- 小 failure mechanism 从未被隔离，于是直接 wholesale rewrite；
- AI-generated patch 同时改很多 plausible sites，suite green 就被当成“模型找对 cause”；
- failed speculative edits 以“harmless cleanup”名义留在树里，使后来再也无法知道到底哪一个 change 有效。

## 不应触发
- Reversible probe 被明确用来区分 named hypotheses，并在不能支持 hypothesis 时撤掉。
- 多个 edits 只是一个已建立 causal repair 的机械后果。
- 系统初期确实需要 experimental exploration，但最终留下的 fix 被缩小，并针对 causal hypothesis 验证。
- 当前动作明确只是 mitigation、只降低 impact，没有假装 root cause 已关闭；remaining uncertainty 被如实保留。
- Broad rewrite 本身由任务独立要求，而且相关 behavior 都有验证；“改得多”本身不是猜。

## 与相邻规则区分
`guessed-not-verified` 是 mutation 前的 epistemic debt：一个重要 premise 没查 owner 就被当 fact。`guess-based-fix` 则是 search-by-mutation：代码/settings 被改来改去，直到 symptom 移动。

`blind-edit` 更强调还没找 ownership 就动手。`repeat-until-pass` 完全不改代码，只重抽 execution sample。这里的 search variable 是 implementation 本身。

## 判定程序
要求作者说出 violated invariant，以及一个可证伪 mechanism：它如何导致已经观察到的 failure。

再问：什么 observation 能把这个 mechanism 与至少一个 plausible alternative 区分开？如果 patch 在这种 discrimination 之前就被决定，重新建立 experiment：撤掉无关 changes，保留 failing case，只逐步引入有 causal justification 的 change。

如果解释中最有力的一句话仍然是“it passed after this”，本规则成立。

## 例子
- positive：CI flaky；同时改 timeout、retry、worker count；suite green，bundle 直接以“stability fix”交付。
- positive：加 global lock 后 race 消失，但没人知道 shared state 在哪里；lock 可能只是在串行化无关工作、降低撞 race 的概率。
- positive：agent 同时改三个 parser、cache layer 与 error handling；tests pass，却没有一个 failing input 被绑定到其中任何一个 change。
- near-miss：先提出两个 hypothesis；targeted probe 排除 cache corruption，另一个 probe 复现 lost update；patch 最终只修 ownership conflict，并增加 regression。
- counterexample：violated invariant 已经明确，多个 call sites 只是机械地一致迁移到新 enforcement。

## Nudge
“It passes now” 是 observation，不是 explanation。

留下那个能够解释 failure 的 change。把只是在好运发生时恰好站在旁边的 changes 扔掉。
