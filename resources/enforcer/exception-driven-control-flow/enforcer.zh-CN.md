# exception-driven-control-flow — Enforcer 中文版

## 定义
Exception-driven control flow 是把正常运行中预期发生的普通分支交给 stack unwinding 表达。问题不是 exception “慢”，而是它把本应在类型/局部语法中可见的 alternatives 藏进远处 handler。

## 何时触发
- not-found、optional parse、loop termination 用 throw/catch；
- routine retry 依赖 exception 作为成功协议的一部分；
- caller 正常工作必须频繁 catch 才能继续；
- API signature 看起来只有 success，普通 branch 却从 exception channel 冒出。

## 不要误判
- invariant broken、programmer error、基础设施崩溃导致 ordinary continuation 无意义；
- foreign API 只能 throw，owned adapter 在边界立即翻成 typed outcome；
- 真正 domain refusal 更具体属于 `expected-failure-as-exception`；
- test 对真正 exceptional failure 做 throw assertion 很正常。

## 刀口
这个 outcome 是 caller 在正常业务过程中**预期遇到并处理**的吗？是，就应出现在 local control/type contract，不该靠非局部 stack jump 才知道。

## 提醒
Exception 的力量来自“跳出普通推理”。不要把普通推理本身也搬进去，否则函数签名会系统性少报它真实可能返回的世界。
