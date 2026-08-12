# flaky-test-tolerated — Enforcer

## 定义
Flaky test 不是“偶尔失败的 test”。它是一个 measuring instrument：在 test 自己认为“相关输入等价”的情况下，verdict 仍然会变；更糟的是，团队已经决定把这种歧义当成日常生活的一部分。

本规则抓的是**容忍**，不是第一次发现。一次无法解释的 intermittent red 是值得调查的 evidence。只有当 rerun、quarantine、团队 folklore 或选择性不相信 red，把 nondeterminism 变成正常流程时，它才成为制度性 defect。

## 支配原则
Test 有两个职责：发现有意义的区别，并让 verdict 可以解释。

如果同一份 relevant state 可以得到 red 也可以得到 green，suite 就无法区分 product change 与 measurement noise。最先损失的不是 CI 速度，而是认识能力：以后每一个 red 都多了逃生门——“probably flaky”；每一个 green 也要打折——“maybe lucky”。

一个被容忍的 flake 会教会团队一种比这条 test 本身昂贵得多的习惯：**先 rerun，再决定 red 值不值得信。** 一旦这个习惯扩散，真实 regression 与噪声会得到同样待遇。

## 何时触发
当已知 nondeterminism 仍留在一条被当作 evidence 使用的 test 中，并被制度化接受时触发，例如：

- CI 自动重跑失败 test，只报告最后一次 green；
- 团队看到第一条 red 的默认反应是“再跑一次”；
- test 被 quarantine / skip / 标记 known flaky，却没有 owner、退出标准或明确修复期限，同时还被算作“覆盖”；
- timing window 被不断放宽，却没有定位 hidden input；
- failure 依赖 order、clock、random seed、shared residue、race、resource pressure 或 external service state，而这些输入仍未受控；
- 对 deterministic contract 接受“多数时候会过”。

## 不应触发
- Stochastic/property test 会记录 random seed，并且同一个 seed 有 deterministic verdict。
- 一次 intermittent failure 正被当成真实 defect 保存和调查，没有被正常化。
- 产品 contract 本身就是概率性的，并且 test 使用明确统计规则验证那个概率 contract；此时 nondeterminism 是 specification 的一部分，不是测量噪声。
- 真正 external transient 由显式 infrastructure policy 进行 bounded retry，而 correctness assertion 自身仍 deterministic，最终 failure 也不会被吞掉。

## 与相邻规则区分
`repeat-until-pass` 是从混合 outcome 中主动挑一个 favorable sample。`flaky-test-tolerated` 是更上层的 policy failure：这个不稳定仪器仍然被允许拥有 authority。

`time-dependent-test`、`order-dependent-test`、`random-source-in-logic`、shared-state 类规则往往能指出具体 mechanism。Cause 已知时用更具体规则；中心伤口是“changing verdict 被正常化”时，本规则最准确。

## 判定程序
先问：两次被 suite 认为是“同一个 experiment”的运行，是否可能产生不同 verdict？

如果会，找 hidden input：clock、seed、scheduler、order、process lifetime、network state、shared database、filesystem residue、port allocation、environment、resource pressure。

然后问 policy：suite 是否仍把这条 test 当成可信 evidence，却没有控制那个 input？如果是，本规则成立。

## 例子
- positive：CI 最多 retry 3 次；只要有一次通过 job 就 green，而 flake 无限期留在树里。
- positive：test 在 parallel execution 下偶尔失败，于是整个 suite 被强制 serial，而真正 leaked shared state 从未修复；大家仍称这条 test reliable。
- positive：“temporary quarantine” 六个月后仍无 owner、日期或 exit criterion。
- near-miss：一次 intermittent failure 被保留下来，调查发现 unseeded random choice；seed 显式化后 test 才恢复 trusted status。
- counterexample：property test 报告 seed `0xabc`，重放这个 seed 可以 deterministic 地复现 failure。

## Nudge
Flaky test 是坏掉的仪器，不是性格古怪的同事。

修 hidden input，或者退休这个仪器。不要训练团队和 red 讨价还价。
