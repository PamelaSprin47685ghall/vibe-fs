# timeout-inflated-to-pass — Lesson

## 评价

### 定义

当更大的等待 budget 被当作“修复 progress 为什么迟到、缺失或不稳定”的办法时，就是 timeout inflation。

病灶不是“有人把 2s 改成 30s”。Timeout 合法地会变化。真正的问题是 causal substitution：**因为 mechanism 没弄明白，所以去改 clock。**

### 支配原则

Timeout 不会让工作前进。它只决定 caller 愿意忍受 uncertainty 多久，才承认“progress 尚未被建立”。

真正 completion condition 可能是 message、process exit、persisted record、lock release、readiness event、remote response 或 state transition。多等一会儿，只会给这些 condition 更多机会发生；它不会创造缺失的 causal link。

所以 timeout inflation 特别会制造“看起来像修好了”的效果：test 绿了，race、deadlock、resource leak、missing signal、pathological tail 或 unbounded algorithm 却一个没动。Defect 只是变慢、变难复现，而这种“更难撞见”经常被误叫成“更稳定”。

### 何时触发

当 failure/flakiness 主要通过增加 timeout/deadline 被变成 acceptable，而没有 evidence 证明旧 budget 与 healthy latency 本来就不匹配时触发，例如：

- integration test timeout，于是把 limit 倍增到 CI 大多数时候会过；
- process wait 没有可靠 completion signal，于是 timeout 实际承担了 synchronization；
- async race 用“给它更多时间”掩盖，而不是修 ordering/ownership；
- deadlock/resource leak 从快速失败变成长时间 hang；
- CI 单独拿到巨大 timeout，只因为“CI 慢”，没人测到底慢在哪里；
- agent 每失败一次就建议更大 deadline，却没有增加任何 causal observation；
- timeout 的依据就是“第一个能让它绿的数字”。

### 不应触发

- Measurement 证明 healthy p95/p99/tail latency 合法超过旧 bound，新 timeout 对应明确 SLO / resource policy。
- 产品/SLO 决策明确改变“对仍在健康 progress 的 operation 愿意等多久”。
- Timeout 是 negative test 的一部分，用于证明 absence of progress 会被 bounded failure 捕获。
- Remote retry/deadline 根据已知 tail behavior 设计，并且 operation 有可证明 causal progress。
- Test 现在故意执行更多合法工作，因此 test-specific timeout 随 workload 一起显式上调。

### 与相邻规则区分

`sleep-based-synchronization` 是直接把 elapsed time 当 readiness signal。`timeout-inflated-to-pass` 是把 failure threshold 往外推，让 unexplained wait 更不容易暴露。

`repeat-until-pass` 买更多 attempts；本规则给每个 attempt 买更多时间。真正 cause 找到后，可能归 `resource-not-scoped`、cancellation、deadlock 或 concurrency 等更具体规则。

### 判定程序

先命名“什么 event 应该让 operation complete”。

然后问：

1. Wait 是否真的与那个 event 有 causal connection，还是只在祈祷时间足够长？
2. Failing run 的时间到底花在哪里？
3. Operation 在 healthy progress、blocked、starved、leaked，还是等一个根本没来的 signal？
4. 新 budget 的证据是什么——除了“它现在会过”？

如果更大 timeout 唯一的证明就是 green 变得更常见，本规则成立。

### 例子

- positive：integration test 2s 超时，改 30s；没人发现 event subscription 有时在 event 发出之后才注册。
- positive：CI 偶尔卡在 child process，于是 job timeout 从 1 分钟改 10 分钟，而 leaked child 仍可能存在。
- positive：browser test 用 `waitForTimeout(5000)`，五秒有时不够，于是继续抬 suite timeout。
- near-miss：telemetry 证明 remote p99 为 1.8s，而旧 policy 只有 500ms；SLO 正式修订，timeout 有证据地改为 2.5s。
- counterexample：readiness event 被修复，使 waiter 因 causal event 醒来；旧 timeout 继续只承担 bounded failure policy。

