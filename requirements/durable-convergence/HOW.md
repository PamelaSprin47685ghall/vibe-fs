# HOW —— durable-convergence（实现模型与约束）

> 本文件**非 normative**。行为合同在 `WHAT.md`；本文件回答「代码在哪里、怎么工作」，
> 并收纳历史与弃权裁决。

## 核心机制（逐概念）

### 1. K-way merge（`Infrastructure/Persist/CanonicalIntegrator.fs` / sync support）

```text
inputs:
  .git/wanxiang/events/<WriterId>.ndjson   // one process = one ordered stream
  remote WriterId blob                    // same full-file bytes at remote snapshot

KWayMerge:
  one cursor per writer stream
  compare canonical event sort key
  same EventId + same bytes → dedupe
  same EventId + different bytes → IdentityCollision
  output independent of stream enumeration / process / machine

boot/recovery: KWayMerge(local streams) → CanonicalIntegrator CE
remote sync:   KWayMerge(local + remote streams) → local replacement + remote materialization
ordinary local Append: no global merge; append only own WriterId file then Integrator integrates that one event
```

### 2. DomainConflict / structural frontier（`IntegrationKernel.fs` + `CanonicalIntegrator.fs`）

```text
StructuralProjection.Heads : stream_id → Set<EventId>
StructuralIntegration.rule : accepts every EventEnvelope

apply one event:
  prior = Heads[stream]
  next  = (prior - event.parents) + event.id

|next| = 0  → empty
|next| = 1  → unique structural head
|next| > 1  → DomainConflict frontier（合法 concurrent fork；非 StorageInvalid）

resolution event 只有在 parents 覆盖全部 competing heads 时才把 set 收敛为自己的单 head。
StructuralIntegration 与 Journal/Strength/Casebook/JsTransaction business rules 一样注册进
同一个 CanonicalIntegrator CE；同一 event 可以同时更新 structural slot + 一个 business slot，
不存在第二个 full-history projector。
```

### 3. Remote-only 双向 sync（独立 Git hook 进程）

```text
Wanxiangshu/OpenCode startup:
  ensure reference-transaction + pre-push hooks
  ensure remote Wanxiang store fetch-refspec
  stop — 产品进程不主动 fetch/pull/push

user Git process → hook child process:
  1. obtain remote EventStore root
     - reference-transaction: use the just-observed remote-tracking root
     - pre-push: fetch/read the remote store root
  2. decode each remote writer blob as one full canonical NDJSON stream
  3. read local .git/wanxiang/events/*.ndjson streams
  4. KWayMerge + identity/structural validation + payload closure
  5. replace local synchronized writer-file set
  6. encode each complete synchronized WriterId file as exactly one Git blob
  7. materialize remote root + lease-push
  race → refetch and repeat boundedly

reference-transaction and pre-push are both full bidirectional convergence;
they differ only in how the initial remote root is obtained.
No Integrator/WorkspaceEventStore dependency exists in the hook process.
No timer/background uploader/event-count trigger.
No segment/chunk/index/delta protocol.
WANXIANG_GIT_SYNC_ACTIVE prevents Git operations performed by the hook from recursively entering sync.
```

Hot path:

```text
successful full materialization
  → persist non-authoritative {physical-stat-fingerprint, root-oid}

next hook under the same store lock
  → stat fingerprint only
  → hit + same remote root = reuse root; no writer/payload body reads; no remote blob reads
  → miss/different root = compare per-file cached stat + cached blob OID
      unchanged local file → reuse prior blob OID
      unchanged remote entry → do not read/decompress blob
      changed file only → read/import/blobify that file
    then canonical validate + refresh cache

reference-transaction(observed current root):
  candidate == observed → no push

pre-push:
  expected := local refs/remotes/<remote>/wanxiang/store
  lease-push candidate directly
  lease rejected → fetch/discover current root → full retry
```

cache 从不参与 durable correctness：metadata 不匹配/parse 失败即 miss；完整 path 仍是唯一 validation owner。
这等价于 Git index 的 stat-cache 角色——只证明“可以复用已验证 materialization”，不创造新事实。

### 4. Dumb remote 与 hook（`Infrastructure/Git/HookDispatcher.fs`）

```text
remote = 普通 bare Git repo：objects / refs / fetch / push / lease(CAS) / auth
HookDispatcher：reference-transaction（observed store root → full bidirectional converge）+
                pre-push shim（discover/fetch store root → full bidirectional converge → 继续原 push）
durability activation ensure：plugin Load Phase 零 Git mutation；首个真实 workspace 业务交互才确保 hook + refspec；同步执行本身不要求 Wanxiangshu 正在运行
hook shim：`exec /usr/bin/env node <package>/resources/git/wanxiang-hook.mjs ...`；
          禁止把安装时 `process.execPath`（可能是 opencode.exe/Bun host）固化进 hook，也不要求 runner 文件可执行
安装规则：不覆盖/不删除用户 hook、不改写无法证明 ownership 的 hook、安全 chain/dispatcher、
         无法安全集成时明确诊断 Git integration incomplete（不得静默降级）
递归 guard：WANXIANG_GIT_SYNC_ACTIVE=1 → hook 内部 Git 操作不再触发收敛
```

## 边界与相关实现

- single-store 的 local append/canonical identity/Integrator 机制全部在 `durable-events`
  （`CanonicalEventCodec`/`ProcessEventLog`/`EventStore`/`CanonicalIntegrator`）。Git raw 只在本包 remote sync materialization 使用。
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
4. **Process Registry / MergeStateMachine —— 弃权**：不维护 machine/process registry、lease 或同步状态机；
   WriterId 只作为 single-writer stream identity。进程死后 writer 自然封存，新进程开新 WriterId。
