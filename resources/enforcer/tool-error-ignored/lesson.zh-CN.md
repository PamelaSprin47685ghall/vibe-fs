# tool-error-ignored — Lesson

## 评价

### 定义

Tool error ignored 的问题，不是“console 里有一行红字”，而是一个 observation 已经明确否定某个前提，workflow 却继续使用那个前提作后续决定。

Tool call 失败后，至少有一件事成立：**你没有获得原本希望获得的事实或 effect guarantee**。继续把 missing/failed observation 当成功结果使用，就是把 uncertainty 洗成 certainty。

### 支配原则

一次失败的 tool call 制造一个 epistemic gap。想要的事实没有建立，任何依赖它的结论都失去一个 premise。只有 error 被显式分类、且独立来源提供等价 evidence 时，继续才是合法的；否则 workflow 静默把 “unknown” 变成 “probably fine”。

### 何时触发

以下情形触发：

- build/test/lint 返回 nonzero，流程仍宣称 verified；
- file read/grep/query fail，却按“没找到”继续；
- provider/tool 返回 error，caller 仍解析空/default payload 当事实；
- process spawn fail，却继续等待不存在的 output；
- write/edit 失败后，后续步骤假设 mutation 已落盘；
- external API error 被 log 后 swallowed，业务仍进入 success branch。

### 不应触发

不要误杀**明确建模的 best-effort**。如果某个 telemetry source 可选、失败只降低 enrichment，policy 明确知道 `Unavailable` 并且核心 correctness 不依赖它，继续完全合理。关键是 failure 是否破坏了一个 load-bearing premise。

### 与相邻规则区分

与 `expected-failure-as-exception` 区分：那条讨论 expected outcome 用什么 channel 表达；本规则不管 error 类型漂亮不漂亮，只管 caller 已经收到 failure 却假装没收到。与 `unverified-completion-claim` 区分：那里可能根本没运行 tool；这里运行了，而且 evidence 已经明确反对你的 confidence。

### 判定程序

最锋利的问题：**这次 tool call 原本要证明/建立什么？失败后，那条 claim 现在还能成立吗？** 不能，就必须停止、降级 claim、retry/recover，不能继续走 success semantics。

### 例子

- positive：test run 失败，agent 忽略 stderr，报告 feature complete。
- near-miss：search 因路径缺失而失败；agent 记录这一点，并通过读 parent listing 证明同一事实。
- counterexample：tests 根本没跑，却照样断言完成——那是 `unverified-completion-claim`。

## 建议

### 现在该做什么

把 tool failure 重新连接到它所否定的 premise。

对每个 tool call 先声明用途：它是在获取 fact、执行 mutation、建立 verification、还是做可选 enrichment？只有这样，failure 才能有正确 policy：stop、retry、recover、degrade、mark unknown，而不是统一 `catch/log/continue`。

### 为什么重要

Tool failure 不只是不便，它改变已知的东西。忽略它会让后续步骤继承一个从未建立的 premise，于是打磨过的最终结果可能坐落在 evidence chain 的一个静默空洞上。

### 修复策略

常见 repair：

- fact query fail → 返回 `Unknown/Unavailable`，不能当 empty/not-found；
- mutation fail → 后续不得假设世界已改变；
- verification fail → completion claim 降级或 work 继续；
- optional enrichment fail → 明确保留 core result，并记录 enrichment unavailable；
- transient effect fail → 只有 idempotency/unknown-outcome protocol 允许时才 retry。

### 决策分支

如果后续结论依赖失败 operation 的预期结果，修复它、在合理语义下 retry，或用等价 evidence 替换。如果 failure 无关紧要、另一个来源已经证明同一 property，记录该分类并继续。

### 常见假修复

常见假修复：

- catch everything，返回空数组/null/default；
- log error 后继续 success branch；
- tool fail 就自动 retry，不区分 non-idempotent/known-invalid；
- 把 nonzero exit 当 warning，因为“别的测试都过了”；
- failed grep/read 当成“仓库里没有这个 symbol”；
- mutation command 输出 error，却仍跑 verification 并把旧文件 green 当新代码 green。

### 验证

验证要故意让 tool 失败，并观察**下游 claim 是否被正确收回**。Query failure 不得变成 negative fact；write failure 不得产生 success event；test failure 不得进入 verified completion；optional telemetry failure也不能把整个产品 result 误判失败。

Error handling 的核心不是“所有 failure 都 fatal”，而是保持 epistemic integrity：知道什么、做成什么，只能由实际 outcome 决定。

如果某个 tool error 的协议现在只能靠 parsing prose，先解决 typed identity/stringly error；如果 failure 可能已产生 unknown external effect，再接 idempotency/reconciliation，而不是盲 retry。

### 完成条件

完成时每条 failure path 都能回答“这次 error 让哪条 premise 失效，因此哪条后续行为被禁止/改变”。

> 正确处理 error，不是看到红就停止；是绝不让已经被证伪的前提继续伪装成成功。

### Nudge

> Error 不是噪声，它是世界拒绝给你某份证据或效果的正式回答。忽略它，不会让原来的前提重新变真。
