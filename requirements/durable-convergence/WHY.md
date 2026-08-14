# WHY —— durable-convergence

## 一句话

两个 replica 都可能各自合法发展（A、B 离线同见 parent=P 各自 append A1/B1）。
同步不能靠 wall-clock/revision 选「较新」世界——那会**静默丢掉一个合法分支**；必须
保留共同事实，把真正的 domain conflict 显式交还领域。

## 为什么必须独立存在

`changes/completed/storage.md` §10 明确：万象术不能假设一个 repository = 一个
Wanxiang process。真实运行允许 OpenCode process A/B/C、IDE、external Git、remote
replica 同时存在，且任一 process crash 后其内存可以全部消失。因此：

- **没有** master process / leader / 全局锁 / 内存 authority——并发知识只通过
  immutable snapshot / root ref / CAS conflict / remote tracking snapshot 显现。
- 每个 process 是**独立 replica**，只 append 自己的 delta；合并是
  `KWayMerge(snapshot[])` 这个统一 primitive，推广到所有动态 durable 域。
- 跨机器只是多进程语义跨了机器边界（storage.md §42），不引入新协议。

若「谁赢」由时间戳决定（LWW），后果是确定的：**晚到的合法分支消失**，而它可能承载
用户已经依赖的事实。timestamp 不证明内容更新，revision 排序制造第二真相。

## 两个不可退让的支柱

1. **Storage 层永不丢事实。** merge = append-only set union + identity dedupe。
   两个不同 EventId 永远都进入 merged history——即使领域上互斥（同一 Job 同时
   Accept/Reject、同一 Case 同时 Close/Reopen）。绝对禁止 Store 做
   `wall_clock newer wins` 把其中一个 durable fact 消失（storage.md §10.6/§19）。
2. **Persist 负责不丢事实，Domain 负责解释事实是否相容。** 合法并发 fork 是物理层
   正常产物，不是全局 corruption；领域互斥由 projection 表达为 deterministic
   `Conflict` 状态，经以**全部 heads 为 parents** 的 resolution event 收敛
   （storage.md §5.3/§10.9）。真公式是
   `Projection(KWayMerge(S1..Sk)) = Fold(Union(Events(S1..Sk)))`，不是
   `Merge(Projection(S1), Projection(S2))`。

## 失败模式（RED 长什么样）

- 同步后一个合法分支消失（LWW 偷删）→ 用户事实丢失，且没有记录。
- 相同 object set 在 replica A 折叠成世界 W1、在 replica B 折叠成 W2 → 分叉真相。
- 自然 fork 被误判 StorageInvalid → 历史永久不可恢复。
- 单向 Pull/Push 让 remote 永远落后（Local={A,B}, Remote={A,C} 成功同步后仍不对称）。
- dumb server 开始理解 domain event → server 变成第二套领域运行时。
- `Merge(Projection1, Projection2)` 式合并 → 投影层面的 LWW/漂移。

## 被拒方案（考古）

| 方案 | 为什么拒 |
|---|---|
| revision + wall_clock LWW 裁决同 Case 冲突 | Store 保存 immutable facts 不是 mutable snapshot；timestamp 不证明内容未变；LWW = 丢分支（storage.md §19、§10.10；docs/why/casebook.md） |
| 单向 PullStore/PushStore/Download/Upload | 永远双向是永久 architecture invariant；任何同步入口都必须 fetch→union→validate→CAS→push（storage.md §11/§17） |
| server-side merge / pre-receive domain reducer | dumb remote 只懂 objects/refs/CAS/auth；智能全在 client（storage.md §12/§38） |
| Process Registry / leader / writer election | 所有并发知识只通过 snapshot/root/CAS/remote-tracking 显现（storage.md §10.7） |
| MergeStateMachine / PendingPeer / NeedMerge 队列 | k-way merge 是纯函数 primitive，从当前真实输入重新计算，不维护同步状态机（storage.md §10.8） |
| multi-remote CRDT（首版） | 一个 remote 一个同步算法；多 remote 是 future work（storage.md §22） |

## 与相邻包的边界（谁不归我）

- **`durable-events`**：单 store 的 append/CAS/canonical identity/确定性 fold。本包消费
  它，但「identity collision fail closed」「提交原子性」等单 store 律不归本包。
- **`knowledge-reuse`（Casebook）**：同一 Case 的合法并发 fork 必须显式 DomainConflict、
  禁 LWW——这是 Case **对象**语义（KNOWLEDGE-REUSE-011）；本包拥有 general
  set-union/DomainConflict 物理律，Case 语义归 Casebook。
- **`crash-reconciliation`**：崩溃后重入普通程序；本包只管 replica 之间的事实交换。
- 各 domain 的 resolution 语义（`*Resolved` 的具体业务含义）→ 各 domain owner。
