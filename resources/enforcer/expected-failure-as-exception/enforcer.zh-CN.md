# expected-failure-as-exception — Enforcer 中文版

## 定义
Foreseeable business refusal 被 exception 表达时，函数签名在撒谎：它看起来“成功返回 T”，但产品本来就承认 `Unauthorized/Conflict/InsufficientBalance/InvalidTransition` 等合法答案。

这些不是程序意外坏掉，而是业务世界允许操作回答“不”。Caller 有合理响应，就应该被类型迫使面对这条分支。

## 何时触发
- `withdraw()` 用 `InsufficientFundsException` 表达余额不足；
- not-authorized / conflict / invalid transition 全丢进 generic exception hierarchy；
- outer layer 才靠 catch type 猜 domain cases；
- 新增 business refusal 不会让 compile/test 提醒 callers 更新 match。

## 不要误判
- disk full、corrupt invariant、programmer error 等让 ordinary domain reasoning 无法继续；
- foreign library throw，在 owned adapter 立刻转成 typed domain result；
- ordinary loop/not-found plumbing 若不属于业务拒绝，更接近 `exception-driven-control-flow`。

## 刀口
产品在执行前能给这个 outcome 起一个稳定业务名字吗？Caller 收到后能做正常业务响应吗？都能，就属于 contract，不属于 exception side channel。

## 提醒
Foreseeable refusal 不是失败设计的羞耻角落；它是 API 的正常答案之一。类型应该把业务世界说完整。
