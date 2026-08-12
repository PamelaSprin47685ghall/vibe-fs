# phase-flag-accumulation — Enforcer 中文版

## 定义
Phase flag accumulation 是一种“每修一个 lifecycle bug 就加一个 boolean”的设计债。`started/waiting/retrying/done/cancelled/...` 单看都合理，组合起来却在暗中形成一个没有名字、没有 transition table 的状态机。

每增加一个独立 flag，representation 都在乘法扩张；真实 lifecycle 通常没有这么多世界。多出来的组合不是 flexibility，而是代码以后必须不断排除的虚构状态。

## 何时触发
- lifecycle bug 修复不断新增 bool/counter；
- 分支条件开始出现 `started && !done && retrying && hasLease`；
- 同一“阶段”需要从多字段组合猜出来；
- 不合法组合只能靠 assertions/comments 禁止；
- transition 不是 `State × Event -> State`，而是到处 set/unset flags。

## 不要误判
- 真正独立、可以自由组合的 feature/capability flags 没问题；
- local temporary boolean 不承担 durable/shared lifecycle；
- 一个显式 state union 外加与 lifecycle 无关的独立属性，不是 accumulation；
- bitset 若语义就是独立 capabilities，也不应硬改成巨型 enum。

## 刀口
把所有 flag 做 truth table。**真实 domain 允许其中多少组合？**

如果 representation 允许 32 个，而业务只承认 5 个，剩下 27 个世界都是模型自己发明的。

## 与近邻区分
`boolean-blindness` 更一般；这里专门指一组 flags 共同回答“我们现在处于 lifecycle 的哪里”。

`program-counter-state` 是直接持久化“下一步代码执行到哪里”；phase flags 则用 bits 间接拼出那个位置。

## 例子
- 正例：job 有 `started/waiting/retrying/done` 四个 bool，并不断修 `done && retrying` 的 bug。
- 近邻：`notifyEmail/notifySms` 是可自由组合用户偏好。
- 反例：`Pending | Running of Lease | WaitingRetry of DueAt | Completed`，每个 phase 只携带对自己有意义的数据。

## 提醒
如果几个 boolean 合起来是在回答“现在在哪个阶段”，就别再加 boolean。把那个阶段直接建模。
