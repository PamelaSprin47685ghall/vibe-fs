# misleading-name — Enforcer 中文版

## 定义
Misleading name 是 identifier 向读者承诺了实现没有提供的 guarantee、owner、scope 或 domain meaning。名字不是装饰；读者靠它跳过重复阅读实现，所以错误名字会把错误 premise 缓存进每个 call site。

## 何时触发
- `commitDurable` 实际只写 memory；
- `atomicSave` 没有 atomicity；
- `uniqueId` 只“通常不重复”；
- `authorizedUser` 尚未授权；
- `final/complete/safe/verified` 比真实 evidence 更强；
- 名字暗示 global scope，实际只 session-local，或反之。

## 不要误判
- 泛化名字可能弱，但未必说谎；
- qualifier 已诚实缩小 guarantee，如 `InMemoryCache`；
- 单纯有多个 synonym 属 `domain-language-drift`；
- 缩写难解码属 `abbreviation-anxiety`。

## 刀口
写出一个合理读者仅凭名字会推断的最强 contract，再与实现逐项对比。名字比实现强一档，就是 semantic lie。

## 提醒
名字是给维护者使用的缓存。错误名字最危险的地方，是它让人**合理地不去检查实现**，然后从一个假 premise 继续推理。
