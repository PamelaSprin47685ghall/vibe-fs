# flaky-test-tolerated — Lesson

## 评价

### 定义

Flaky test 不是“偶尔失败的 test”。它是一个 measuring instrument：在 test 自己认为“相关输入等价”的情况下，verdict 仍然会变；更糟的是，团队已经决定把这种歧义当成日常生活的一部分。

本规则抓的是**容忍**，不是第一次发现。一次无法解释的 intermittent red 是值得调查的 evidence。只有当 rerun、quarantine、团队 folklore 或选择性不相信 red，把 nondeterminism 变成正常流程时，它才成为制度性 defect。

### 支配原则

Test 有两个职责：发现有意义的区别，并让 verdict 可以解释。

如果同一份 relevant state 可以得到 red 也可以得到 green，suite 就无法区分 product change 与 measurement noise。最先损失的不是 CI 速度，而是认识能力：以后每一个 red 都多了逃生门——“probably flaky”；每一个 green 也要打折——“maybe lucky”。

一个被容忍的 flake 会教会团队一种比这条 test 本身昂贵得多的习惯：**先 rerun，再决定 red 值不值得信。** 一旦这个习惯扩散，真实 regression 与噪声会得到同样待遇。

### 何时触发

当已知 nondeterminism 仍留在一条被当作 evidence 使用的 test 中，并被制度化接受时触发，例如：

- CI 自动重跑失败 test，只报告最后一次 green；
- 团队看到第一条 red 的默认反应是“再跑一次”；
- test 被 quarantine / skip / 标记 known flaky，却没有 owner、退出标准或明确修复期限，同时还被算作“覆盖”；
- timing window 被不断放宽，却没有定位 hidden input；
- failure 依赖 order、clock、random seed、shared residue、race、resource pressure 或 external service state，而这些输入仍未受控；
- 对 deterministic contract 接受“多数时候会过”。

### 不应触发

- Stochastic/property test 会记录 random seed，并且同一个 seed 有 deterministic verdict。
- 一次 intermittent failure 正被当成真实 defect 保存和调查，没有被正常化。
- 产品 contract 本身就是概率性的，并且 test 使用明确统计规则验证那个概率 contract；此时 nondeterminism 是 specification 的一部分，不是测量噪声。
- 真正 external transient 由显式 infrastructure policy 进行 bounded retry，而 correctness assertion 自身仍 deterministic，最终 failure 也不会被吞掉。

### 与相邻规则区分

`repeat-until-pass` 是从混合 outcome 中主动挑一个 favorable sample。`flaky-test-tolerated` 是更上层的 policy failure：这个不稳定仪器仍然被允许拥有 authority。

`time-dependent-test`、`order-dependent-test`、`random-source-in-logic`、shared-state 类规则往往能指出具体 mechanism。Cause 已知时用更具体规则；中心伤口是“changing verdict 被正常化”时，本规则最准确。

### 判定程序

先问：两次被 suite 认为是“同一个 experiment”的运行，是否可能产生不同 verdict？

如果会，找 hidden input：clock、seed、scheduler、order、process lifetime、network state、shared database、filesystem residue、port allocation、environment、resource pressure。

然后问 policy：suite 是否仍把这条 test 当成可信 evidence，却没有控制那个 input？如果是，本规则成立。

### 例子

- positive：CI 最多 retry 3 次；只要有一次通过 job 就 green，而 flake 无限期留在树里。
- positive：test 在 parallel execution 下偶尔失败，于是整个 suite 被强制 serial，而真正 leaked shared state 从未修复；大家仍称这条 test reliable。
- positive：“temporary quarantine” 六个月后仍无 owner、日期或 exit criterion。
- near-miss：一次 intermittent failure 被保留下来，调查发现 unseeded random choice；seed 显式化后 test 才恢复 trusted status。
- counterexample：property test 报告 seed `0xabc`，重放这个 seed 可以 deterministic 地复现 failure。

## 建议

### 现在该做什么

在“一次 experiment 对应一个可解释 verdict”恢复之前，不要继续把这条 test 当作普通、可信的 evidence。

保存 failure reproduction，找出 hidden input，然后控制它；如果做不到，就把这条 test 从 evidence chain 中移除。不要一边给它加 retry、quarantine label、团队口头说明，一边仍让它拥有“证明正确”的 authority。

### 为什么重要

Flakiness 消耗的远不只是 CI minutes。

它会腐蚀 failure 的语义。一旦工程师学会“red 可能没事”，每个真实 regression 都获得 plausible deniability。默认流程会倒转成“先 rerun，后调查”，而第一条无法解释的 red 往往恰恰是你能得到的最便宜证据——race、resource leak、stale state、missing causal signal 通常就在里面。

Flaky suite 还制造一种社会偏差：green 不需要辩护，red 必须先证明自己不是噪声。这个 selection bias 会系统性低估 defect。

### 修复策略

让 hidden inputs 变成显式、有 owner 的输入：

- 注入/freeze time，不要和 wall clock 赛跑；
- 记录 random seed，并让 replay 精确；
- 每条 test 独立拥有 filesystem/database/global state；
- 删除 order coupling 与 shared mutable fixture；
- 等待 causal signal，而不是 sleep；
- 有意控制 concurrency，并在可能时用 deterministic coordination 暴露 race；
- 当 live external dependency 不是 test 本体时，用 deterministic boundary contract 替代；
- 当 external system 本身就是 test subject 时，明确建模 transient/failure policy，不要假装环境 deterministic。

保留最初的 failure 价值。不要通过“让 observation 更看不见 defect”来修 test。

### 决策分支

- **Hidden input 已找到且可控：**把它显式化，保留 test。
- **依赖 shared residue：**给 test 独立 ownership 与 cleanup；必要时用 shuffled/parallel execution 证明 independence。
- **产品 contract 天生 probabilistic：**明确 statistical acceptance criterion、seed/sample policy。不要 ad-hoc rerun。
- **无法做到足够 deterministic 以支撑 claim：**替换或删除。Missing test 比 fake evidence 诚实。
- **修复期间必须 quarantine：**给 owner、具体 defect link、bounded exit criterion；在此期间不要把它算作 trusted coverage。

### 常见假修复

- 把 retry count 调高到 failure rate 在社交上可接受。
- 加 sleep / 扩 timeout，让 favorable schedule 更容易出现。
- 因为一条 test 漏 shared state，就把整个 suite 强制 serial；除非 serialization 真的是被测试的 product invariant。
- CI 中 catch/ignore flaky assertion，local 继续装作它有价值。
- 因为删除会“降低 coverage”而永久保留 quarantine。坏 witness 不会因为一直站在那里就变好。
- 连跑 100 次都 green，就宣称 causality repaired，却说不出到底修了哪个 hidden input。稳定性 sampling 可以在 repair 后增加 confidence，但它不是 repair。

### 验证

证明必须是 causal，而不是统计幻觉：

1. 指出使 verdict 分叉的 input / mechanism；
2. 控制或删除它；
3. 让 test 的 experiment definition 变得明确；
4. 证明一个相关 defect 仍能把它打红。

修复后的 repeated runs 可以作为 stress check，但再多 lucky green 也不能替代 1–4。

Invariant：

> 等价的 relevant inputs 产生一个 verdict，并且这个 verdict 只有一个清晰含义。

### 完成条件

Red 再次变得可行动。

没人需要“probably flaky”、rerun 按钮或运气来决定这条 test 是否在说真话。

### Nudge

Flaky test 是坏掉的仪器，不是性格古怪的同事。

修 hidden input，或者退休这个仪器。不要训练团队和 red 讨价还价。
