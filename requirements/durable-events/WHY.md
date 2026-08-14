# WHY —— durable-events

## 一句话

**动态 durable state 只能有一个解释权。** 事实=event、修改=append、恢复=fold、
查询=projection、原子发布=Git ref CAS。没有第二个 journal、第二套 blob、私有 ref 或
私有 merge 协议。

## 为什么必须独立存在

`archive/docs/why/persist.md` 与 `archive/changes/completed/storage.md` 反复确认同一个失败模式：
**每个 feature 自造一份 durable 机制，崩溃恢复与多进程合并立刻分裂成多个世界。**

一旦「谁先写盘、何时算提交、坏了怎么办、多进程怎么合并」由每个 feature 各答一遍，
以下每个问题都会得到互相矛盾的答案：

- 崩溃后重放：重放什么？按什么顺序？
- 多进程同时 append：谁是 authority？要不要 leader？
- 损坏检测：坏了一个对象，跳过继续还是整体拒载？
- 同步：remote 怎么合并？按时间戳猜赢家吗？

统一 substrate 把这些问题**一次回答、所有人共用**，且回答本身可被机械证明
（canonical bytes、CAS 语义、fold 顺序都是纯函数）。

## 三个不可退让的支柱

1. **Event 是唯一 durable truth。** 任何业务状态只以 event 表达；projection 是派生态，
   不是第二真相源。禁止先改投影、以后补 event（storage.md §2；PERSIST-008）。
2. **Append Only。** committed event 永远不可修改、覆盖、删除、原地升级、重新解释。
   错误用**新事实**纠正（`CaseCaptured → CaseRefreshed`、`PromptRequested → PromptRejected`）。
   禁止打开旧 JSON 改 status（storage.md §3）。
3. **原子发布 = CAS。** `CompareAndSwapRef(refs/wanxiang/store, expected, new)` 是唯一提交
   原语；Absent 首次发布也是同一 CAS。不存在部分写入的权威历史——one event = one
   immutable blob，半条 NDJSON 进不了 canonical root（storage.md §4/§9）。

## 失败模式（RED 长什么样）

- 某个 feature 发明 `refs/wanxiang/<feature>` 或自己的 `foo.db` → 出现第二个解释权，
  多进程无法共享 merge/CAS，恢复路径按 feature 分裂。**历史证据**：Casebook 原设计
  `refs/wanxiang/inspector-casebook`、Student QA 私有文件——都被 storage.md §31/§26
  明确禁止；`scripts/checks/unified-store-gate.mjs` 的 `feature-ref` 规则现在钉死它。
- 先改内存再补盘：内存会看见**无证据的未来**；崩溃后重放与内存分歧进不了恢复路径
  （archive/docs/why/persist.md「先改内存再补盘 vs append 成功后才改权威态」）。
- 同 EventId + 不同 bytes 被静默接受 → 重放身份漂移。
- 坏 JSON / 缺 parent / 成环 / 未知 event_type 被跳过继续 fold → 后续事实建在错基上。
- schemaVersion / store-v2：版本不是领域事实，逼出永久 migration mode。
- 全历史扫描当查询 → 把「查询」变成「重放成本」。

## 被拒方案（考古）

| 方案 | 为什么拒 |
|---|---|
| feature 自有 journal/blob/ref | 无法共享一套 merge/CAS；恢复按 feature 分裂（PERSIST-005/006） |
| 内存先记账、后补盘 | 崩溃窗口重复/丢失事实；append 成功后才应 fold 权威态（PERSIST-002/003） |
| schemaVersion / store-v2 | 版本不是领域事实；消灭的是 storage-version compatibility，不是 historical-event compatibility（PERSIST-001/005，storage.md §5.2） |
| 全历史扫描查询 | 查询变成重放成本；投影是积分状态（PERSIST-008） |
| 自增 ordinal 命名 event 路径 | 多进程序号撞车；同集合不同分组得不同 root（storage.md §4） |
| 内容寻址之外的第二套 blob（RuntimePath/blob、SHA256 path 约定） | 重放漂身份；Git object id 即物理 identity（PERSIST-007） |
| CommitUnknown 永久无法确定 | Git canonical root 本身就是 commit outcome 的 durable witness（storage.md §9） |
| 旧 NDJSON / blobs / Student QA 迁移或双写 | clean-break：leave-unread；legacy reader 只允许存在于 one-shot migration tool（PERSIST-005，storage.md §28） |

## 与相邻包的边界（谁不归我）

- **`durable-convergence`**：并发 fork 后 replicas 如何按对象语义收敛、DomainConflict
  如何表达与裁决。本包只保证「set union 后 fold 是确定性的」，不裁决领域冲突。
- **`effect-accounting`**：外部效果的 Requested/Unknown/Accepted 语义。本包只保证
  「append 成功与否可机械判定」，不规定「未知结局意味着什么」。
- **`crash-reconciliation`**：进程中断后如何重入普通程序。本包保证「重放的原料是
  durable facts」，不规定恢复流程。
- 各 domain event 的业务意义 → 各 domain owner（Todo/Context/Review/Casebook/Strength）。
