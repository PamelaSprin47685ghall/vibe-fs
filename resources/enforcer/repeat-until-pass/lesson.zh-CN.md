# repeat-until-pass — Lesson

## 评价

### 定义

Repeat-until-pass 是**把 outcome selection 伪装成 verification**。

同一个、或实质等价的 experiment 同时产生 red 与 green；操作者不解释这个矛盾，而是继续抽样，直到出现自己想要的结果。最后那个 green 被提拔成“真相”，前面的 reds 则像从未发生。

这是工程版 p-hacking：一直抽样，直到现实说出你想听的话。

### 支配原则

一个与后续 observation 冲突的 red，不会因为后来出现了方便的 green 就失去真实性。

Relevant inputs 没有变化，verdict 却变化时，目前最强的事实不是“它大概好了”，而是：**系统或测量存在 nondeterminism**。在这个事实被解释之前，挑 green 只是主动销毁不方便的 evidence。

Retry 当然有合法用途——例如 protocol 明确定义的 transient network retry、idempotent acquisition、基于 causal readiness 的 polling。非法的是：把已经失败的 correctness assertion 反复执行，直到买到一个想要的 verdict。

### 何时触发

当一个 correctness check 失败后，在实质相同条件下被重跑到某次通过，并且那个 favorable attempt 被接受，却没有解释之前 failures 时触发。常见形式：

- local test fail，工程师不断按 up-arrow，直到 green，然后报告成功；
- CI 配置“flaky tests retry N times”，任意一次 green 就把 job 洗成 passing；
- integration check 被 shell loop 到 exit 0，之前 output 被丢弃；
- agent 被反复要求“再跑一遍 tests”，但代码/config 根本没变，也不调查为什么 verdict 混合；
- timeout/scheduler noise 后偶然 green，被当成确认，即使两次运行间没有任何 causal change；
- 在多个环境/调度下取样，只引用那个 passing sample。

### 不应触发

- 第一次 failure 已被一个明确 input change 或 causal repair 解释；后一次因此是真正的新 experiment。
- 明确建模、bounded 的 retry 正在处理 known external transient，而且最终 failure 仍然可见。
- Polling 等待 causal readiness，不是把一个已经失败的 assertion 重新解释成“还没准备好”。
- Repetition 在 causal repair **之后**作为 stress sampling；正确性 claim 并不依赖“最终总能找到一次 green”。
- Property/stochastic test 使用明确 sampling contract，而不是“出现一次 pass 就停”。

### 与相邻规则区分

`flaky-test-tolerated` 是允许不稳定仪器继续拥有 authority 的 policy failure。`repeat-until-pass` 是具体的 cherry-picking 行为：从混合 outcome 里挑一个 favorable sample。

`timeout-inflated-to-pass` 是修改等待 budget，让有利 schedule 更容易出现。`sleep-based-synchronization` 是用 elapsed time 代替 readiness signal。这些可以共存；本规则抓的是“面对矛盾 evidence，不解释，改为挑 green”。

### 判定程序

第一条无法解释的 red 出现后，停止 sampling。

记录 exact relevant inputs，并问：下一次 run 前是否发生了任何真正具有因果意义的变化？如果没有，后来 green 就没有权力抹掉之前 red。

下一步只能是找 hidden variable——seed、time、order、scheduler、resource pressure、external state、shared residue——或者证明第一次 failure 属于一个单独建模的 transient。

如果流程本质只是“run until pass”，本规则成立。

### 例子

- positive：test 连续失败两次，第三次同样命令通过，于是第三次 output 被粘进 completion report。
- positive：CI 每条 failure 最多 retry 三次，只要有一次 pass 整个 suite 就 green，而且没有任何 flaky debt。
- positive：agent 在没有 code/config change 的情况下重复执行同一个 failing command，直到一次 return 0。
- near-miss：failed test 暴露 unseeded random branch；seed 固定、cause 修复，下一次 run 使用受控输入。
- counterexample：HTTP client 按明确 protocol 对 idempotent 503 做 bounded retry，重试耗尽后仍报告 failure。

## 建议

### 现在该做什么

停止继续重跑 correctness check。

把第一条无法解释、且与后续 verdict 冲突的 observation 当作真正 defect。保留它的 output，记录 relevant inputs/environment，并找出什么 hidden variable 能让实质等价的 runs 产生不同结果。

只有当你能够解释“为什么后一次已经是一个不同 experiment”，或“产生 red 的 mechanism 已被修复”时，后续 green 才有资格成为新 evidence。

### 为什么重要

Repeat-until-pass 会把 verification 变成 selection bias。

问题悄悄从：

> 系统在这些条件下是否正确？

变成：

> 我是否最终能抽到一个 schedule/environment，让它看起来 green？

这两个 proposition 完全不是一回事。

在自动化 coding workflow 里，这种坏习惯尤其便宜：tool call 几乎没有心理成本，于是重复调用很容易制造“证据变多了”的错觉。实际上，如果每次都在向同一个不稳定系统重复问同一个坏问题，十次 invocation 并不会因为 timestamp 不同就变成十个 independent witnesses。

### 修复策略

在继续改 implementation 之前，先冻结 experiment：

- 保存 test name、seed、timing、order、environment、process state、external dependencies、shared resources；
- 保留第一条 red output；
- 找出 nominally identical commands 之间实际可能变化的东西；
- 为这个 hidden variable 设计 discriminating observation；
- 修真正拥有 cause 的 mechanism；
- 然后在受控条件下执行一次，作为主要 correctness observation。

如果 retry 确实是合法 infrastructure behavior，把它变成显式 bounded policy：命名 transient class、声明 idempotence assumptions、必要时 backoff，并确保最终 failure 仍然 visible。

### 决策分支

- **Red 与 green 之间没有任何有意义变化：**green 不能结案，继续调查 nondeterminism。
- **发生了 causal fix：**下一次 run 是新 experiment，可以正常使用。
- **确有 known external transient：**走显式 retry policy，不要 ad-hoc 重跑，并保留 transient classification 的 evidence。
- **Test 本身 flaky：**修或退休它；见 `flaky-test-tolerated`。
- **Command 实际是在 poll readiness：**使用 causal condition + bounded deadline，不要把 failed assertion 重新包装成 polling。

### 常见假修复

- 增大 retry count，直到 failure 统计上变得“很少”。
- Shell loop command，只打印第一次 green。
- 写“passed on retry”，却不解释为什么第一次结果可以被判无效。
- 平均 failure rate，然后认为 deterministic contract 只要低于某个百分点就够好。
- Restart process、clear cache、删 temp state，直到 green，然后在报告里省略这些 interventions。你已经改变 experiment；隐藏这种变化会毁掉 evidence。
- 让另一台机器/另一个 agent 跑同一个 check，只为了买第二次中奖机会。

### 验证

修复后 correctness 不能再依赖“找到 favorable sample”。

主要 proof 是：

1. hidden variable / transient class 已被识别；
2. mechanism 已被控制或修复；
3. 一次 explicit-condition run 有稳定含义；
4. 原 failure 不能在同样条件下随意再现，否则你的解释自相矛盾。

Repeated execution 可以 stress repaired system，但不能再作为发现“pass”的算法。

### 完成条件

Green 被接受，是因为 experiment 可解释，而不是因为买了足够多次尝试，终于抽中一次。

之前的 red 必须拥有解释，而不是橡皮擦。

### Nudge

不要按 outcome 选择 evidence。

一条无法解释的 red 足以让 lucky green 失去结案资格，直到你能说出到底什么变了。
