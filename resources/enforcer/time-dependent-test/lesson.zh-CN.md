# time-dependent-test — Lesson

## 评价

### 定义

Time-dependent test 的问题，是 host clock 给 verdict 偷塞了一个没声明的 premise。

来源可能是 `Date.now()`、`UtcNow`、local timezone、DST、elapsed wall time、scheduler delay、current date、real deadline。共同点是：scenario 本来应该固定，但 test 没有自己选择 time，而是去机器上“发现现在几点”，于是 input 一直在移动。

因此失败不只是泛泛 "timing flaky"，可能具体依赖：

- test 执行中跨 midnight；
- month/year boundary；
- daylight-saving transition；
- CI zone/locale 与本地不同；
- leap day/calendar edge；
- scheduler pause 把 operation 推过 deadline；
- slow machine 改变 "within N ms" 是否通过；
- suite order 改变 assertion 前已过去多少 real time。

### 支配原则

正确修法是把 temporal fact 当普通 data：fixed instant、explicit zone、duration、deadline、monotonic tick、manual clock。Fixture 自己拥有它们。

Real time 可以留在很薄的 adapter boundary；需要时单独用 tolerant smoke 验证 adapter，再把 deterministic value 传进 domain。

也不要把一个 ambient source 换成另一个 global monkeypatch 就算解决。Process-wide fake clock 如果 test 无 scoped restore，反而会制造 order dependence。

### 何时触发

当 functional/domain verdict 依赖 ambient clock / wall-time window，而 time 本身又不是被测 feature 时，触发。

### 不应触发

不要误杀真正 clock-adapter smoke，它可以很窄地证明 production 能读 system clock。Performance/load benchmark 本来就测 wall time，也不是本规则。Causal synchronization 也不同：await 真实 completion signal，real timeout 只负责防 hang，不代表 success，就可以有 deterministic meaning。

### 与相邻规则区分

与 `time-source-in-logic` 区分：那条打 production policy 深处读取 ambient time；本规则打**test premises**。两者常指向同一 seam，但可以独立存在。

### 判定程序

决定性问题：

> 这条 test 如果一小时后开始、换一个 timezone、换一台慢机器，是否仍表示完全相同的 scenario？

如果不是，time 就是 undeclared input。

### 例子

test 执行中跨 midnight。month/year boundary。daylight-saving transition。CI zone/locale 与本地不同。leap day/calendar edge。scheduler pause 把 operation 推过 deadline。

## 建议

### 现在该做什么

把 time 变成显式 test input。

先列出 scenario 真正需要的 temporal fact：instant、date、zone、duration、deadline、monotonic elapsed value。然后直接传入，或通过 fixture 自己拥有的 scoped/manual clock 提供。

Expiration/deadline logic 更适合这样测：

```text
clock = ManualClock(t0)
run scenario
clock.advance(delta)
assert result
```

而不是 sleep 到 real time "应该已经越过 threshold"。

### 为什么重要

Calendar logic 要明确 zone 与 boundary instant：DST start/end、month boundary、leap day、midnight。Test 应自己说明“我在审哪条时间法则”，而不是等 CI 某天碰巧跑到那个时刻。

Timeout/cancellation 要拆成两件事：

- domain/policy deadline → controlled clock / explicit deadline；
- test runner 为防 hang 设置的 safety timeout → 可以继续用 real time，但只能负责失败上限，不能定义功能 success。

### 修复策略

如果 production policy 深处仍直接读 ambient `now()`，测试很难 deterministic 往往是在替 `time-source-in-logic` 报警。把 production seam 修好，通常同时改善设计与 testability。

### 决策分支

然后只改变**受控时间输入**：expiry 前一刻、恰好到点、后一刻，DST crossing，calendar boundary。只有 domain law 说 outcome 应变时，test 才应改变。

需要的话保留一个很窄的 real-clock integration smoke，只证明 "adapter 能读到合理 system time"。不要让它承担 "billing/expiry domain 正确" 这种更强 claim。

### 常见假修复

常见假修复：

- `within 100ms` 放宽成 `within 5s`；
- sleep 更久，保证 deadline "肯定过去了"；
- CI 全局设 timezone，就假设所有本地环境一致；
- 全局 monkeypatch `Date.now`，却没有 scoped restore，转头制造 cross-test leakage；
- 某一层 freeze time，另一 dependency 仍偷偷读 real clock；
- 断言 formatted date string，却没固定 locale/zone；
- 把 wall time 换成 scheduler order，仍然暗中依赖 elapsed duration。

### 验证

验证要主动移动真实环境。在不同 host timezone、不同 wall-clock 起点、不同 scheduler speed 下运行，functional verdict 必须不变，因为 semantic temporal facts 全由 fixture 提供。

### 完成条件

完成时，同一个 scenario 不管 CI 何时何地执行都保持同一含义；temporal behavior 只在 test 显式改变 temporal data 时变化。

> Deterministic test 不是把 time 从世界删掉，而是把 time 从天气变成故事里的数据。

### Nudge

> Test 应该自己选择故事发生的时间，而不是问机器“我读这个故事时碰巧几点”。
