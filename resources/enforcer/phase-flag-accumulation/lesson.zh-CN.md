# phase-flag-accumulation — Lesson

## 评价

### 定义

Phase flag accumulation 是一种“每修一个 lifecycle bug 就加一个 boolean”的设计债。`started/waiting/retrying/done/cancelled/...` 单看都合理，组合起来却在暗中形成一个没有名字、没有 transition table 的状态机。

每增加一个独立 flag，representation 都在乘法扩张；真实 lifecycle 通常没有这么多世界。多出来的组合不是 flexibility，而是代码以后必须不断排除的虚构状态。

### 支配原则

每一个独立 flag 都在乘法式地扩大可能世界。当这些 flag 实际上描述同一个 lifecycle 时，product type 发明了进程永远无法合法进入的组合，并隐藏了连接合法状态的是哪些 transition。结果系统成了一台用位运算表达的状态机，读者必须从组合与控制历史里重建状态才能判断正确性。

### 何时触发

- lifecycle bug 修复不断新增 bool/counter；
- 分支条件开始出现 `started && !done && retrying && hasLease`；
- 同一“阶段”需要从多字段组合猜出来；
- 不合法组合只能靠 assertions/comments 禁止；
- transition 不是 `State × Event -> State`，而是到处 set/unset flags。

### 不应触发

- 真正独立、可以自由组合的 feature/capability flags 没问题；
- local temporary boolean 不承担 durable/shared lifecycle；
- 一个显式 state union 外加与 lifecycle 无关的独立属性，不是 accumulation；
- bitset 若语义就是独立 capabilities，也不应硬改成巨型 enum。

### 与相邻规则区分

`boolean-blindness` 更一般；这里专门指一组 flags 共同回答“我们现在处于 lifecycle 的哪里”。

`program-counter-state` 是直接持久化“下一步代码执行到哪里”；phase flags 则用 bits 间接拼出那个位置。

### 判定程序

把所有 flag 做 truth table。**真实 domain 允许其中多少组合？**

如果 representation 允许 32 个，而业务只承认 5 个，剩下 27 个世界都是模型自己发明的。

### 例子

- 正例：job 有 `started/waiting/retrying/done` 四个 bool，并不断修 `done && retrying` 的 bug。
- 近邻：`notifyEmail/notifySms` 是可自由组合用户偏好。
- 反例：`Pending | Running of Lease | WaitingRetry of DueAt | Completed`，每个 phase 只携带对自己有意义的数据。

## 建议

### 现在该做什么

列出真实 lifecycle states 与合法 transitions，用 closed state model 或局部 structured control flow 替代 flag product。状态特有数据放进对应 case，不再靠 nullable sibling fields 配合 boolean。

### 为什么重要

Flag model 把构造非法世界的成本推给每个 reader。每段代码都要重新证明“如果 retrying 就一定 started、如果 done 就不能 waiting”。一个强状态模型只需在 transition owner 证明一次。

### 修复策略

1. 从 domain lifecycle 写出合法 states，不从现有 flags 倒推；
2. 标注每个 state 独有的数据；
3. 建 explicit transitions；
4. 迁移 readers/writers 到新 state；
5. 删除旧 flags，不做 mirror；
6. 对所有 finite transitions 做 exhaustive test。

### 决策分支

- 若这些 flag 编码同一个 lifecycle，用命名状态与合法 transitions 替代 product；
- 若它们是可自由组合的独立 predicates，保持原样；它们不是隐藏的自动机。

### 常见假修复

- 加一个 `phase` enum，却继续维护原有 flags；这变成 `duplicated-truth`。
- 每发现一个 illegal combination 就再加 assertion。
- 用更复杂的 boolean expression “规范化”状态。
- 生成一个巨型 enum 包含本来真正独立的 capabilities。
- 把 flags 挪进一个 helper record，但 state space 没变。

### 验证

旧模型中每个非法组合应变成无法构造；新增 state/event 时，编译器或 transition test 应迫使所有相关分支重新决策，而不是自动落入 default。

### 完成条件

representable lifecycle states 与 legitimate lifecycle states 对齐；读者看一个 state case 就知道当前 phase，不再需要解 boolean 谜题。

### Nudge

如果几个 boolean 合起来是在回答“现在在哪个阶段”，就别再加 boolean。把那个阶段直接建模。
