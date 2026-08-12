# serial-investigation — Main

按 epistemic dependency 调度调查。

先把当前要回答的问题列出来，而不是直接列 tool commands。对每个问题问：**我现在已经拥有足够信息把它完整提出吗？** 如果答案是 yes，而且它不需要另一个问题的结果，那它就可以进入同一 parallel evidence wave。

一个健康节奏：

```text
current knowledge
      ↓
form independent questions
      ↓
issue bounded parallel evidence requests
      ↓
synthesize together
      ↓
form next dependent wave
```

这里的 “bounded” 很重要。工具、provider、filesystem、API 都有 capacity。并行的目标是消除虚假的依赖，不是制造无限 fan-out。

调查问题也要足够锋利。好的 parallel wave 由几个**能够区分 competing hypothesis** 的 observation 组成，而不是几十条方向不明的搜索。

常见假修复：

- 把一百条 grep 一次全发出去，结果没人知道每条想证明什么；
- parallel wave 返回后不 synthesis，又立刻继续 fan-out；
- 把本来依赖 previous result 才能正确 formulation 的问题提前猜出来；
- 同时运行会修改同一个环境的 probe，导致 evidence 互相污染；
- 只为了“保持上下文清晰”逐文件 read，即使文件之间完全独立；
- 因为第一条 evidence 看起来很有说服力，就取消原本计划好的独立 falsification。

验证时可以给调查步骤画 DAG。每条 serial edge 都必须能解释成 data dependency、ownership/capacity restriction、或 destructive interference；如果唯一解释是“我习惯一个一个做”，这条 edge 就是人工 latency。

还要看 synthesis 是否真发生。Parallelism 的价值不是同时收集更多 token，而是让多个 independent witness 在 narrative 固化前共同参与 judgment。

当一个结果真正决定下一问时，果断串行。比如先读 stack trace 才知道需要 grep 哪个 symbol；这不是慢，而是尊重 causality。RuleBook 不奖励形式上的并发。

完成时，investigation elapsed time 接近真实 dependency graph 的 critical path，而不是所有独立等待之和；同时每一轮并行都围绕清楚 hypothesis/evidence 关系组织。

> 严谨不是把事实一个个排队。严谨是知道哪些问题可以同时问，哪些问题必须等答案之后才有资格被问。