# weak-boundary-parsing — Enforcer 中文版

## 定义
Boundary parsing 弱，不是因为 external data “不够 typed”，而是 raw/loose representation 在最有证据的 ingress 没被一次性解释，反而继续向内传播，让每层在 provenance 更少的地方重复猜 shape、version 与 validity。

Ingress 是信任最低、上下文最全的时刻：raw payload、protocol、schema version、validation errors 都还在。此时不完成解释，之后只会更难。

## 何时触发
- raw JSON/dict/string bag 穿过 controller 进入 service/domain；
- 多层重复 `hasField/isString/if key in body`；
- validate 过后仍把原 map 传下去；
- cross-language payload 靠 downstream 逐字段试探；
- malformed input 在深层 policy 才爆炸。

## 不要误判
- adapter 保留 raw bytes 做 signature/checksum，但离开 boundary 前仍构造 strong type；
- protocol owner 本身就位于更低层，raw bytes 到它为止；
- test 用 raw fixtures 驱动 ingress 很正常；
- decoded 后仍泄漏 `any/unchecked` 更具体属于 `type-erosion-at-boundary`。

## 刀口
找到第一条 trusted boundary。**为什么内部第二层还需要重新问“这个字段存在吗/是什么类型”？** 若边界本可知道答案，uncertainty 被错误地向内借出去了。

## 提醒
Parse 不是把 JSON 变成 object；是把“外部可能是什么”转换成“内部现在有权相信什么”。
