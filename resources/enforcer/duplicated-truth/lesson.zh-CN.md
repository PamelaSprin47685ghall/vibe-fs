# duplicated-truth — Lesson

## 评价

### 定义

同一个事实有多个可独立写入、且都被当作 authoritative 的表示时，truth 被复制了。

问题不是“数据重复”。同一个事实可以有 cache、index、read model、API view、日志副本；只要它们是**单向派生**的，就没有多重主权。真正的缺陷是两个地方都能说“我这里改了，所以事实现在就是这样”。从这一刻起，disagreement 不再是异常，而成为合法可表示状态。

### 支配原则

一个事实可以有许多 projection，但只能有一个 authority。一旦两个可写表示声称地位平等，分歧就变成合法的系统状态，每次读取都必须回答一个新问题：哪份副本说了算？同步代码消除不了这个问题；它只是为模型已经允许的矛盾定义越来越复杂的善后仪式。

### 何时触发

- DB column 与 config file 都可修改同一个业务设置；
- 内存 map 与 journal 都可各自写入“当前状态”，重启时再猜谁更真；
- old/new representation 双写且双方都可独立接受 mutation；
- cache miss 能自己创造新值，而不是回到 source；
- 两个 subsystem 都维护同一个 lifecycle status，并靠 reconciliation 定期“对齐”。

### 不应触发

- projection/cache/read model 可完全从唯一 source 重建，不是 duplicated truth；
- event history 与当前 folded state 不是两个事实：后者若只是历史的函数，仍是一条 authority chain；
- display copy、metrics、logs 不能写回事实，不构成第二 owner；
- 两个不同事实长得一样，不应因为 shape 相似就合并。

### 与相邻规则区分

`snapshot-as-truth` 是一个 derived representation 被抬成 authority；`duplicated-truth` 更一般：任何两个 writable authority 都算。

`compatibility-cruft` 可能保留两种 representation，但只要旧形态 decode-only、不能反向写回，就不必触发本规则。

### 判定程序

问：**当两份表示不一致时，系统能否不靠 heuristic、不靠 timestamp、不靠“谁最后写”直接知道谁错了？**

如果不能，说明模型已经授权了多个 truth owner。

### 例子

- 正例：feature flag 同时能在 DB 与 YAML 修改，应用启动时选“修改时间较新”的那个。
- 近邻：DB 是唯一 writer，Redis 只是可删除 cache。
- 反例：journal 是 fact source，snapshot 有 source digest，失配即丢弃重放。

## 建议

### 现在该做什么

选出唯一 writable authority。其它表示全部降级为 projection、cache、index、decode compatibility 或 read model，并明确它们如何从 source 派生、如何失效、如何重建。

如果目前两个地方都能写，先停掉其中一个 writer，再删掉 reconciliation ritual。不要反过来给同步逻辑加更多 conflict policy。

### 为什么重要

多权威模型把“事实是什么”变成运行时仲裁问题。每次读取都隐含一个新决策：信 DB、信 file、信 memory、信 timestamp，还是信最后一次 sync？系统越努力同步，越容易把一个本可不存在的问题制度化。

最危险的是分歧常常不立即爆炸。两个 copy 可以长时间“碰巧一样”，直到 crash、partial rollout、rare write path 或人工修改把它们分开。

### 修复策略

- 明确一条 `write -> authority -> projections` 的单向链；
- secondary representation 只能由 authority 更新或重建；
- projection mismatch 必须以 source 为准，而不是投票；
- historical compatibility 若必须保留，做 decode-only，不保留旧 writer；
- 如果两个 representation 实际代表不同事实，重新命名并分开语义，不要硬同步。

### 决策分支

- 若两个 store 都能写这个事实，选一个 authority，让另一个成为派生 projection；
- 若 cache 可重建，让它保持只读、可丢弃；绝不让 cache miss 发明一次写入；
- 若 snapshot 被当作活真相，那是 `snapshot-as-truth`；降级 snapshot，而不是加 tie-breaker。

### 常见假修复

- 增加 last-write-wins timestamp；这只是把“谁是真”交给时钟。
- 双写后定期 reconcile；这保留了两个 writer。
- 加一个 third “master cache” 来决定前两份谁赢。
- 用 distributed lock 保证两个 source 同时写；锁能协调写入，却没回答为什么要有两个 truth owner。
- 在文档写“DB is primary”但代码仍允许 file/admin path 覆盖。

### 验证

故意让 secondary representation stale、corrupt 或缺失。系统应能从唯一 authority 确定事实并恢复 secondary，而不是产生 ambiguous conflict。

再搜索所有 mutation entry：同一事实应只存在一条 authoritative write path。

### 完成条件

一个事实可以有很多副本，但只有一个地方有权改变它。其它表示删掉最多影响性能或展示，不会改变“世界到底是什么”。

### Nudge

同步机制不能治愈 duplicated truth。同步只是承认你允许两个权威先互相矛盾，再安排仪式收拾残局。
