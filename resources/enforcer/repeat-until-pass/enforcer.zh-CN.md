# repeat-until-pass — Enforcer

## 定义
Repeat-until-pass 是**把 outcome selection 伪装成 verification**。

同一个、或实质等价的 experiment 同时产生 red 与 green；操作者不解释这个矛盾，而是继续抽样，直到出现自己想要的结果。最后那个 green 被提拔成“真相”，前面的 reds 则像从未发生。

这是工程版 p-hacking：一直抽样，直到现实说出你想听的话。

## 支配原则
一个与后续 observation 冲突的 red，不会因为后来出现了方便的 green 就失去真实性。

Relevant inputs 没有变化，verdict 却变化时，目前最强的事实不是“它大概好了”，而是：**系统或测量存在 nondeterminism**。在这个事实被解释之前，挑 green 只是主动销毁不方便的 evidence。

Retry 当然有合法用途——例如 protocol 明确定义的 transient network retry、idempotent acquisition、基于 causal readiness 的 polling。非法的是：把已经失败的 correctness assertion 反复执行，直到买到一个想要的 verdict。

## 何时触发
当一个 correctness check 失败后，在实质相同条件下被重跑到某次通过，并且那个 favorable attempt 被接受，却没有解释之前 failures 时触发。常见形式：

- local test fail，工程师不断按 up-arrow，直到 green，然后报告成功；
- CI 配置“flaky tests retry N times”，任意一次 green 就把 job 洗成 passing；
- integration check 被 shell loop 到 exit 0，之前 output 被丢弃；
- agent 被反复要求“再跑一遍 tests”，但代码/config 根本没变，也不调查为什么 verdict 混合；
- timeout/scheduler noise 后偶然 green，被当成确认，即使两次运行间没有任何 causal change；
- 在多个环境/调度下取样，只引用那个 passing sample。

## 不应触发
- 第一次 failure 已被一个明确 input change 或 causal repair 解释；后一次因此是真正的新 experiment。
- 明确建模、bounded 的 retry 正在处理 known external transient，而且最终 failure 仍然可见。
- Polling 等待 causal readiness，不是把一个已经失败的 assertion 重新解释成“还没准备好”。
- Repetition 在 causal repair **之后**作为 stress sampling；正确性 claim 并不依赖“最终总能找到一次 green”。
- Property/stochastic test 使用明确 sampling contract，而不是“出现一次 pass 就停”。

## 与相邻规则区分
`flaky-test-tolerated` 是允许不稳定仪器继续拥有 authority 的 policy failure。`repeat-until-pass` 是具体的 cherry-picking 行为：从混合 outcome 里挑一个 favorable sample。

`timeout-inflated-to-pass` 是修改等待 budget，让有利 schedule 更容易出现。`sleep-based-synchronization` 是用 elapsed time 代替 readiness signal。这些可以共存；本规则抓的是“面对矛盾 evidence，不解释，改为挑 green”。

## 判定程序
第一条无法解释的 red 出现后，停止 sampling。

记录 exact relevant inputs，并问：下一次 run 前是否发生了任何真正具有因果意义的变化？如果没有，后来 green 就没有权力抹掉之前 red。

下一步只能是找 hidden variable——seed、time、order、scheduler、resource pressure、external state、shared residue——或者证明第一次 failure 属于一个单独建模的 transient。

如果流程本质只是“run until pass”，本规则成立。

## 例子
- positive：test 连续失败两次，第三次同样命令通过，于是第三次 output 被粘进 completion report。
- positive：CI 每条 failure 最多 retry 三次，只要有一次 pass 整个 suite 就 green，而且没有任何 flaky debt。
- positive：agent 在没有 code/config change 的情况下重复执行同一个 failing command，直到一次 return 0。
- near-miss：failed test 暴露 unseeded random branch；seed 固定、cause 修复，下一次 run 使用受控输入。
- counterexample：HTTP client 按明确 protocol 对 idempotent 503 做 bounded retry，重试耗尽后仍报告 failure。

## Nudge
不要按 outcome 选择 evidence。

一条无法解释的 red 足以让 lucky green 失去结案资格，直到你能说出到底什么变了。
