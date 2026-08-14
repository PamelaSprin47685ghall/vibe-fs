# durable-convergence

> 多个各自合法发展的 durable replicas 必须按**对象语义收敛**，而不是靠 wall-clock/LWW
> 猜赢家；否则同步会静默丢掉合法分支，相同 object set 在不同 replica 上折叠成不同世界。

## 这是什么包

`durable-convergence` 拥有「多个 replica 之间的事实交换与收敛」语义。它规定：

```text
merge        = append-only set union + identity dedupe（永不丢事实）
并发 fork    = DomainConflict，不是 StorageInvalid（永不把 store 打成不可恢复）
收敛         = Projection(Fold(Union(Events)))，不是 Merge(Projection1, Projection2)
同步         = 永远双向 Converge（fetch → merge → validate → CAS local → lease push）
dumb remote  = 只交换 objects/refs/CAS/auth，不拥有 domain policy
禁制         = wall_clock / revision LWW、单向 Pull/Push、server-side reducer
```

物理 substrate（单 store 的 append/CAS/canonical identity/fold 机制）由
`durable-events` 提供；本包只回答「多个 store 之间发生什么」。

```text
README.md   ← 你在这里
WHY.md      为什么不能靠时间戳猜赢家（被拒方案考古）
WHAT.md     唯一 normative 合同：9 条命题（DURABLE-CONVERGENCE-001..009）
HOW.md      实现模型：k-way merge、DomainConflict、Converge、dumb server；历史与弃权
PROOF.md    每条命题的测试落点
tests/      本包拥有的可执行 proof（1 个 NEW 文件，5 断言）
```

## WHAT 概览（按命题组）

- **merge 律**（001–003）：set union 永不丢事实；k-way merge 满足
  associative/commutative/idempotent/deterministic；生产 structural merge ≡ union oracle。
- **并发分叉**（004–005）：合法 fork → DomainConflict（非 StorageInvalid），以全部 heads
  为 parents 的 resolution event 收敛。
- **无 LWW**（006）：merge 是 event 集合的函数，与 wall-clock/到达顺序无关。
- **确定性**（007）：相同 merged snapshot 折叠为相同 projection。
- **同步**（008–009）：永远双向 Converge；dumb remote 无 domain 逻辑。

## HOW 概览

```text
EventStoreMergeSpec（oracle）：mergeEvents = set union + identity dedupe
EventStoreMerge（生产）：structural tree merge —— EventId 分片路径直接 union，
        同 EventId 异 OID 才读 bytes 校验 IdentityCollision
ConvergeStore(remote)：fetch store ref → merge → EventStoreFold.validate →
        PayloadClosure.validatePresent → CAS local → lease push（永远双向）
HookDispatcher：reference-transaction / pre-push shim + WANXIANG_GIT_SYNC_ACTIVE guard
```

核心文件（精确到符号）：

| 概念 | 文件 |
|---|---|
| merge oracle + production | `src/Wanxiangshu/Infrastructure/Persist/EventStoreMerge.fs`（`EventStoreMergeSpec.mergeEvents` / `EventStoreMerge.merge`） |
| DomainConflict 类型 | `Infrastructure/Persist/StoreTypes.fs`（`DomainConflict.ConcurrentHeads`） |
| 确定性 fold 的冲突表达 | `Infrastructure/Persist/EventStoreFold.fs`（`StreamHeadState` / `applyStream` / `isResolution`） |
| Converge / 双向同步 | `Infrastructure/Persist/EventStore.fs`（`IEventStore.Converge`）+ `Infrastructure/Git/GitGateway.fs`（`convergeLoop` / `leasePush` / `fetchStoreRef`） |
| hook 收敛注入 | `Infrastructure/Git/HookDispatcher.fs` |

## proof 概览

```bash
node --test requirements/durable-convergence/tests/replica-merge-laws.test.mjs
# 物理律的既有证明（REUSE，留在原处）：
node --test tests/unit/persist/event-store-merge.test.mjs
node --test tests/unit/persist/event-store-converge.test.mjs
node --test tests/integration/persist/dumb-server.test.mjs
```

## DEPENDS ON `durable-events`

merge/fold 的 canonical identity、CAS、确定性 fold 机制由 `durable-events` 提供，
本包在其上定义 replica 之间的律。

## 边界（DOES NOT OWN）

- 单一 store 的 append/CAS/identity/fail-closed → `durable-events`。
- Casebook/Orchestrator 等 domain-specific 的冲突裁决语义 → 各 domain owner
  （Casebook 对象语义 → `knowledge-reuse`）。
- network transport / provider → `host-boundary` / 具体 transport 实现。
- wall-clock 本身的时间能力 → `time-capability`。
