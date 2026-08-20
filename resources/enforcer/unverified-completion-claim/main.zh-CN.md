# unverified-completion-claim — Main

## 现在该做什么
不要让 completion claim 比 evidence 更强。

也不要把 truthful incompleteness 当成工作的替代品。如果你能够准确说出仍然存在的 required work，而且自己仍拥有一个能推进它的 useful action，就去执行那个 action。描述得诚实，不赚取任何 finality credit。

如果取得缺失 observation 属于你的 office，就现在取得它。使用最窄但忠实的 check——它必须真的有能力证明你的 claim 是错的。

如果那个 observation 属于另一个 office，**不要为了制造一个绿色结尾而跨越 role boundary**。把 candidate work 留在可被观察的状态，明确写出什么仍未被观察，并让整体 completion claim 保持 open。

最终宣告结果 complete 的 participant，必须拥有一种真实 evidence：如果实现错了，它有能力让这个声明失败。

## 为什么重要
工程里最常见的谎言通常不是故意造假，而是**语法膨胀**。

“我写了 patch”变成“bug 修好了”。
“unit test 绿了”变成“workflow 正常”。
“build 过了”变成“deployment 安全”。
“代码看起来对”变成“done”。

每一次升级都悄悄跨过一个 evidentiary boundary，却保留上一句话带来的心理确定感。于是非常有能力的团队，也会把未经验证的 assumption 包装成极其专业的 prose 发出去。

Verification 的价值，恰恰在于它被允许反驳作者。一个永远不会让 implementation 难堪的 evidence，只是装饰。

## 修复策略
先把 prose 降级到当前 evidence 真正能够支持的最强 claim。然后找出缺失 observation，以及谁才是它的正当 owner。

对 Coder：通常意味着把 source mutation 做到连贯；需要 regression evidence 时写出可执行 test source；并明确报告 runtime behavior 仍未被观察。不要借用 DevOps authority。

对 DevOps：运行真正相关的 observation，保留实际结果，不要用 optimistic interpretation 把 failure 洗成 success。

对 Manager 或 Reviewer：不要把 subordinate 的 implementation report 当成独立 execution evidence。检查 evidence chain 中，在最终 claim 所依赖的 boundary 上是否真的存在一个 falsifier。

对承担 mission 的 Manager，在任何 ending 之前还要问 residual-action question：“我还能对某项未满足 requirement 做哪一个 useful authorized act？”只要答案能命名一个，就继续。hypothetical future session 不是 transfer target。

优先购买最窄且忠实的 check，但 claim 位于更高 boundary 时，必须继续沿 verification ladder 上升。Unit test 不能认证 deployment；smoke test 也不能认证它从未覆盖的 property。

## 决策分支
- **缺失 observation 属于你：**取得它，报告真正发生了什么，而不是“应该发生什么”。
- **属于另一个 office：**handoff 一个 ready candidate，命名缺失 observation，让整体 claim 保持 open。
- **当前环境无法取得：**说明具体限制，并把 claim 降级到该限制允许的范围。
- **已有 evidence 已经反驳 claim：**这不再是 verification gap。工作没有完成；处理 failure，或把它交回正当 owner。
- **claim 只描述你的 bounded contribution：**明确写出边界，不要让读者把 role-local completion 误读成 whole-system verification。
- **你诚实说出 required work 要留给 “next session” 或 “later”：**除非具体 boundary 阻止继续，或真实 authority-bearing transfer 已经发生，否则这正是反对 finality 的 evidence。继续做，不要把精力花在美化 handoff 上。

## 常见假修复
- 跑一个无关而容易绿的 test，只为了让回复里出现“passed”。这是仪式，不是 evidence。
- 把同一个窄 check 重跑很多次，然后把 repetition 叫做 confidence。反复询问同一个 witness 不会制造 independent witness。
- 引用另一 commit、另一环境或很久以前的 green CI 作为当前 proof。
- 使用“should pass”“looks good”“likely fixed”“没有理由失败”等 modal language，让语气偷偷替 claim 升级。
- 为了让报告看起来 self-contained，给 Coder shell，或绕到另一个 role 执行。这样是用破坏 authority model 的方式修饰 prose。
- 开头写“done”，最后再藏一句“不过没跑测试”。读者按 headline 行动。
- 把经过时间、commit 数、克服的困难、productivity 或整洁 checkpoint 当成“mission 已经做够了”的理由。这些事实可以给 cost/progress 定价，但不能解除 scope。

## 验证
修复后的 invariant：

> 每一个 completion-level claim，要么由当前且相关、并真正有能力反驳它的 observation 支撑；要么被明确限制在一个不会暗示更强结论的 scope 内。

检查最终实际措辞。如果一个合理读者仍可能相信“比 evidence 实际建立的结果更强的东西已经验证”，缺陷仍在。

## 完成条件
记录必须清楚区分：

- 改了什么；
- 推理建立了什么；
- 真正观察到了什么；
- 什么仍未观察；
- 下一次 observation 属于谁。

“complete”只能出现在 evidence 真正赢得它的层级。
