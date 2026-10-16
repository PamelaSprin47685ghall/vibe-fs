# contract-test-missing — Lesson

## 评价

### 定义

Contract test 缺失，不是因为“integration test 数量不够”，而是两个独立实现都能在自己的世界里完全正确，却在真正相遇的地方彼此不兼容。

最核心的问题：

```text
producer 认为自己发出了 X
consumer 认为自己接受 Y
X ≠ Y
```

两边 unit test 都可以全绿，因为它们各自在证明自己的假设。真正缺的是**交集 proof**：跨边界真实发生的 bytes、framing、identity、ordering、default、lifecycle、failure semantics、versioning、capability rule。

### 支配原则

一个重要 trigger 是“独立性”。Producer/consumer 能分别改、用不同 language/runtime、由不同 release cycle 管理时，两边把不同东西都当“显然”的概率会大幅上升。

### 何时触发

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

### 不应触发

不要每次 internal refactor 都机械加 contract test。如果 observable agreement 没变，而且现有 boundary test 已能在 incompatibility 时失败，就不要制造 theater。

也不要冻结所有 incidental byte。Exact wire detail 只有本身是 contract 时才值得锁定；否则应断言 semantic property：required field、stable identity、allowed alternative、ordering guarantee、failure category、idempotency identity、capability projection。把 private serialization accident 全冻结，只是 system-scale 的 `test-implementation-coupled`。

### 与相邻规则区分

与 `behavioral-boundary-untested` 区分：那条在同一产品内部 supported public entrance 也可触发；本规则专门管独立两侧会 drift 的 agreement。`canary-skipped` 则是在 external side 无法被 local double 诚实复现时，需要真实环境证明。

### 判定程序

最决定性的 test-design 问题：

> 最小哪一次 execution，能让**两边真实 assumptions 同时存在**？

能用 real encoder/parser/adapter 就用。不要手写一个 fixture，只因它与 production 同样读了那份 spec；两者可能共享同一个误解。

### 例子

一个强 contract test 应能抓住现实 incompatibility：

- field rename/missing；
- union tag 变化；
- default 漂移；
- status/error mapping 错；
- identity 被 regenerate；
- ordering 改；
- version 混用；
- capability 多暴露；
- ack semantics 变化。

## 建议

### 现在该做什么

在独立两侧真正相遇的**最窄边界**补 contract test。

### 为什么重要

先把 agreement 说清：

- producer；
- consumer；
- crossing representation；
- stable identity rule；
- supported version/alternative；
- ordering/lifetime guarantee；
- failure/unknown semantics；
- 如果 boundary 带 capability/permission，也包括它们。

### 修复策略

然后尽可能让 test 同时使用两边真实 boundary machinery。Real serializer → real parser，比两份根据同一英文 spec 手写的 fixture 更强；generated Fable export → JS facade，比只断言 F# source 里“有这个 type name”更强。

Test 应保持窄，不必为了 contract 自动上整套 production deployment；但绝不能 mock 掉自己正要证明的那层 transformation。

一个好的 contract test 通常同时有正反 evidence：

```text
supported producer output → consumer 接受且保留 semantics
plausible incompatible output → consumer reject / 按 contract 映射
```

Versioned protocol 要证明真实 support matrix；identity-bearing protocol 要证明 ID/cursor/idempotency key round trip 不被 regenerate；capability surface 要证明 advertisement 与 execution gate 一致。

### 决策分支

如果 boundary 依赖 real external service 的 undocumented behavior，本地 double 不可能诚实复现，就把职责拆开：你自己拥有的部分用 narrow local contract test；external runtime 那部分用真实 canary。不要把本地 mock 伪装成别家公司 runtime 的 oracle。

### 常见假修复

常见假修复：

- producer encoder 与 consumer decoder 各自 unit-test，但 fixture 各编各的；
- 把 captured payload 全量 snapshot，却从未声明哪些 field 才 contractual；
- 只断言 “serialization succeeds” / “parser returns something”；
- mock external side 时复用 production 同一个错误 schema object；
- protocol 不关心 key order/whitespace，却把这些 incidental bytes 锁死；
- 只测 happy representation，不测 error/failure；
- compatibility 声称支持 old version，test 却只跑 newest；
- 上一个巨大 E2E，失败后分不清 contract drift 还是其他 infrastructure。

### 验证

验证时做 mutation：任一侧故意引入一个 plausible incompatible change——rename field、change tag、default/error mapping、causally significant order、regenerate identity——在另一侧尚未同步时，contract test 必须立即红。

### 完成条件

完成时任一侧可以自由 internal evolve，但只要它 drift 出对方真正消费的 agreement，就会出现立即、可定位的 failure。

> Contract test 是 independent truths 之间的 executable diplomacy。

### Nudge

> 两个正确 component 仍然可能彼此不兼容。测试 agreement，而不是测试两边各自有多自信。
