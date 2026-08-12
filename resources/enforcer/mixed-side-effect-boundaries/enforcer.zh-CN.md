# mixed-side-effect-boundaries — Enforcer 中文版

## 定义
一个 unit 同时碰 DB、HTTP、Git、process、filesystem 并不自动有罪。真正的病灶是：**这些拥有不同 failure / lifetime / retry / authority law 的外部世界，被同一个 imperative policy owner 混成一团**。

当“该做什么”的业务判断与“每个外部世界怎样失败”的适配细节纠缠，测试就必须一次模拟整个宇宙，任何 effect 的变化都会迫使无关 policy 跟着改。

## 何时触发
- 一个 service method 一边决定业务规则，一边直接 transaction、shell、HTTP、filesystem；
- retry policy、rollback、auth、domain branching 与多个 adapter call 交错；
- 某个 effect 的 timeout/error shape 泄漏进另一个 effect 的 decision；
- 单测一个 policy 需要构造 DB、Git、network、process 四套 fixture。

## 不要误判
- composition root / application workflow 只顺序执行已经决定好的 commands，可以同时认识多个 adapter；
- 同一 store contract 下的多个 query 不算“多个外部世界”；
- generated adapter 做机械协议翻译而不拥有 policy，不必为了 purity 再拆十层；
- 跨多个 effect 的 atomic workflow 如果确实是 application concern，可以有一个 orchestrator，但各 effect law 仍应由自己的 adapter 拥有。

## 刀口
问：**如果只改变其中一个外部系统的 failure model，为什么业务 policy 也必须被打开修改？**

若答案只是“因为它们都写在这个 function 里”，边界已经混了。

## 与近邻区分
`impure-core` 关注 policy 主动观察/执行外界；`mixed-side-effect-boundaries` 更强调多个不同 effect contract 被一个 owner 一起承担。

`god-module` 范围更广；这里即使函数不大，只要 effect laws 互相污染，也成立。

## 例子
- 正例：一个 `deploy()` 同时判断是否可发布、开 Git worktree、写 DB、启动 process、POST webhook，并在一串 catch 中决定补偿。
- 近邻：workflow 接收 `DeploymentPlan`，依次调用 Git/Store/Process ports；每个 adapter 各自翻译 failure，workflow 只处理 typed outcome。
- 反例：为了“分层”给每个一行调用套一个 forwarding service，但 failure/policy 仍然混在顶层。

## 提醒
边界不是按 API 数量切，而是按**不同世界拥有不同失败定律**来切。
