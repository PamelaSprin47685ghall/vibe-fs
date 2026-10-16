# behavioral-boundary-untested — Lesson

## 评价

### 定义

Behavioral boundary untested，不是说“unit test 不够高级”，而是 suite 把 supported entry 背后的零件都证明了，却从来没证明**真实 caller 走进去时，承诺真的成立**。

### 支配原则

Helper test 很容易让人产生安全感：快、精确、coverage 高、failure 好定位。但 public behavior 从来不只是 helper 之和。Boundary 还包含 wiring、default、identity、authorization、normalization、serialization、effect ordering、error mapping、dependency composition——恰恰是“每个零件都对，组起来却错”的地方。

可以把 internal test 看成 lemma，supported boundary 看成 theorem。十个 lemma 都正确，也不代表组合出来的 theorem 一定成立。

### 何时触发

以下情形触发：

- test 直接 call private/internal helper，而真实 public method/route/tool/hook 从未走过；
- fixture 直接 mutate internal state 做 setup，绕过 production behavior；
- test-only export 绕开真实 adapter/decoder/permission gate/workflow owner；
- helper coverage 被拿来证明 public default/wiring；
- public identity/failure semantics 改了，测试仍停在下一层；
- integration wiring 全坏，unit suite 仍可全部 green。

### 不应触发

不要误杀已经有强 boundary test 的模块。Helper test 可以继续负责 localization、pure law、edge-space。也不要每个小 helper 改动都要求巨大 full-stack E2E。真正标准只有一个：**caller-visible promise 在真正 owning entrance 上，是否至少有一个能把错误打红的 proof。**

### 与相邻规则区分

与 `contract-test-missing` 区分：那条专门管 independent runtime/system 之间的 agreement；本规则在同一产品内部也成立，只要 caller 依赖一个 supported behavior surface。`test-implementation-coupled` 则是反方向——测试钻太深，冻结 private choreography。

### 判定程序

决定性 mutation 很简单：保持所有 helper 都正确，只把 boundary wiring 改错——wrong default、swapped field、missing permission、wrong serializer、forgotten adapter、stale route、wrong ID。有没有 test 会红？没有，就说明 public theorem 未被证明。

好的 boundary test 不需要大。它应该是**仍然从真实 caller 入口进入的最窄测试**，并只观察 caller 有权依赖的结果。

### 例子


## 建议

### 现在该做什么

在真正拥有 promise 的 supported entrance 上补一个 test。

### 为什么重要

Lower-level test 不用删。它们在 boundary theorem 存在后仍然很有价值：pure law、edge space、failure localization 都适合 unit/property test。真正要停止的是拿 unit test 去替自己从未经过的 integration 作证。

### 修复策略

从 caller 的句子开始，不要从“我想 cover 哪个 helper”开始：

```text
Caller 通过 boundary B 提供 X，
应观察到 Y，且绝不能观察到 Z。
```

然后构造仍会经过相关 production decoding/wiring/permission/default logic 的最小 fixture，从 B 进入，只断言 caller-visible result、durable state 或 external effect，不断言 private helper choreography。

### 决策分支

不要把这条规则扩大成“所有东西都 E2E”。一个 private arithmetic function 如果 public owner 已有 boundary proof，新 arithmetic case 完全可以只加 focused unit/property test。Evidence 应该放在 claim 所属的层。

### 常见假修复

常见假修复：

- 为了测试方便，把 private helper export 出来；
- fixture 自己复制 production wiring，结果 fixture 与 production 可以各错各的；
- 直接 construct post-decoder object，而 bug 本来就可能在 decoder/default；
- 把 permission/identity layer mock 掉，但它其实属于 public behavior；
- 明明一个窄 real-boundary test 足够，却上一个巨大 full-stack E2E；
- 只断言 “did not throw”，wrong result/default/identity 仍能 green。

### 验证

验证要证明这条 test 真守住 boundary risk。保持 internal helper 全正确，只故意制造一个 plausible composition defect：swapped field、missing default、bypassed adapter、wrong dependency、wrong ID。Test 必须红。

再做一次 semantics-preserving internal refactor。Boundary test 应继续 green。如果 helper rename/call order 一变就炸，它已经滑向 `test-implementation-coupled`。

### 完成条件

完成时 caller-visible regression 不再能躲在 green helper suite 后面，而 internal decomposition 仍可自由演化。

> Supported entrance 是 implementation 变成 promise 的地方。至少放一个 witness 在那里。

### Nudge

> 在 promise 真正成立的地方证明 behavior，而不是只在 implementation 方便测试的地方证明零件。
