# mock-hidden-state — Enforcer 中文版

## 定义
Mock hidden state 发生在 test double 的答案依赖 invisible cursor、call count、clock、closure flag 或 suite history，而这些状态并不属于真实 provider-visible contract。

于是相同 request 在相同显式状态下可以得到不同 response，只因为 fixture “记得这是第几次调用”。Test 验证的变成了自己编排的 choreography，而不是外部协议。

## 何时触发
- mock 依次吐 `responses.shift()`，不看 request；
- 第一次 call success、第二次 fail，只靠 counter；
- closure 中藏 phase，test body 看不到；
- reorder 两个独立 calls 会改变 responses；
- reset cursor 成为 suite 正确性的必要仪式。

## 不要误判
- 真实 protocol 本来 stateful，fake 显式建模 session/server state；
- cassette keyed by visible request；
- variation 来自 test 明确输入；
- fake state 由 test 构造并作为显式 model 传入，不藏在 mock closure。

## 刀口
给 mock 两次相同 visible request + 相同 explicit protocol state。若可能返回不同值，问那个差异来自哪里。若答案只有“第几次调用”，fixture 在创造生产 contract 没有的因果。

## 提醒
Mock 应缩小现实，而不是创造秘密现实。它的答案必须能由 caller 可见的协议事实解释。