5. **「非法 fork → fail closed」的旧解释 —— 弃权**：storage.md §5.3 Amendment 把
   Storage 层永不因自然 fork 不可恢复；「forbidden fork」= DomainConflict 业务不可
   接受态，由 projection + resolution 收敛。
6. **跨机器不引入强一致性 —— HOW**：§42.1 best-effort distributed extension：无
   global transaction/linearizability/distributed lock/leader；网络分区两边都可 append，
   重连后 k-way merge eventually convergence；机器无持久身份语义（§42.2）。

## 验证与测试落点

> 2026-08-14 shock cut。所有本轮新/改写 oracle 按用户要求 **FROZEN，未执行**。
> Git snapshot merge / product-process `ConvergeStore` 的旧 proof 已废弃；同步宿主现在是独立 Git hook 进程。

### 运行方式（解冻后）

```bash
node --test requirements/durable-convergence/tests/event-store-merge.test.mjs
node --test requirements/durable-convergence/tests/replica-merge-laws.test.mjs
node --test requirements/durable-convergence/tests/writer-stream-sync.test.mjs
node --test requirements/durable-convergence/tests/event-store-converge.test.mjs
node --test requirements/durable-convergence/tests/dumb-remote-no-domain.test.mjs
node --test requirements/durable-convergence/tests/hook-performance-fast-path.test.mjs
node --test requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs
```

### 命题 → 落点

| 命题 | 落点测试 | 类型 |
|---|---|---|
| DURABLE-CONVERGENCE-001 | `tests/event-store-merge.test.mjs::set union never drops distinct events` + `tests/replica-merge-laws.test.mjs::set union never drops concurrent events` | NEW/FROZEN |
| DURABLE-CONVERGENCE-002 | `tests/writer-stream-sync.test.mjs::one k-way primitive is shared by integrator and sync` + `tests/replica-merge-laws.test.mjs::merge is commutative associative idempotent at writer stream level` + `tests/event-store-merge.test.mjs::writer enumeration is commutative` + `tests/event-store-merge.test.mjs::duplicate stream input is idempotent by EventId` | NEW/FROZEN |
| DURABLE-CONVERGENCE-003 | `tests/writer-stream-sync.test.mjs::sync blobifies each complete writer file once without segments or index` + `tests/writer-stream-sync.test.mjs::runtime append and external hook share one physical store gate` + `tests/event-store-merge.test.mjs::identity collision is fail closed not LWW` | NEW/FROZEN |
| DURABLE-CONVERGENCE-004 | `tests/replica-merge-laws.test.mjs::concurrent heads are preserved as structural DomainConflict frontier` | NEW/FROZEN |
| DURABLE-CONVERGENCE-005 | `tests/replica-merge-laws.test.mjs::resolution with all competing heads collapses structural frontier` | NEW/FROZEN |
| DURABLE-CONVERGENCE-006 | `tests/replica-merge-laws.test.mjs::convergence is a function of event truth not arrival wall clock` | NEW/FROZEN |
| DURABLE-CONVERGENCE-007 | `tests/writer-stream-sync.test.mjs::sync does not integrate business history` + `requirements/durable-events/tests/canonical-integrator.test.mjs` | NEW/FROZEN + CROSS/FROZEN |
| DURABLE-CONVERGENCE-008 | `tests/event-store-converge.test.mjs::reference-transaction and pre-push both call the same full bidirectional converge` + `tests/event-store-converge.test.mjs::reference-transaction observed root changes discovery only not sync direction` + `tests/event-store-converge.test.mjs::lease race refetches and repeats the same k-way sync boundedly` + `tests/event-store-converge.test.mjs::product process has no fetch pull push remote API` + `tests/event-store-converge.test.mjs::hook-internal Git commands are recursion guarded and pre-push is not reentered` + `tests/writer-stream-sync.test.mjs::activation only ensures hooks and user Git process runs full sync` | NEW/FROZEN |
| DURABLE-CONVERGENCE-009 | `tests/dumb-remote-no-domain.test.mjs::dumb remote fixture has no Wanxiang domain or server-side logic` + `tests/integration/persist/dumb-server.test.mjs::dumb_remote_helper_has_no_Wanxiang_domain_or_projection_logic` + `tests/integration/persist/dumb-server.test.mjs::pre_push_hook_process_uploads_one_local_writer_file_to_bare_remote_store_ref` + `tests/integration/persist/dumb-server.test.mjs::second_machine_hook_imports_remote_writer_truth_without_any_running_Wanxiang_process` + `tests/integration/persist/dumb-server.test.mjs::two_offline_clients_converge_by_whole_writer_files_and_repeat_is_idempotent` | NEW/FROZEN |
| DURABLE-CONVERGENCE-010 | `tests/hook-performance-fast-path.test.mjs::no-op sync reuses stat-fingerprint materialization instead of rereading durable bytes` + `tests/hook-performance-fast-path.test.mjs::near-equal worst path reads and blobifies only changed files` + `tests/hook-performance-fast-path.test.mjs::pre-push starts from tracking ref and only discovers remote after lease rejection` + `tests/hook-performance-fast-path.test.mjs::confirmed same-root convergence does not publish an empty snapshot` | NEW |

### 统计

- WHAT 命题：10；PROOF 行：10。
- 统一 k-way primitive：`Infrastructure/Persist/EventKWayMerge.fs`，由 `CanonicalIntegrator` 与 `WriterStreamSync` 共同调用。
- remote sync trigger：plugin Load Phase 零 Git mutation；durability activation 才 `HookDispatcher.ensure`；实际执行由 `resources/git/wanxiang-hook.mjs` → `HookSync` 独立进程完成。
- GAP：0。
