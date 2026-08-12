# duplicated-control-flow — Enforcer 中文版

## 定义
重复控制流不是“代码长得像”。它是同一个 workflow、验证顺序、retry protocol 或 state transition 被两个以上 owner 各自重新实现，于是一个协议拥有多个权威版本。

文本重复只是表象。真正昂贵的是**时间知识被复制**：谁先做、谁失败后取消谁、什么结果才允许下一步、什么情况下重试、哪个 side effect 只能发生一次。这些一旦被复制，就会一处修复、一处遗忘。

## 何时触发
当出现以下情形时触发：

- 同一业务流程在 CLI、HTTP、job、recovery 各写一遍；
- 两个 caller 都手写相同 validate → mutate → persist → publish 顺序；
- retry/backoff/abort 规则被多个 adapter 各自实现；
- 状态迁移表在 runtime 与 migration/recovery 中各有一份独立逻辑；
- 修改协议时必须靠人工记住“另外几处也要同步改”。

## 不要误判
- 两段 loop 形状相似，但 domain reason、失败语义、owner 不同，不是重复协议；
- test 观察 production sequence，不等于复制 owner，只要 test 不重新实现决策；
- 一个 canonical workflow 被参数化后从多个入口调用，不是重复；
- 同一个 algebra 在不同 context 中恰好都先 validate 再 save，也不能仅凭形状合并。

## 刀口
问：**如果协议规则明天改变，我必须改几处 production owner 才能保证行为一致？**

答案大于一，就不是 DRY 美学问题，而是 authority duplication。

## 与近邻区分
`duplicated-truth` 复制的是“一个事实”；这里复制的是“如何从事实推进到下一步”的流程知识。

`premature-unification` 是把不同原因的相似代码硬并在一起。只有当两处必须因同一规则同时变化，才应该统一 control flow。

## 例子
- 正例：HTTP handler 与 background worker 各自实现一套“reserve → charge → confirm”，且 rollback 顺序略有差异。
- 近邻：两个独立业务流程都使用 `try/finally`，结构相似但 invariant 不同。
- 反例：两入口都调用同一个 `PlaceOrderWorkflow`，自己只负责输入输出 adapter。

## 提醒
不要去“消除重复代码”；去消除**重复的协议权威**。文字可以重复，authority 不该重复。
