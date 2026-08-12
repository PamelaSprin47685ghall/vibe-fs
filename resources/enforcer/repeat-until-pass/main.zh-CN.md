# repeat-until-pass — Main

## 现在该做什么
停止继续重跑 correctness check。

把第一条无法解释、且与后续 verdict 冲突的 observation 当作真正 defect。保留它的 output，记录 relevant inputs/environment，并找出什么 hidden variable 能让实质等价的 runs 产生不同结果。

只有当你能够解释“为什么后一次已经是一个不同 experiment”，或“产生 red 的 mechanism 已被修复”时，后续 green 才有资格成为新 evidence。

## 为什么重要
Repeat-until-pass 会把 verification 变成 selection bias。

问题悄悄从：

> 系统在这些条件下是否正确？

变成：

> 我是否最终能抽到一个 schedule/environment，让它看起来 green？

这两个 proposition 完全不是一回事。

在自动化 coding workflow 里，这种坏习惯尤其便宜：tool call 几乎没有心理成本，于是重复调用很容易制造“证据变多了”的错觉。实际上，如果每次都在向同一个不稳定系统重复问同一个坏问题，十次 invocation 并不会因为 timestamp 不同就变成十个 independent witnesses。

## 修复策略
在继续改 implementation 之前，先冻结 experiment：

- 保存 test name、seed、timing、order、environment、process state、external dependencies、shared resources；
- 保留第一条 red output；
- 找出 nominally identical commands 之间实际可能变化的东西；
- 为这个 hidden variable 设计 discriminating observation；
- 修真正拥有 cause 的 mechanism；
- 然后在受控条件下执行一次，作为主要 correctness observation。

如果 retry 确实是合法 infrastructure behavior，把它变成显式 bounded policy：命名 transient class、声明 idempotence assumptions、必要时 backoff，并确保最终 failure 仍然 visible。

## 决策分支
- **Red 与 green 之间没有任何有意义变化：**green 不能结案，继续调查 nondeterminism。
- **发生了 causal fix：**下一次 run 是新 experiment，可以正常使用。
- **确有 known external transient：**走显式 retry policy，不要 ad-hoc 重跑，并保留 transient classification 的 evidence。
- **Test 本身 flaky：**修或退休它；见 `flaky-test-tolerated`。
- **Command 实际是在 poll readiness：**使用 causal condition + bounded deadline，不要把 failed assertion 重新包装成 polling。

## 常见假修复
- 增大 retry count，直到 failure 统计上变得“很少”。
- Shell loop command，只打印第一次 green。
- 写“passed on retry”，却不解释为什么第一次结果可以被判无效。
- 平均 failure rate，然后认为 deterministic contract 只要低于某个百分点就够好。
- Restart process、clear cache、删 temp state，直到 green，然后在报告里省略这些 interventions。你已经改变 experiment；隐藏这种变化会毁掉 evidence。
- 让另一台机器/另一个 agent 跑同一个 check，只为了买第二次中奖机会。

## 验证
修复后 correctness 不能再依赖“找到 favorable sample”。

主要 proof 是：

1. hidden variable / transient class 已被识别；
2. mechanism 已被控制或修复；
3. 一次 explicit-condition run 有稳定含义；
4. 原 failure 不能在同样条件下随意再现，否则你的解释自相矛盾。

Repeated execution 可以 stress repaired system，但不能再作为发现“pass”的算法。

## 完成条件
Green 被接受，是因为 experiment 可解释，而不是因为买了足够多次尝试，终于抽中一次。

之前的 red 必须拥有解释，而不是橡皮擦。
