# tool-error-ignored — Enforcer

Tool error ignored 的问题，不是“console 里有一行红字”，而是一个 observation 已经明确否定某个前提，workflow 却继续使用那个前提作后续决定。

Tool call 失败后，至少有一件事成立：**你没有获得原本希望获得的事实或 effect guarantee**。继续把 missing/failed observation 当成功结果使用，就是把 uncertainty 洗成 certainty。

以下情形触发：

- build/test/lint 返回 nonzero，流程仍宣称 verified；
- file read/grep/query fail，却按“没找到”继续；
- provider/tool 返回 error，caller 仍解析空/default payload 当事实；
- process spawn fail，却继续等待不存在的 output；
- write/edit 失败后，后续步骤假设 mutation 已落盘；
- external API error 被 log 后 swallowed，业务仍进入 success branch。

不要误杀**明确建模的 best-effort**。如果某个 telemetry source 可选、失败只降低 enrichment，policy 明确知道 `Unavailable` 并且核心 correctness 不依赖它，继续完全合理。关键是 failure 是否破坏了一个 load-bearing premise。

与 `expected-failure-as-exception` 区分：那条讨论 expected outcome 用什么 channel 表达；本规则不管 error 类型漂亮不漂亮，只管 caller 已经收到 failure 却假装没收到。与 `unverified-completion-claim` 区分：那里可能根本没运行 tool；这里运行了，而且 evidence 已经明确反对你的 confidence。

最锋利的问题：**这次 tool call 原本要证明/建立什么？失败后，那条 claim 现在还能成立吗？** 不能，就必须停止、降级 claim、retry/recover，不能继续走 success semantics。

> Error 不是噪声，它是世界拒绝给你某份证据或效果的正式回答。忽略它，不会让原来的前提重新变真。