## 建议

### 现在该做什么

把 clock 放回它真正的位置。

找出本应让 operation complete 的 causal event，并确定这个 event 为什么迟到或根本没来。必要时先 instrument path。先修 stalled mechanism，再根据 measured healthy behavior 与“愿意容忍 uncertainty 多久”的 policy 选择 timeout。

### 为什么重要

Timeout inflation 很诱人，因为它几乎不需要理解，就能立刻制造 cosmetic relief。

而这份 relief 经常是负进展：2 秒 race 变 30 秒 race；leaked child 变成 10 分钟 CI hang；missing readiness signal 变成“infrastructure 比较慢”。系统没有更可靠，只是 observation window 被放宽到不再那么容易把 defect 暴露出来。

Timeout 的真正价值恰恰是给 uncertainty 画边界。把它当 repair，就是擦掉那个原本在告诉你“这里不对劲”的边界。

### 修复策略

不要猜数字，建立 causal timeline：

- 定义 completion event；
- 记录 operation start；
- 记录 meaningful milestones 与 resource acquisition；
- 记录 completion event 是否 emit、persist、observe，或在中间丢失；
- 记录 cancellation / cleanup；
- 区分真正 CPU/work latency 与 blocked waiting；
- 对比 healthy 与 failing trace。

在 progress 真正停止的地方修 ownership、ordering、signaling、cleanup、resource contention 或 algorithmic cost。

Cause 弄清楚以后，再从显式 policy 选择 timeout：measured tail latency + 有理由的 margin、SLO、deadline budget，或 bounded test expectation。Timeout 应该表达“healthy uncertainty 能忍多久”，而不是 trial-and-error 找到第一个让 CI green 的整数。

### 决策分支

- **没有 causal progress：**不要加 timeout，修 missing signal、deadlock、leak、starvation 或 unbounded work。
- **有 healthy progress，但旧 budget 与 measurement 冲突：**有证据地修订 timeout，并把 operational reason 固化。
- **CI 因已知 resource constraint 更慢：**测出真正 bottleneck；要么 provision capacity，要么对真实 contending heavy work 合法 serialization，要么设置基于该 measured constraint 的 environment-specific budget。
- **Test 用 sleep 表示 readiness：**改为 causal wait；见 `sleep-based-synchronization`。
- **Operation 有真实 external tail：**明确建模 deadline/retry policy，并保留 final failure。

### 常见假修复

- 每次 red 就把 timeout 再翻倍，直到 failure frequency 在心理上可以接受。
- 直接禁用 timeout。Infinite uncertainty 不是 reliability。
- 在更大 timeout 上再叠 retries，让每次 failure 花更多时间才说同一件事。
- 只调高 CI limit，然后用“CI 本来就慢”结束讨论，却没有定位哪个 resource/stage 在慢。
- 加 progress logging，却不修 missing causal signal。Observability 能帮助 diagnosis，但不会创造 completion。
- 用巨大 timeout “更保险”。巨大 budget 很擅长隐藏 orphaned work，也会让 incident response 更糟。

### 验证

分别证明两个事实：

1. **Causality：**healthy completion 来自 intended event/condition，而不是“终于等够久”。
2. **Policy：**timeout 足够覆盖 measured healthy behavior，同时仍能有意地 bounded actual failure。

Fault injection 时，operation 仍应在选定 bound 内 timeout。Healthy run 则因为 causal condition 发生而完成，不是因为 clock 宽容。

### 完成条件

你可以解释 timeout 为什么是这个值，而答案里不出现“因为这样能过”。

Mechanism 负责 progress。Clock 只负责你愿意等多久，才承认 progress 没有被建立。

### Nudge

更大的钟，不会修复缺失的原因。

先解释 progress 为什么迟到，再决定 uncertainty 值得活多久。
