# HOW —— durable-convergence（实现模型与约束）

> 本文件**非 normative**。行为合同在 `WHAT.md`；本文件回答「代码在哪里、怎么工作」，
> 并收纳历史与弃权裁决。

## 核心机制（逐概念）

### 1. K-way merge（`Infrastructure/Persist/EventStoreMerge.fs`）

```text
EventStoreMergeSpec（契约 oracle，测试对照）：
  mergeEvents = mergeByIdentity（set union + identity dedupe；同 id 异 bytes → IdentityCollision）
  merge(store, MergeInput) = 逐 snapshot loadEventEnvelopes → mergeEvents
EventStoreMerge（生产）：
  collisionAt：同 EventId 路径 blob OID 不同 → IdentityCollision
  mergeEntryLists：identical bytes → 复用 OID；events/ 下异 bytes → IdentityCollision；
                   payloads/ 等其它路径冲突 → NonCanonical；missing → MalformedEnvelope
  merge：[] → empty；[only] → as-is；many → K-way（结构性 union，非 O(N) 全量反序列化）
```

### 2. DomainConflict 表达（`Infrastructure/Persist/EventStoreFold.fs` + `StoreTypes.fs`）

```text
StoreTypes.DomainConflict = ConcurrentHeads of streamId * heads（RequireQualifiedAccess）
EventStoreFold.StreamHeadState = Empty | Unique of head | Conflict of DomainConflict
applyStream：新 event 与既有 head 并发（非其后继）→ Conflict；
isResolution：event_type EndsWith "ConflictResolved" | "Resolved"
resolution 覆盖全部 prior heads → 收敛为单一 head；仅当 resolution + 全部 parents 已 fold
projection 才离开 conflict state
fold：validateVocabulary → validateParents → topologicalOrder（Kahn + EventId tie-break）
      → applyStream → { Streams; FoldOrder; Conflicts }
```

### 3. Converge 永远双向（`Infrastructure/Git/GitGateway.fs` + `Infrastructure/Persist/EventStore.fs`）

```text
IEventStore.Converge(remote) → 经绑定的 GitGateway
convergeLoop：
  1. fetchStoreRef：git fetch <remote> +refs/wanxiang/store:refs/wanxiang/remotes/<remote>/store
     （缺 remote ref → Ok Absent lease）
  2. EventStoreMerge.merge [local; remoteSnap]
  3. validateMerged：EventStoreMergeSpec.merge → EventStoreFold.validate → PayloadClosure.validatePresent
  4. CAS local canonical ref
  5. leasePush：git push --force-with-lease=refs/wanxiang/store:<expected> <remote> <oid>:refs/wanxiang/store
  race → 重新 fetch → loop（ConvergeCasRejected / ConvergeRetryExhausted at exhaustion）
convergeStoreWithObservedRemote：hook 路径复用已 fetch 的 observedRemoteSnapshot，不重复 fetch
SyncActiveEnv = "WANXIANG_GIT_SYNC_ACTIVE" + syncActiveDepth：嵌套 ConvergeStore 直接返回 local snapshot
```

### 4. Dumb remote 与 hook（`Infrastructure/Git/HookDispatcher.fs`）

```text
remote = 普通 bare Git repo：objects / refs / fetch / push / lease(CAS) / auth
HookDispatcher：reference-transaction（store remote-tracking ref 变化 → 触发收敛）+
                pre-push shim（fetch remote store → merge → CAS local → lease push → 继续原 push）
安装规则：不覆盖/不删除用户 hook、不改写无法证明 ownership 的 hook、安全 chain/dispatcher、
         无法安全集成时明确诊断 Git integration incomplete（不得静默降级）
递归 guard：WANXIANG_GIT_SYNC_ACTIVE=1 → 内部 Git 操作不再触发收敛
```

## 边界与相关实现

- single-store 的 append/CAS/canonical identity/fold 机制全部在 `durable-events`
  （`CanonicalEventCodec`/`EventStore`/`GitRawStore`/`StoreTypes`/`EventStoreFold`）。
- `Converge` 的 transport 失败（offline/auth/lease contention）由 `GitGateway` 的
  `GitError = Transport | Failed` 表达；物理能力合同归 `host-boundary`。
- Casebook 的 `revision/wall_clock deterministic tie` 只允许在 **CasebookProjection**
  从完整 history 派生 `CurrentCase(session)`，不删 loser event（storage.md §10.10）；
  对象语义归 `knowledge-reuse`。

## 历史与弃权

1. **Casebook 原 `refs/wanxiang/inspector-casebook` + revision/wall_clock LWW —— 弃权**：
   storage.md §10.10/§31 把它重定位为「LWW = Casebook projection rule，不是 Persist
   replication rule」；feature-owned ref 已被 unified-store-gate 钉死。Case 对象语义
   归 `knowledge-reuse`。
2. **Pull/Push/Download/Upload 单向模式 —— 弃权**：storage.md §17 永久禁止；只存在
   `ConvergeStore` 一种应用可见同步语义。
3. **multi-remote CRDT —— 弃权（future work）**：storage.md §22 首版一个 remote 一个
   同步算法；多 remote、Repository Identity + Session Portability（§42.6）由未来
   Proposal 定义，当前不承诺。
4. **Process Registry / MergeStateMachine —— 弃权**：storage.md §10.7/10.8 禁止；
   并发知识只通过 snapshot/root/CAS/remote-tracking 显现。
5. **「非法 fork → fail closed」的旧解释 —— 弃权**：storage.md §5.3 Amendment 把
   Storage 层永不因自然 fork 不可恢复；「forbidden fork」= DomainConflict 业务不可
   接受态，由 projection + resolution 收敛。
6. **跨机器不引入强一致性 —— HOW**：§42.1 best-effort distributed extension：无
   global transaction/linearizability/distributed lock/leader；网络分区两边都可 append，
   重连后 k-way merge eventually convergence；机器无持久身份语义（§42.2）。
