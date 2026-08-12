# dirty-hack — Enforcer 中文版

## 定义
Dirty hack 是明知 governing model/invariant 在这里是错的，却加一个局部 bypass 让当前路径继续工作。它不是“代码丑”，而是系统开始同时维护两套 specification：公开模型，以及只有 workaround 知道的真实例外。

## 何时触发
- magic ID/path/flag 绕过正常 ownership；
- special-case branch 只因为现有 abstraction 表达不了真实情况；
- fallback/shim 让已知错误模型继续活着；
- exception 没有 domain 名字、contract、owner，唯一解释是“否则这个 case 跑不通”。

## 不要误判
- 现实本身确实有稳定 domain exception，且有名字、测试、规则；
- foreign protocol 在 adapter 做必要翻译；
- 明确 time-boxed spike 不进入 shipping path；
- bounded compatibility branch 有真实外部 creditor。

## 刀口
问 special case：“**哪条领域事实让你合法？**”

如果答案不是领域事实，而是“我们的模型/架构这里坏了”，就不要给 workaround 永久身份。

## 提醒
没有 domain meaning 的 workaround 是一份秘密模型。每多一个 hack，真正 specification 就更远离 canonical structure。
