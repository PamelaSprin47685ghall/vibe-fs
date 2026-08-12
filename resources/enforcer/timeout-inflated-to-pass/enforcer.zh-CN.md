# timeout-inflated-to-pass — Enforcer

## 定义
当更大的等待 budget 被当作“修复 progress 为什么迟到、缺失或不稳定”的办法时，就是 timeout inflation。

病灶不是“有人把 2s 改成 30s”。Timeout 合法地会变化。真正的问题是 causal substitution：**因为 mechanism 没弄明白，所以去改 clock。**

## 支配原则
Timeout 不会让工作前进。它只决定 caller 愿意忍受 uncertainty 多久，才承认“progress 尚未被建立”。

真正 completion condition 可能是 message、process exit、persisted record、lock release、readiness event、remote response 或 state transition。多等一会儿，只会给这些 condition 更多机会发生；它不会创造缺失的 causal link。

所以 timeout inflation 特别会制造“看起来像修好了”的效果：test 绿了，race、deadlock、resource leak、missing signal、pathological tail 或 unbounded algorithm 却一个没动。Defect 只是变慢、变难复现，而这种“更难撞见”经常被误叫成“更稳定”。

## 何时触发
当 failure/flakiness 主要通过增加 timeout/deadline 被变成 acceptable，而没有 evidence 证明旧 budget 与 healthy latency 本来就不匹配时触发，例如：

- integration test timeout，于是把 limit 倍增到 CI 大多数时候会过；
- process wait 没有可靠 completion signal，于是 timeout 实际承担了 synchronization；
- async race 用“给它更多时间”掩盖，而不是修 ordering/ownership；
- deadlock/resource leak 从快速失败变成长时间 hang；
- CI 单独拿到巨大 timeout，只因为“CI 慢”，没人测到底慢在哪里；
- agent 每失败一次就建议更大 deadline，却没有增加任何 causal observation；
- timeout 的依据就是“第一个能让它绿的数字”。

## 不应触发
- Measurement 证明 healthy p95/p99/tail latency 合法超过旧 bound，新 timeout 对应明确 SLO / resource policy。
- 产品/SLO 决策明确改变“对仍在健康 progress 的 operation 愿意等多久”。
- Timeout 是 negative test 的一部分，用于证明 absence of progress 会被 bounded failure 捕获。
- Remote retry/deadline 根据已知 tail behavior 设计，并且 operation 有可证明 causal progress。
- Test 现在故意执行更多合法工作，因此 test-specific timeout 随 workload 一起显式上调。

## 与相邻规则区分
`sleep-based-synchronization` 是直接把 elapsed time 当 readiness signal。`timeout-inflated-to-pass` 是把 failure threshold 往外推，让 unexplained wait 更不容易暴露。

`repeat-until-pass` 买更多 attempts；本规则给每个 attempt 买更多时间。真正 cause 找到后，可能归 `resource-not-scoped`、cancellation、deadlock 或 concurrency 等更具体规则。

## 判定程序
先命名“什么 event 应该让 operation complete”。

然后问：

1. Wait 是否真的与那个 event 有 causal connection，还是只在祈祷时间足够长？
2. Failing run 的时间到底花在哪里？
3. Operation 在 healthy progress、blocked、starved、leaked，还是等一个根本没来的 signal？
4. 新 budget 的证据是什么——除了“它现在会过”？

如果更大 timeout 唯一的证明就是 green 变得更常见，本规则成立。

## 例子
- positive：integration test 2s 超时，改 30s；没人发现 event subscription 有时在 event 发出之后才注册。
- positive：CI 偶尔卡在 child process，于是 job timeout 从 1 分钟改 10 分钟，而 leaked child 仍可能存在。
- positive：browser test 用 `waitForTimeout(5000)`，五秒有时不够，于是继续抬 suite timeout。
- near-miss：telemetry 证明 remote p99 为 1.8s，而旧 policy 只有 500ms；SLO 正式修订，timeout 有证据地改为 2.5s。
- counterexample：readiness event 被修复，使 waiter 因 causal event 醒来；旧 timeout 继续只承担 bounded failure policy。

## Nudge
更大的钟，不会修复缺失的原因。

先解释 progress 为什么迟到，再决定 uncertainty 值得活多久。
