# WHY —— durable-events

## 一句话

**动态 durable state 只能有一个解释权。** 事实=event、修改=append、恢复/在线更新=同一个
Canonical Integrator CE、查询=Current。运行时真相是 `.git` 内 process-local 裸 NDJSON；Git blob
只在 remote sync 边界出现。没有第二个 journal、第二套 history reader、私有 ref 或私有 merge 协议。

## 为什么必须独立存在

历史 why/persist 与历史 change（storage）反复确认同一个失败模式：
**每个 feature 自造一份 durable 机制，崩溃恢复与多进程合并立刻分裂成多个世界。**

一旦「谁先写盘、何时算提交、坏了怎么办、多进程怎么合并」由每个 feature 各答一遍，
以下每个问题都会得到互相矛盾的答案：

- 崩溃后重放：重放什么？按什么顺序？
- 多进程同时 append：谁是 authority？要不要 leader？
- 损坏检测：坏了一个对象，跳过继续还是整体拒载？
- 同步：remote 怎么合并？按时间戳猜赢家吗？

统一 substrate 把这些问题**一次回答、所有人共用**，且回答本身可被机械证明
（canonical bytes、single-writer append、k-way merge、Integrator rule 都是确定性的）。

## 三个不可退让的支柱

1. **Event 是唯一 durable truth。** 任何业务状态只以 event 表达；projection 是派生态，
   不是第二真相源。禁止先改投影、以后补 event（storage.md §2；PERSIST-008）。
2. **Append Only。** committed event 永远不可修改、覆盖、删除、原地升级、重新解释。
   错误用**新事实**纠正（`CaseCaptured → CaseRefreshed`、`PromptRequested → PromptRejected`）。
   禁止打开旧 JSON 改 status（storage.md §3）。
3. **Local commit = 完整 NDJSON 行。** 每个 process 只追加自己的 `.git/wanxiang/events/<WriterId>.ndjson`；
   一条 canonical `JSON+LF` 完整落盘才算 committed。本地 append 不创建 Git object/tree/ref，不做 CAS。
4. **Process = Writer；文件不切片。** 一个 process 一个全局唯一 WriterId，一个文件从进程开始长到进程退出，
   就该多大多大。新进程开新 writer；machine identity 不进入事件模型。
5. **只有一个 Integrator。** 历史 k-way merge 与 Current 积分只由一个 F# CE Integrator 执行；
   Journal/Casebook/Strength/Job/JsTransaction 只注册单-event rule，禁止自行 load history/project。
6. **Git = remote sync 编码。** 只有用户 Git remote 操作触发同步；每个完整 writer 文件编码成一个 blob，
   不切 chunk、不建 index、不设计 delta。Git 自己的 pack/delta 属 Git 内部实现。

## 失败模式（RED 长什么样）

- 某个 feature 发明 `refs/wanxiang/<feature>` 或自己的 `foo.db` → 出现第二个解释权，
  多进程无法共享 merge/CAS，恢复路径按 feature 分裂。**历史证据**：Casebook 原设计
  `refs/wanxiang/inspector-casebook`、Student QA 私有文件——都被 storage.md §31/§26
  明确禁止；`scripts/checks/unified-store-gate.mjs` 的 `feature-ref` 规则现在钉死它。
- 先改内存再补盘：内存会看见**无证据的未来**；崩溃后重放与内存分歧进不了恢复路径
  （历史 why/persist「先改内存再补盘 vs append 成功后才改权威态」）。
- 同 EventId + 不同 bytes 被静默接受 → 重放身份漂移。
- 坏 JSON / 缺 parent / 成环 / 未知 event_type 被跳过继续 fold → 后续事实建在错基上。
- schemaVersion / store-v2：版本不是领域事实，逼出永久 migration mode。
- 业务 helper 自己 load/filter/fold 历史 → 出现第二积分器，恢复与在线语义会漂移。
- 全历史扫描当查询 → 把「查询」变成「重放成本」。
- 每 fact 都写 Git blob/tree/ref，或把 writer 文件切 segment/index → 把普通 append 放大成 O(history/shards)
  的 object rewrite；这不是“Git 太慢”，而是把 Git 错当在线数据库。

## 被拒方案（考古）

| 方案 | 为什么拒 |
|---|---|
| feature 自有 journal/blob/ref/history reader | 无法共享唯一 k-way merge/Integrator；恢复按 feature 分裂（PERSIST-005/006） |
| 内存先记账、后补盘 | 崩溃窗口重复/丢失事实；append 成功后才应 fold 权威态（PERSIST-002/003） |
| schemaVersion / store-v2 | 版本不是领域事实；消灭的是 storage-version compatibility，不是 historical-event compatibility（PERSIST-001/005，storage.md §5.2） |
| 全历史扫描查询 | 查询变成重放成本；投影是积分状态（PERSIST-008） |
| segment/ordinal 切 writer log | 没有产品语义，只增加 rotation/index/tail rewrite；一个 process 一个文件更简单 |
| EventId→Git blob index | online Git store 的补丁，会随 tail OID 变化产生写放大；本地 Current 已承担查询 |
| CommitUnknown 永久无法确定 | 本地 canonical EventId+bytes 是否存在就是 durable witness；remote ref 不参与本地 commit |
| pre-unified `.git/wanxiangshu-next` / blobs / Student QA 迁移或双写 | clean-break：leave-unread；它们不重新成为 runtime authority |
| EventStore 自己的 `events/<hex>/<EventId>.jsonl` 兼容读取/迁移 | 休克切换：只识别 root shape，不读 event bodies；CAS 到新空 universal-log root。旧 history 明确丢弃，避免把一次错误物理选择变成永久兼容负担 |

## 与相邻包的边界（谁不归我）

- **`durable-convergence`**：多个 WriterId 有序流如何 k-way merge、remote Git 操作时如何替换本地+远端
  snapshot、DomainConflict 如何表达与裁决。本包只保证 Integrator 消费 canonical merge 输出。
- **`effect-accounting`**：外部效果的 Requested/Unknown/Accepted 语义。本包只保证
  「append 成功与否可机械判定」，不规定「未知结局意味着什么」。
- **`crash-reconciliation`**：进程中断后如何重入普通程序。本包保证「重放的原料是
  durable facts」，不规定恢复流程。
- 各 domain event 的业务意义 → 各 domain owner（Todo/Context/Review/Casebook/Strength）。
