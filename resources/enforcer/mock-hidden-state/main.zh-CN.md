# mock-hidden-state — Main 中文版

## 现在该做什么
删除 invisible cursor/call-count logic。Stateless contract 用纯 `request -> response`；stateful contract 把真实 protocol state 显式建模，并用 stable identity/request 驱动 response。

## 为什么这很重要
Hidden-state mock 很容易“准确复现 test 顺序”，却完全不复现 provider semantics。Production 改成等价但不同 call ordering 后，tests 会红；真正 request 错了但调用顺序没变时，tests 又可能绿。

## 常见假修复
- 每个 test 前更认真 reset cursor。
- 写更多 sequential canned responses。
- 加 call-order assertions 来保护 fixture 自己的秘密状态。
- 把 cursor 包进一个 class，仍不属于协议。

## 验证
交换独立 calls、重复相同 request。只有 visible request 或 explicit protocol state 改变时，mock answer 才能改变。

## 完成条件
Test double 是外部 contract 的小型透明模型，而不是由测试调用顺序驱动的秘密 state machine。
