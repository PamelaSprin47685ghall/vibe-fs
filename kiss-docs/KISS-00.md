# Structured Flow: 万象术本质重构第一原理

结论稿。执行闭合以 KISS-Driver、KISS-01～04、Tools 为准。

持久化正式名：

> **Per-Runtime Journal + Lifetime Snapshot Isolation + Chronological Replay**（KISS-04）

---

## 一、断根：去函数化控制流

业务流程被手工展开为 Phase/Stage/Lease/Owner/Generation，再伪装成领域。  
解法：结构化程序 + 本 Runtime 内单 Driver，而非治理框架与跨进程实时一致性。

行数 = 健康观察；禁凑门禁拆空壳。

### 1.1 程序计数器被伪装成领域状态

当源码把一段普通顺序程序：

```
for model in models do
    for attempt in attempts do
        match! send model attempt with
        | Completed result -> return result
        | RetryableFailure _ -> continue
        | FatalFailure e -> fail e
```

拆成 `FallbackPhase`、`ContinuationStage`、`PendingLease`、`Generation` 和多个 Registry 时，新增的字段并没有增加用户事实。它们只是把编译器原本会维护的 continuation、循环变量和异常路径搬到业务数据里。

判断标准：

|字段来源|处理|
|---|---|
|用户看见或外部系统确认的事实|持久化并进入领域 Fold|
|外部协议确实存在的不可观测状态|保留极小 ADT，局限于本 Runtime|
|“代码运行到哪一步”|删除，回到 while/await/return/尾递归|

### 1.2 结构化程序的目标形状

```text
Session:  while Todo.Unfinished → ContinueWork → while Review.Required → RequestReview
Process:  use Spawn → pump → wait → drain → dispose
Journal:  open snapshot → append own fact → flush → apply own projection
Child:    get/create → attach waiter → send → terminal → result
Squad:    parallel work → ordered verify/FF → wave accepted
```

这些主路径不应出现 `Manager`、`Coordinator`、`Stage`、`Lease` 或“先调用再刷新”的跳转链。可靠性属于动词内部；源码只留下业务顺序。

---

## 二、真实复杂

- 持久化：每 Runtime 单写 NDJSON；启动固定前沿快照；先盘后内存；半行忽略；不改他文件。  
- 外部协议：进程、prompt 回执、child、**本侧** Prompt 协议 + Host MessageId。  
- 依赖方向 Host→Runtime→Kernel。  
- 必留功能属性：子代理、Fallback 隔离、续跑语义非 Nudge、OpenCode codec、万象阵。  
- **不**把「多 OpenCode 实时共享一内存世界」当需求。

### 2.1 持久化复杂度的边界

Per-Runtime Journal 并没有取消持久化困难，只把它压缩到可证明边界：

1. 自己的文件只有一个 Writer；
2. 每一行自包含、带 RuntimeId、LocalSeq、CommittedAt 和 Fact；
3. 启动只读固定 byte Frontier；
4. EOF 半行可忽略，中间非法行只降级该源；
5. 本进程先 flush 再更新自己的内存投影；
6. 其他 Runtime 的后续变化不进入当前生命周期；
7. 重启时重新枚举、归并、Fold。

这不是“最终会同步”，而是**生命周期级快照隔离**。它主动放弃实时共享，换取没有跨进程 owner、锁转移、watcher 和 IPC 投影同步。

---

## 三、虚假耦合（删）

全局 SessionState；Nudge/idle 调度；全局 SessionOwner；Fallback SM 塔；Subsession 套餐；动态流水线；多宿主平台；业务 EventBus；旧 NDJSON 兼容；  
**以及**：workspace/session 跨进程写锁、Owner 代写、Previous/Fork 协议、实时 tail 汇合。

---

## 四、原则表

|原则|含义|
|---|---|
|源码=过程|while/try/match/return；early-exit 用尾递归|
|调用者不担可靠|动词完整语义单位（本 Runtime 可见状态）|
|幂等|稳定键；本地 sendOnce；无 stepId|
|投影|返回前本侧最新；Stamp 本侧进展|
|资源|use! 异步可等待|
|删 PC|Phase/Stage/Lease/Owner/Generation 出业务源码|
|恢复|主程序重放；不持久化 continuation|
|保留事实|全部 Runtime 日志在重启时按时间归并；不删他进程历史|
|Driver|每 Runtime 内每 Session 0|1|
|Origin|仅 Human 升本侧 Epoch|
|Outcome≠Error|分支 DU vs 终止 error|
|Journal|Per-Runtime 单写；Lifetime Snapshot Isolation|
|同 Session 多进程|合法；陈旧视图上操作；重启时间线 Fold|

---

## 五、冻结 / Oracle / 迁移

Mux/OMP 冻结。旧 OpenCode=Oracle。独立 next。双轨期行数可暂增。

迁移只搬用户行为与外部事实，不搬旧目录形状：

```
旧入口/旧测试 → 行为 ID 与公开结果 → 新领域 owner
→ 黄金源码 → 契约/重启/故障测试 → 标记旧实现已替代
```

旧版可提供 characterization 输入，但新版测试不得断言旧 module、Stage、Coordinator 或日志布局。Mux/OMP 冻结代码不得为迁移方便被新架构反向污染。

---

## 六、卷表

Driver · 01 Flow · 02 契约 · 03 桥 · 04 Journal（本模型）· 05 Process · 06 Session · 07–12 · 13 实施 · Tools。

---

*KISS-00 终。*
