# weak-boundary-parsing — Main 中文版

## 现在该做什么
在 ingress 一次完成 decode version、shape validation、关系 validation、normalization、unit/name conversion，然后构造内部 strong type。Raw protocol form 保持 private，不让 domain/application 再解析一次。

## 为什么这很重要
Repeated parsing 会产生 validation drift：A 层认为 field optional，B 层认为 required，C 层又从 string 猜 enum。越深入，原始协议上下文越少，却承担越多解释责任。

## 常见假修复
- 给 raw JSON 标一个 TypeScript interface 就宣布 parsed。
- controller validate 后仍传原 object。
- 建 `hasField/isValidShape` 工具让每层更方便继续猜。
- 把所有 boundary error 压成 generic string。

## 验证
Malformed/unsupported input 应在 ingress 以 typed outcome 失败；valid input 进入内部后不再出现重复 shape checks/raw property access。

## 完成条件
系统有一个明确点完成“外部表示 → 内部意义”的转换；uncertainty 到此为止，不再向核心扩散。
