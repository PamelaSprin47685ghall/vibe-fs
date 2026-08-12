# contract-test-missing — Main

在独立两侧真正相遇的**最窄边界**补 contract test。

先把 agreement 说清：

- producer；
- consumer；
- crossing representation；
- stable identity rule；
- supported version/alternative；
- ordering/lifetime guarantee；
- failure/unknown semantics；
- 如果 boundary 带 capability/permission，也包括它们。

然后尽可能让 test 同时使用两边真实 boundary machinery。Real serializer → real parser，比两份根据同一英文 spec 手写的 fixture 更强；generated Fable export → JS facade，比只断言 F# source 里“有这个 type name”更强。

Test 应保持窄，不必为了 contract 自动上整套 production deployment；但绝不能 mock 掉自己正要证明的那层 transformation。

常见假修复：

- producer encoder 与 consumer decoder 各自 unit-test，但 fixture 各编各的；
- 把 captured payload 全量 snapshot，却从未声明哪些 field 才 contractual；
- 只断言 “serialization succeeds” / “parser returns something”；
- mock external side 时复用 production 同一个错误 schema object；
- protocol 不关心 key order/whitespace，却把这些 incidental bytes 锁死；
- 只测 happy representation，不测 error/failure；
- compatibility 声称支持 old version，test 却只跑 newest；
- 上一个巨大 E2E，失败后分不清 contract drift 还是其他 infrastructure。

一个好的 contract test 通常同时有正反 evidence：

```text
supported producer output → consumer 接受且保留 semantics
plausible incompatible output → consumer reject / 按 contract 映射
```

Versioned protocol 要证明真实 support matrix；identity-bearing protocol 要证明 ID/cursor/idempotency key round trip 不被 regenerate；capability surface 要证明 advertisement 与 execution gate 一致。

验证时做 mutation：任一侧故意引入一个 plausible incompatible change——rename field、change tag、default/error mapping、causally significant order、regenerate identity——在另一侧尚未同步时，contract test 必须立即红。

如果 boundary 依赖 real external service 的 undocumented behavior，本地 double 不可能诚实复现，就把职责拆开：你自己拥有的部分用 narrow local contract test；external runtime 那部分用真实 canary。不要把本地 mock 伪装成别家公司 runtime 的 oracle。

完成时任一侧可以自由 internal evolve，但只要它 drift 出对方真正消费的 agreement，就会出现立即、可定位的 failure。

> Contract test 是 independent truths 之间的 executable diplomacy。