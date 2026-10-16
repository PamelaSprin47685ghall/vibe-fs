# dirty-hack — Enforcer 中文版

## 定义

Dirty hack 是明知 governing model/invariant 在这里是错的，却加一个局部 bypass 让当前路径继续工作。它不是“代码丑”，而是系统开始同时维护两套 specification：公开模型，以及只有 workaround 知道的真实例外。

## 支配原则

每个 special case 都是关于现实的声明。如果例外没有 domain meaning、只因为当前 abstraction 表达不了真相，代码库就获得两套模型：正式的那套，以及知道它在哪儿失效的 workaround。重复这个过程不会让系统稳定，只会把真正的 specification 分散到各个 escape hatch。

## 何时触发

- magic ID/path/flag 绕过正常 ownership；
- special-case branch 只因为现有 abstraction 表达不了真实情况；
- fallback/shim 让已知错误模型继续活着；
- exception 没有 domain 名字、contract、owner，唯一解释是“否则这个 case 跑不通”。

## 不应触发

- 现实本身确实有稳定 domain exception，且有名字、测试、规则；
- foreign protocol 在 adapter 做必要翻译；
- 明确 time-boxed spike 不进入 shipping path；
- bounded compatibility branch 有真实外部 creditor。

## 与相邻规则区分

`compatibility-cruft` 保留没有正当理由的历史 surface；`facade-hides-mess` 掩盖大范围架构债。本规则是一个局部 patch，其唯一语义是“让坏掉的模型在这里活下来”。Tie-break：如果 special case 存在是因为 invariant 在此处已知为 false，这个案子归本规则。

## 判定程序

问 special case：“哪条领域事实让你合法？”如果答案不是领域事实，而是“我们的模型/架构这里坏了”，就不要给 workaround 永久身份。

## 例子

- positive：额外的 `if (id === "legacy-user")` 绕过 ownership model，让一条路径继续工作。
- near-miss：有名字、有测试的 `InsufficientBalance` domain case，因为业务本身就包含这个拒绝。
- counterexample：修复 invariant 或 ownership boundary，然后删除 bypass。

## Nudge

没有 domain meaning 的 workaround 是一份秘密模型。每多一个 hack，真正 specification 就更远离 canonical structure。