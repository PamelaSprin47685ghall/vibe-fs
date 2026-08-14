# DSL 结构化程序规则 — 理由

行为见 `what/dsl-structured-program.md`。

## 为什么必须用语言结构而不是状态机字段

F# 调用栈已经为业务流程提供了结构化边界：`let!` 是等待，`match` 是分支，`return!` 是继续，`try/finally` 是资源作用域。  
把「下一步去哪」重新编码为字段，等于在业务层重建一个第二运行时。恢复、调试、测试都会被迫理解这个手写运行时的语义。

## 为什么禁止大枚举式 DU 当程序计数器

大 DU 可以是领域词汇或持久事实，但不可以是「当前执行到第几步」。  
当 `InFlight/Parked/Sealed/Disposed` 与 `PendingOffer`、`Recovery`、`Drain` 正交组合时，可表示状态空间爆炸，大量组合无业务意义。类型系统不再拦截非法态，反而帮它们合法化。

## 为什么把 `slotArmed` 改为一次性 CE

`slotArmed` 注释即承认自己是 control-flow state。  
一次失败应当拥有一次结构化恢复机会：启动 `runRecoveryOpportunity`，`let!` 等待下一份材料，完成或取消后机会自然消失。不需要跨调用设置/查询/清除。

## 为什么从 transcript 推导 repair 而不是保存隐藏状态

`BloggerToolRecovery` 与 provider-visible `rawMessages` 是同一事实的两份副本。  
隐藏状态会老化、会丢失、会双写不一致。可观察 transcript 是唯一 SSOT；纯函数从它推导证据，删除隐藏状态，减少不一致面。

## 为什么拆分 `AgentFact`

41-case 大总和类型横跨多个 bounded context，所有 fold 被迫依赖同一个不断膨胀的全局目录。  
分 family 后，每个 bounded context 拥有自己的不变量、codec、fold 和演化策略。外层只做一次 `match`，不构造解释器。

## 为什么升级 `dsl-ownership`

名称黑名单只能防住已经想起来的坏名字。  
语义检查才能持续识别「换个名字的状态机」。`ref` 与 `mutable` 同样写入可变存储；只检查关键字会奖励语法替换而放行同一程序计数器。门禁必须先被故意破坏并变红，才算存在。
