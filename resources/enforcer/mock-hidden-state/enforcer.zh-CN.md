# mock-hidden-state — Enforcer 中文版

## 定义

Mock hidden state 发生在 test double 的答案依赖 invisible cursor、call count、clock、closure flag 或 suite history，而这些状态并不属于真实 provider-visible contract。

于是相同 request 在相同显式状态下可以得到不同 response，只因为 fixture “记得这是第几次调用”。Test 验证的变成了自己编排的 choreography，而不是外部协议。

## 支配原则

Test double 应该让 contract 更容易推理，而不是在它背后发明一个看不见的宇宙。隐藏的可变 cursor 制造生产 caller 无法观察的因果：相同 request 因为 mock 记得协议没有表达的某物而返回不同答案。Tests 于是验证的是与 fixture 的 choreography，而不是真实交互的语义。

## 何时触发

- mock 依次吐 `responses.shift()`，不看 request；
- 第一次 call success、第二次 fail，只靠 counter；
- closure 中藏 phase，test body 看不到；
- reorder 两个独立 calls 会改变 responses；
- reset cursor 成为 suite 正确性的必要仪式。

## 不应触发

- 真实 protocol 本来 stateful，fake 显式建模 session/server state；
- cassette keyed by visible request；
- variation 来自 test 明确输入；
- fake state 由 test 构造并作为显式 model 传入，不藏在 mock closure。

## 与相邻规则区分

`order-dependent-test` 管 tests 互相泄漏状态；`test-implementation-coupled` 管内部调用期望。Tie-break：如果 mock 的 response function 隐藏了 protocol 未暴露的状态，用本规则；如果 tests 互相泄漏状态，用 `order-dependent-test`；如果 test 断言的是调用 choreography 而非 contract，用 `test-implementation-coupled`。

## 判定程序

给 mock 两次相同 visible request + 相同 explicit protocol state。若可能返回不同值，问那个差异来自哪里。若答案只有“第几次调用”，fixture 在创造生产 contract 没有的因果。

## 例子

- positive：mock 每次调用返回下一个 canned payload，不管 request body。
- near-miss：fake session object 把 `open`/`balance` 暴露为 protocol state，并从那个 model 回答。
- counterexample：纯 `request → response` map 对相同 visible request 返回相同值。

## Nudge

Mock 应缩小现实，而不是创造秘密现实。它的答案必须能由 caller 可见的协议事实解释。