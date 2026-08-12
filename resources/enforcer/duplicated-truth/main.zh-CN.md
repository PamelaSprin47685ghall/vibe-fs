# duplicated-truth — Main 中文版

## 现在该做什么
选出唯一 writable authority。其它表示全部降级为 projection、cache、index、decode compatibility 或 read model，并明确它们如何从 source 派生、如何失效、如何重建。

如果目前两个地方都能写，先停掉其中一个 writer，再删掉 reconciliation ritual。不要反过来给同步逻辑加更多 conflict policy。

## 为什么这很重要
多权威模型把“事实是什么”变成运行时仲裁问题。每次读取都隐含一个新决策：信 DB、信 file、信 memory、信 timestamp，还是信最后一次 sync？系统越努力同步，越容易把一个本可不存在的问题制度化。

最危险的是分歧常常不立即爆炸。两个 copy 可以长时间“碰巧一样”，直到 crash、partial rollout、rare write path 或人工修改把它们分开。

## 修复策略
- 明确一条 `write -> authority -> projections` 的单向链；
- secondary representation 只能由 authority 更新或重建；
- projection mismatch 必须以 source 为准，而不是投票；
- historical compatibility 若必须保留，做 decode-only，不保留旧 writer；
- 如果两个 representation 实际代表不同事实，重新命名并分开语义，不要硬同步。

## 常见假修复
- 增加 last-write-wins timestamp；这只是把“谁是真”交给时钟。
- 双写后定期 reconcile；这保留了两个 writer。
- 加一个 third “master cache” 来决定前两份谁赢。
- 用 distributed lock 保证两个 source 同时写；锁能协调写入，却没回答为什么要有两个 truth owner。
- 在文档写“DB is primary”但代码仍允许 file/admin path 覆盖。

## 验证
故意让 secondary representation stale、corrupt 或缺失。系统应能从唯一 authority 确定事实并恢复 secondary，而不是产生 ambiguous conflict。

再搜索所有 mutation entry：同一事实应只存在一条 authoritative write path。

## 完成条件
一个事实可以有很多副本，但只有一个地方有权改变它。其它表示删掉最多影响性能或展示，不会改变“世界到底是什么”。
