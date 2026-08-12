# release-ladder-skipped — Enforcer

Release ladder skipped 的问题，不是“少跑了一条 command”，而是验证直接跳到更宽、更贵、更混杂的层，**窄层本来可以独立否证的 uncertainty 还没被清掉**。

Pure/unit/property、boundary contract、replay/recovery、integration、real canary 各自回答不同问题。Broad test 成功不能自动替代 narrow proof：它可能根本没触发那个 local invariant；失败时反而把几十种可能 cause 混在一起。

以下情形触发：

- local algebra 改了，只跑 staging/E2E；
- wire contract 改了，跳过 codec/contract test 直接跑 full Host；
- recovery semantics 改了，没有 replay/fault test，只看正常 integration；
- applicable lower test 红着，仍继续用更高层 green 当“总体没问题”；
- “CI 后面会跑”被用来跳过当前最窄可失败 proof。

不要把 ladder 变成固定清单宗教。Docs-only/content-only change 不需要 runtime rung；某个项目没有某类 test 也不需要虚构它。关键是**change 实际触及哪些 uncertainty**，每一种先由最低 faithful boundary 证明。

与 `canary-skipped` 区分：那条专门缺真实 external owner 的 empirical proof；本规则管证据顺序与层级。与 `unverified-completion-claim` 区分：后者最终证据总量不足；这里可能跑了很多，只是窄因果 proof 被 broad realism 覆盖掉。

判定问题：对每个 changed promise，哪一层是**最便宜、最可定位、仍能把错误打红**的 proof？只要这一层 applicable 却被跳过，后面的 broad green 不能替它还债。

> Broad realism 应建立在 narrow causality 上。不要用“跑得更像生产”掩盖“我们还没证明最小那条规则”。