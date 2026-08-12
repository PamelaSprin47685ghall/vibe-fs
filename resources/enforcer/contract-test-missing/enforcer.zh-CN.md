# contract-test-missing — Enforcer

Contract test 缺失，不是因为“integration test 数量不够”，而是两个独立实现都能在自己的世界里完全正确，却在真正相遇的地方彼此不兼容。

最核心的问题：

```text
producer 认为自己发出了 X
consumer 认为自己接受 Y
X ≠ Y
```

两边 unit test 都可以全绿，因为它们各自在证明自己的假设。真正缺的是**交集 proof**：跨边界真实发生的 bytes、framing、identity、ordering、default、lifecycle、failure semantics、versioning、capability rule。

以下边界改动应特别警惕：

- plugin ↔ Host hook object；
- F# ↔ generated JS/Fable representation；
- process ↔ stdout/stdin framing；
- client ↔ provider HTTP/tool schema；
- application ↔ database/store transaction semantics；
- service ↔ queue/message contract；
- package ↔ consumer import/export surface；
- CLI ↔ subprocess exit/status/output protocol；
- network protocol ↔ adapter decoder/encoder。

一个重要 trigger 是“独立性”。Producer/consumer 能分别改、用不同 language/runtime、由不同 release cycle 管理时，两边把不同东西都当“显然”的概率会大幅上升。

不要每次 internal refactor 都机械加 contract test。如果 observable agreement 没变，而且现有 boundary test 已能在 incompatibility 时失败，就不要制造 theater。

也不要冻结所有 incidental byte。Exact wire detail 只有本身是 contract 时才值得锁定；否则应断言 semantic property：required field、stable identity、allowed alternative、ordering guarantee、failure category、idempotency identity、capability projection。把 private serialization accident 全冻结，只是 system-scale 的 `test-implementation-coupled`。

与 `behavioral-boundary-untested` 区分：那条在同一产品内部 supported public entrance 也可触发；本规则专门管独立两侧会 drift 的 agreement。`canary-skipped` 则是在 external side 无法被 local double 诚实复现时，需要真实环境证明。

最决定性的 test-design 问题：

> 最小哪一次 execution，能让**两边真实 assumptions 同时存在**？

能用 real encoder/parser/adapter 就用。不要手写一个 fixture，只因它与 production 同样读了那份 spec；两者可能共享同一个误解。

一个强 contract test 应能抓住现实 incompatibility：field rename/missing、union tag 变化、default 漂移、status/error mapping 错、identity 被 regenerate、ordering 改、version 混用、capability 多暴露、ack semantics 变化。

> 两个正确 component 仍然可能彼此不兼容。测试 agreement，而不是测试两边各自有多自信。