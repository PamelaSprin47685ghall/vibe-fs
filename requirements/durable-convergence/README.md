# durable-convergence

> 多个 process/machine 的 durable writer streams 使用同一个 k-way primitive 收敛；Git remote 只是 dumb transport。

## 当前模型

```text
writer A: .git/wanxiang/events/<WriterA>.ndjson
writer B: .git/wanxiang/events/<WriterB>.ndjson
...

EventKWayMerge(writer streams)
  = deterministic causal k-way union
  + same EventId/same bytes dedupe
  + same EventId/different bytes fail closed

CanonicalIntegrator(EventKWayMerge(...)) → Current
```

machine 不进入模型；单机多进程和多机只是 writer stream 来源不同。

## Remote sync

```text
Wanxiangshu startup:
  HookDispatcher.ensure(reference-transaction, pre-push, remote store fetch-refspec)
  // no fetch/pull/push here

later user git fetch/pull:
  reference-transaction hook
  → observed remote store root
  → FULL bidirectional converge

later user git push:
  pre-push hook
  → discover/fetch remote store root
  → SAME FULL bidirectional converge

FULL converge:
  local writer files + remote writer blobs
  → EventKWayMerge / identity + payload validation
  → import remote writer truth locally
  → each complete WriterId.ndjson exactly one Git blob
  → lease-push unified refs/wanxiang/store
```

两种 hook 差别只有 initial remote root discovery。同步执行者是**用户 Git 进程启动的独立 hook 子进程**；
OpenCode/Wanxiangshu 可以完全不在运行。hook runtime 不依赖 `WorkspaceEventStore`、`CanonicalIntegrator` 或 PluginHost。

## 红线

- 不用 wall-clock/revision LWW 删除事实。
- 不造 machine registry / leader / distributed lock / sync state machine。
- 不做 segment/chunk/EventId→blob index/custom delta。
- 不提供 product-process `Fetch/Pull/Push` API。
- remote 是普通 bare/GitHub/GitLab/Gitea-style Git objects/refs/auth，不运行 Wanxiang domain code。
- `reference-transaction` 不是 download-only；它与 `pre-push` 一样完整双向收敛。

## 核心文件

| 概念 | 文件 |
|---|---|
| one k-way primitive | `Infrastructure/Persist/EventKWayMerge.fs` |
| structural frontier / DomainConflict Current | `Infrastructure/Persist/IntegrationKernel.fs` + `CanonicalIntegrator.fs` |
| one writer file ↔ one blob encoding | `Infrastructure/Persist/WriterStreamSync.fs` |
| hook-process Git transport / lease retry | `Infrastructure/Git/GitGateway.fs` |
| independent sync entry | `Infrastructure/Git/HookSync.fs` |
| durability-activation hook/refspec ensure | `Infrastructure/Git/HookDispatcher.fs` |
| packaged runner | `resources/git/wanxiang-hook.mjs` |

## 文档 / proof

`WHAT.md` 定义 DURABLE-CONVERGENCE-001..009；`HOW.md` 描述实现；`PROOF.md` 给 executable landing。
本轮相关测试按用户要求 **FROZEN 未执行**：

```bash
node --test requirements/durable-convergence/tests/event-store-merge.test.mjs
node --test requirements/durable-convergence/tests/replica-merge-laws.test.mjs
node --test requirements/durable-convergence/tests/writer-stream-sync.test.mjs
node --test requirements/durable-convergence/tests/event-store-converge.test.mjs
node --test requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs
```
