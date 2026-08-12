# expected-failure-as-exception — Main 中文版

## 现在该做什么
把 foreseeable refusals 放进 closed result type，让 callers 显式 match；HTTP/UI/provider adapter 只负责把 typed case 翻译成外部表示。Unexpected infrastructure/programmer failures 继续保持独立 exception channel。

## 为什么这很重要
Exception API 让 caller 可以“忘记业务世界有拒绝”。Typed result 则把 refusal 变成 compile-time/test-time obligation，也避免各层重复 catch 同一 exception 再翻译成 strings。

## 常见假修复
- exception 改成 null/bool/error string。
- 建一个庞大 `AppException` hierarchy；仍然不是返回 contract。
- core 继续 throw，只在 HTTP controller 知道 business cases。
- 把 infrastructure failure 也全部塞进 domain union，抹掉不同 failure law。

## 验证
新增一个 business refusal case 时，相关 callers/tests 应被迫重新决策；adapter 映射不需要 parse exception prose。

## 完成条件
函数签名完整描述 success 与所有 foreseeable refusals；exception 只表示普通 domain contract 之外的破坏。
