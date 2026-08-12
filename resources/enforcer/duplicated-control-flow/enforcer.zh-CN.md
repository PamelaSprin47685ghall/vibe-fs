# duplicated-control-flow — Enforcer

Duplicated control flow 不是“两段代码长得像”，而是**同一个 temporal protocol 被两个 owner 各自实现了一遍**。

Workflow 本身就是知识：哪个 step 先发生、什么 failure 停止后续、哪个 result 才允许继续、retry 在哪一层、cleanup 谁负责。把这套 sequence copy 到两处，相当于给一个 protocol 建了两个 authority。它们今天可以逐行一样，明天只需一个 bug fix 就会变成两个不同世界。

以下情形触发：

- 同一 validate→persist→publish sequence 在多个 entry point 手写；
- retry/backoff/cancel protocol 被几套 caller 分别实现；
- 相同 lifecycle transition 在 HTTP、CLI、background job 各有独立 `if/await` 链；
- 一个 fix 需要“记得同步改另外三处”；
- 两个 owner 对 failure order、cleanup、idempotency 逐渐产生不同规则，却没人明确决定差异是合法的。

不要因为两个 loop 都是 `map/filter/retry` 就触发。Shape 相似不等于 protocol 相同。若两段流程有不同 domain owner、failure semantics、reason to change，即使代码几乎一致，也可能应保持独立。

也不要把 test 对 production order 的**观察**误判成第二实现。Test 若只是断言 contractually significant order，没有复制整套 decision logic，它是 witness，不是 authority。

与 `duplicated-truth` 区分：那里是同一 present fact 有多个 writable authority；本规则是同一**过程/时序规则**有多个实现 authority。与 `missing-rule-combinator` 区分：那里 named rules 已经存在，只是 composition mechanics 重复；这里整个 workflow protocol 都被重复拥有。

判定问题：如果业务把这条 protocol 改一处，另一处是否**必然**也必须改才能保持正确？若 yes，而且没有一个 shared owner 负责这次变化，就是 duplicated control knowledge。

> 重复文本只是维护成本；重复 protocol 是主权冲突。真正该去重的是“谁决定顺序和失败语义”。