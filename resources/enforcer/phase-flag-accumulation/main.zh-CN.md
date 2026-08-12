# phase-flag-accumulation — Main 中文版

## 现在该做什么
列出真实 lifecycle states 与合法 transitions，用 closed state model 或局部 structured control flow 替代 flag product。状态特有数据放进对应 case，不再靠 nullable sibling fields 配合 boolean。

## 为什么这很重要
Flag model 把构造非法世界的成本推给每个 reader。每段代码都要重新证明“如果 retrying 就一定 started、如果 done 就不能 waiting”。一个强状态模型只需在 transition owner 证明一次。

## 修复策略
1. 从 domain lifecycle 写出合法 states，不从现有 flags 倒推；
2. 标注每个 state 独有的数据；
3. 建 explicit transitions；
4. 迁移 readers/writers 到新 state；
5. 删除旧 flags，不做 mirror；
6. 对所有 finite transitions 做 exhaustive test。

## 常见假修复
- 加一个 `phase` enum，却继续维护原有 flags；这变成 `duplicated-truth`。
- 每发现一个 illegal combination 就再加 assertion。
- 用更复杂的 boolean expression “规范化”状态。
- 生成一个巨型 enum 包含本来真正独立的 capabilities。
- 把 flags 挪进一个 helper record，但 state space 没变。

## 验证
旧模型中每个非法组合应变成无法构造；新增 state/event 时，编译器或 transition test 应迫使所有相关分支重新决策，而不是自动落入 default。

## 完成条件
representable lifecycle states 与 legitimate lifecycle states 对齐；读者看一个 state case 就知道当前 phase，不再需要解 boolean 谜题。
