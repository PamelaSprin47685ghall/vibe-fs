# behavioral-boundary-untested — Main

在真正拥有 promise 的 supported entrance 上补一个 test。

从 caller 的句子开始，不要从“我想 cover 哪个 helper”开始：

```text
Caller 通过 boundary B 提供 X，
应观察到 Y，且绝不能观察到 Z。
```

然后构造仍会经过相关 production decoding/wiring/permission/default logic 的最小 fixture，从 B 进入，只断言 caller-visible result、durable state 或 external effect，不断言 private helper choreography。

Lower-level test 不用删。它们在 boundary theorem 存在后仍然很有价值：pure law、edge space、failure localization 都适合 unit/property test。真正要停止的是拿 unit test 去替自己从未经过的 integration 作证。

常见假修复：

- 为了测试方便，把 private helper export 出来；
- fixture 自己复制 production wiring，结果 fixture 与 production 可以各错各的；
- 直接 construct post-decoder object，而 bug 本来就可能在 decoder/default；
- 把 permission/identity layer mock 掉，但它其实属于 public behavior；
- 明明一个窄 real-boundary test 足够，却上一个巨大 full-stack E2E；
- 只断言 “did not throw”，wrong result/default/identity 仍能 green。

验证要证明这条 test 真守住 boundary risk。保持 internal helper 全正确，只故意制造一个 plausible composition defect：swapped field、missing default、bypassed adapter、wrong dependency、wrong ID。Test 必须红。

再做一次 semantics-preserving internal refactor。Boundary test 应继续 green。如果 helper rename/call order 一变就炸，它已经滑向 `test-implementation-coupled`。

不要把这条规则扩大成“所有东西都 E2E”。一个 private arithmetic function 如果 public owner 已有 boundary proof，新 arithmetic case 完全可以只加 focused unit/property test。Evidence 应该放在 claim 所属的层。

完成时 caller-visible regression 不再能躲在 green helper suite 后面，而 internal decomposition 仍可自由演化。

> Supported entrance 是 implementation 变成 promise 的地方。至少放一个 witness 在那里。