# KISS 与奥卡姆剃刀重构指南 — 索引

| Field | Value |
| :--- | :--- |
| **Status** | 原则批准 · Journal 定为 Lifetime Snapshot Isolation · Phase 0 余 host spike + GuideContract |
| **Journal** | Per-Runtime 单写 NDJSON · 启动 Frontier 快照 · 重启时间归并 · 无跨进程锁 |

## 系列总表

|卷|主题|
|---|---|
|`KISS-00.md`|第一原理|
|`KISS-Driver.md`|本 Runtime 内 Driver · FIFO · 本地 Prompt · Origin|
|`KISS-01.md`|Flow 内核|
|`KISS-02.md`|动词契约（本侧后置）|
|`KISS-03.md`|宿主桥 · Boot Gateway|
|`KISS-04.md`|**Per-Runtime Journal + Snapshot Isolation + Chronological Replay**|
|`KISS-05.md`|Process|
|`KISS-06.md`|Session Runtime 投影|
|`KISS-07`–`12`|Child…Squad|
|`KISS-13.md`|实施 · 18 条隔离测|
|`KISS-Tools.md`|Tools · CommandPort · Deadline|

## 持久化一句话

> 每进程自己的 ndjson（单写）；启动读所有日志固定前缀交错归并；运行只积分自己；重启再汇合。同 Session 多进程合法。无 lockfile / Owner / Fork 协议。

## 阅读顺序与术语

先读 `KISS-00` 的诊断，再读 `KISS-Driver` 的本 Runtime 执行边界，最后读 `KISS-04` 的跨 Runtime 持久化语义。两条边界不能混淆：

|概念|范围|保证|
|---|---|---|
|Driver|`(RuntimeId, SessionId)`|本进程内 0 或 1 个；唯一消费 Inbox、运行主程序、提交本日志|
|PromptProtocol|本 Runtime 的 Session|本地防重；不构成跨进程全局互斥|
|Journal|`RuntimeId`|单进程单写、多进程只读；不续写别人的文件|
|BootSnapshot|一次启动|所有源在 Frontier 内的完整合法前缀|
|CurrentState|运行期间|BootSnapshot + 本 Runtime 自己 flush 后的事件|
|Chronological Replay|下一次启动|按 `CommittedAt → RuntimeId → LocalSeq` 稳定归并|

“快照隔离”不是“最终一致性”。当前 Runtime 不会自动看到其他 Runtime 的后续事件；只有新进程建立新 Frontier 后重新归并。允许陈旧视图上的合法事实，是产品取舍，不是待修复的并发 bug。

## 不变式

1. 任意 Runtime 文件最多一个 Writer，Writer 只属于创建它的进程。
2. Reader 不截断、不修复、不追加任何其他 Runtime 文件。
3. 每个 Runtime 内 `LocalSeq` 严格递增，`CommittedAt` 单调不倒退。
4. 单源损坏只降低该源的可见前缀，不阻断其他源的启动恢复。
5. 同一 Session 的多个 Runtime 可以同时产生事实；存储层不判定 fork，不静默删除事实。
6. 领域 Fold 决定较晚事实如何影响投影；Journal 不伪造冲突解决器。

## 被明确拒绝的方向

|方向|拒绝原因|
|---|---|
|Workspace 总锁|把互不需要实时一致性的 Runtime 强行绑成单写者|
|Session ownership|制造接管、stale lock、owner 转发与恢复协议|
|实时 tail / watcher|把启动快照问题升级成 IPC、offset、生命周期同步平台|
|Previous/Fork 检测|把允许的陈旧操作误判为存储冲突|
|按 wall clock LWW 引擎|领域覆盖规则不应藏进持久化层|
|全局 WorkspaceState|重新制造被删除的复合状态|

## 审查闭合摘要

- 结构化 Flow + 本侧单 Driver + Outcome/Error + Progress + Host MessageId  
- Journal：**非** workspace 单写锁，而是 Lifetime Snapshot Isolation  
- 时间归并确定性；领域 Fold 定义覆盖；非 LWW 冲突引擎  

## Phase 0 剩余动作

1. OpenCode host spike  
2. GuideContract 真编译  
3. 单套正式文件名  
4. Status → `Phase 0 已闭合 · 准许 Phase A`

Phase 0 不是再写一卷架构说明。它的验收对象是：真实 OpenCode host spike、GuideContract 的编译、两个 Runtime 的隔离测试，以及仓库中不存在同名旧稿。完成前不实现业务功能。

## 标记

`[NORMATIVE]` · `[ILLUSTRATIVE]` · `[FORBIDDEN]`

---

*旧稿仅 Git 历史。*
