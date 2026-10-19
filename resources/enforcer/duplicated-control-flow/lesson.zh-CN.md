# duplicated-control-flow — Lesson

## 评价

### 定义

重复控制流不是“代码长得像”。它是同一个 workflow、验证顺序、retry protocol 或 state transition 被两个以上 owner 各自重新实现，于是一个协议拥有多个权威版本。

文本重复只是表象。真正昂贵的是**时间知识被复制**：谁先做、谁失败后取消谁、什么结果才允许下一步、什么情况下重试、哪个 side effect 只能发生一次。这些一旦被复制，就会一处修复、一处遗忘。

### 支配原则

文字重复可以容忍，protocol authority 的重复不行。真正昂贵的不是长得像的代码，而是被复制的时间知识——谁先做、谁失败后取消谁、什么结果才允许下一步。协议一旦有了多个各自维护的权威版本，“一处修复、一处遗忘”就只是时间问题；凡是要靠人工记住“另外几处也要同步改”的地方，就是 authority duplication 正在生效的地方。

### 何时触发

当出现以下情形时触发：

- 同一业务流程在 CLI、HTTP、job、recovery 各写一遍；
- 两个 caller 都手写相同 validate → mutate → persist → publish 顺序；
- retry/backoff/abort 规则被多个 adapter 各自实现；
- 状态迁移表在 runtime 与 migration/recovery 中各有一份独立逻辑；
- 修改协议时必须靠人工记住“另外几处也要同步改”。

### 不应触发

- 两段 loop 形状相似，但 domain reason、失败语义、owner 不同，不是重复协议；
- test 观察 production sequence，不等于复制 owner，只要 test 不重新实现决策；
- 一个 canonical workflow 被参数化后从多个入口调用，不是重复；
- 同一个 algebra 在不同 context 中恰好都先 validate 再 save，也不能仅凭形状合并。

### 与相邻规则区分

`duplicated-truth` 复制的是“一个事实”；这里复制的是“如何从事实推进到下一步”的流程知识。

`premature-unification` 是把不同原因的相似代码硬并在一起。只有当两处必须因同一规则同时变化，才应该统一 control flow。

### 判定程序

问：**如果协议规则明天改变，我必须改几处 production owner 才能保证行为一致？**

答案大于一，就不是 DRY 美学问题，而是 authority duplication。

### 例子

- 正例：HTTP handler 与 background worker 各自实现一套“reserve → charge → confirm”，且 rollback 顺序略有差异。
- 近邻：两个独立业务流程都使用 `try/finally`，结构相似但 invariant 不同。
- 反例：两入口都调用同一个 `PlaceOrderWorkflow`，自己只负责输入输出 adapter。

## 建议

### 现在该做什么

先确定协议真正的 owner，再把顺序、失败、retry、取消和 side-effect 约束集中到这一处。其它入口只负责翻译输入、调用 canonical workflow、翻译结果。

### 为什么重要

重复 control flow 会把一个 protocol 变成多个版本。最危险的不是明显分叉，而是“几乎一样”：99% 相同让人以为它们等价，剩下 1% 的错误处理或 ordering 差异恰好只在故障时暴露。

这类 bug 很难通过 code review 发现，因为每个局部实现单看都合理；错误存在于“两个 owner 本应相同却已经不同”。

### 修复策略

1. 写出 canonical workflow 的 ordered facts；
2. 区分 domain decision 与 adapter effect；
3. 选一个 owner 承担 protocol；
4. 把多个入口改成 caller，而不是复制者；
5. 把 legitimate variation 做成显式参数或不同 protocol，不要藏在分叉副本里；
6. 删除旧实现，避免“先留着保险”。

### 决策分支

- 若两处必须因同一规则同时变化，统一 owner。
- 若两处只有表面相似、失败语义不同，保持分离，不做 DRY 表演。
- 若差异是合法 policy variation，把 variation 建模，而不是维护两个 copied workflow。
- 若历史 recovery 需要旧步骤，只保留 decode/replay compatibility，不保留旧 write authority。

### 常见假修复

- 抽几个 helper，但两套顶层 sequence 仍各自决定 ordering。
- 复制一份 workflow 再“以后同步”。
- 用 comments 写“保持与 X 一致”。
- 创建一个 generic orchestrator，但真实 branching 仍散在 callers。
- 只做文本 dedup，把不同语义硬塞进一个 boolean-heavy function。

### 验证

改变一个关键协议规则，例如“persist 必须先于 publish”或“charge failure 必须 release reservation”。production 中应只有一个 owner 需要改，所有入口自然继承该规则。

### 完成条件

协议知识只有一个可修改权威；入口可以多，representation 可以多，但 workflow 的 ordering / failure / retry semantics 不再由多个实现各自解释。

### Nudge

不要去“消除重复代码”；去消除**重复的协议权威**。文字可以重复，authority 不该重复。
