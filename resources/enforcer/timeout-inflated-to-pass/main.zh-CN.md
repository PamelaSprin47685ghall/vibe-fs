# timeout-inflated-to-pass — Main

## 现在该做什么
把 clock 放回它真正的位置。

找出本应让 operation complete 的 causal event，并确定这个 event 为什么迟到或根本没来。必要时先 instrument path。先修 stalled mechanism，再根据 measured healthy behavior 与“愿意容忍 uncertainty 多久”的 policy 选择 timeout。

## 为什么重要
Timeout inflation 很诱人，因为它几乎不需要理解，就能立刻制造 cosmetic relief。

而这份 relief 经常是负进展：2 秒 race 变 30 秒 race；leaked child 变成 10 分钟 CI hang；missing readiness signal 变成“infrastructure 比较慢”。系统没有更可靠，只是 observation window 被放宽到不再那么容易把 defect 暴露出来。

Timeout 的真正价值恰恰是给 uncertainty 画边界。把它当 repair，就是擦掉那个原本在告诉你“这里不对劲”的边界。

## 修复策略
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

## 决策分支
- **没有 causal progress：**不要加 timeout，修 missing signal、deadlock、leak、starvation 或 unbounded work。
- **有 healthy progress，但旧 budget 与 measurement 冲突：**有证据地修订 timeout，并把 operational reason 固化。
- **CI 因已知 resource constraint 更慢：**测出真正 bottleneck；要么 provision capacity，要么对真实 contending heavy work 合法 serialization，要么设置基于该 measured constraint 的 environment-specific budget。
- **Test 用 sleep 表示 readiness：**改为 causal wait；见 `sleep-based-synchronization`。
- **Operation 有真实 external tail：**明确建模 deadline/retry policy，并保留 final failure。

## 常见假修复
- 每次 red 就把 timeout 再翻倍，直到 failure frequency 在心理上可以接受。
- 直接禁用 timeout。Infinite uncertainty 不是 reliability。
- 在更大 timeout 上再叠 retries，让每次 failure 花更多时间才说同一件事。
- 只调高 CI limit，然后用“CI 本来就慢”结束讨论，却没有定位哪个 resource/stage 在慢。
- 加 progress logging，却不修 missing causal signal。Observability 能帮助 diagnosis，但不会创造 completion。
- 用巨大 timeout “更保险”。巨大 budget 很擅长隐藏 orphaned work，也会让 incident response 更糟。

## 验证
分别证明两个事实：

1. **Causality：**healthy completion 来自 intended event/condition，而不是“终于等够久”。
2. **Policy：**timeout 足够覆盖 measured healthy behavior，同时仍能有意地 bounded actual failure。

Fault injection 时，operation 仍应在选定 bound 内 timeout。Healthy run 则因为 causal condition 发生而完成，不是因为 clock 宽容。

## 完成条件
你可以解释 timeout 为什么是这个值，而答案里不出现“因为这样能过”。

Mechanism 负责 progress。Clock 只负责你愿意等多久，才承认 progress 没有被建立。
