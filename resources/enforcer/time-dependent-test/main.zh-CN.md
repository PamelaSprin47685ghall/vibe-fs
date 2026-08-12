# time-dependent-test — Main

把 time 变成显式 test input。

先列出 scenario 真正需要的 temporal fact：instant、date、zone、duration、deadline、monotonic elapsed value。然后直接传入，或通过 fixture 自己拥有的 scoped/manual clock 提供。

Expiration/deadline logic 更适合这样测：

```text
clock = ManualClock(t0)
run scenario
clock.advance(delta)
assert result
```

而不是 sleep 到 real time “应该已经越过 threshold”。

Calendar logic 要明确 zone 与 boundary instant：DST start/end、month boundary、leap day、midnight。Test 应自己说明“我在审哪条时间法则”，而不是等 CI 某天碰巧跑到那个时刻。

Timeout/cancellation 要拆成两件事：

- domain/policy deadline → controlled clock / explicit deadline；
- test runner 为防 hang 设置的 safety timeout → 可以继续用 real time，但只能负责失败上限，不能定义功能 success。

常见假修复：

- `within 100ms` 放宽成 `within 5s`；
- sleep 更久，保证 deadline “肯定过去了”；
- CI 全局设 timezone，就假设所有本地环境一致；
- 全局 monkeypatch `Date.now`，却没有 scoped restore，转头制造 cross-test leakage；
- 某一层 freeze time，另一 dependency 仍偷偷读 real clock；
- 断言 formatted date string，却没固定 locale/zone；
- 把 wall time 换成 scheduler order，仍然暗中依赖 elapsed duration。

验证要主动移动真实环境。在不同 host timezone、不同 wall-clock 起点、不同 scheduler speed 下运行，functional verdict 必须不变，因为 semantic temporal facts 全由 fixture 提供。

然后只改变**受控时间输入**：expiry 前一刻、恰好到点、后一刻，DST crossing，calendar boundary。只有 domain law 说 outcome 应变时，test 才应改变。

如果 production policy 深处仍直接读 ambient `now()`，测试很难 deterministic 往往是在替 `time-source-in-logic` 报警。把 production seam 修好，通常同时改善设计与 testability。

需要的话保留一个很窄的 real-clock integration smoke，只证明 “adapter 能读到合理 system time”。不要让它承担 “billing/expiry domain 正确” 这种更强 claim。

完成时，同一个 scenario 不管 CI 何时何地执行都保持同一含义；temporal behavior 只在 test 显式改变 temporal data 时变化。

> Deterministic test 不是把 time 从世界删掉，而是把 time 从天气变成故事里的数据。