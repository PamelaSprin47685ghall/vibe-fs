# behavioral-boundary-untested — Enforcer

Behavioral boundary untested，不是说“unit test 不够高级”，而是 suite 把 supported entry 背后的零件都证明了，却从来没证明**真实 caller 走进去时，承诺真的成立**。

Helper test 很容易让人产生安全感：快、精确、coverage 高、failure 好定位。但 public behavior 从来不只是 helper 之和。Boundary 还包含 wiring、default、identity、authorization、normalization、serialization、effect ordering、error mapping、dependency composition——恰恰是“每个零件都对，组起来却错”的地方。

可以把 internal test 看成 lemma，supported boundary 看成 theorem。十个 lemma 都正确，也不代表组合出来的 theorem 一定成立。

以下情形触发：

- test 直接 call private/internal helper，而真实 public method/route/tool/hook 从未走过；
- fixture 直接 mutate internal state 做 setup，绕过 production behavior；
- test-only export 绕开真实 adapter/decoder/permission gate/workflow owner；
- helper coverage 被拿来证明 public default/wiring；
- public identity/failure semantics 改了，测试仍停在下一层；
- integration wiring 全坏，unit suite 仍可全部 green。

不要误杀已经有强 boundary test 的模块。Helper test 可以继续负责 localization、pure law、edge-space。也不要每个小 helper 改动都要求巨大 full-stack E2E。真正标准只有一个：**caller-visible promise 在真正 owning entrance 上，是否至少有一个能把错误打红的 proof。**

与 `contract-test-missing` 区分：那条专门管 independent runtime/system 之间的 agreement；本规则在同一产品内部也成立，只要 caller 依赖一个 supported behavior surface。`test-implementation-coupled` 则是反方向——测试钻太深，冻结 private choreography。

决定性 mutation 很简单：保持所有 helper 都正确，只把 boundary wiring 改错——wrong default、swapped field、missing permission、wrong serializer、forgotten adapter、stale route、wrong ID。有没有 test 会红？没有，就说明 public theorem 未被证明。

好的 boundary test 不需要大。它应该是**仍然从真实 caller 入口进入的最窄测试**，并只观察 caller 有权依赖的结果。

> 在 promise 真正成立的地方证明 behavior，而不是只在 implementation 方便测试的地方证明零件。