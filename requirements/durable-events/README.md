# durable-events

> durable truth = append-only EventEnvelope history；Current = 唯一 canonical Integrator 的可丢弃积分结果。

## 当前模型

```text
runtime process
  → .git/wanxiang/events/<WriterId>.ndjson    // one process = one unbounded file
  → CanonicalIntegrator CE                    // only history enumerator
       register StructuralIntegration.rule
       register JournalIntegration.rule
       register StrengthIntegration.rule
       register CasebookIntegration.rule
       register JsTransactionIntegration.rule
  → Current

large material
  → .git/wanxiang/payloads/<PayloadRef>

Wanxiangshu/OpenCode startup
  → HookDispatcher.ensure(reference-transaction, pre-push, remote store fetch-refspec)
  → stop; product process never owns fetch/pull/push

later user Git process, Wanxiangshu may be absent
  → installed hook → resources/git/wanxiang-hook.mjs → HookSync
  → local writer files + remote writer blobs
  → EventKWayMerge
  → each complete WriterId.ndjson = exactly one Git blob
  → full bidirectional local/remote convergence
```

本地 append 路径没有 Git object/tree/ref、没有 segment/chunk、没有 EventId→blob index、没有 CAS retry。
Git ODB 是 remote-sync 编码/transport，不是在线数据库。Git 自己内部 pack/delta 不属于 Wanxiangshu 协议。

旧 `.git/wanxiangshu-next`、旧 RuntimePath blob/journal、旧 one-event-per-blob root、以及
`logs/<ReplicaId>/<segment>.ndjson + index/` online-Git EventStore 全部 **shock cut**：不读、不迁、不 reset、
不双写。旧实现文件已从 F# 编译图移除并标 `GARBAGE`。

## 文档

```text
WHY.md    动机与被拒方案
WHAT.md   normative：DURABLE-EVENTS-001..019
HOW.md    当前实现模型
PROOF.md  命题 → executable oracle；本轮新增/改写测试 FROZEN 未执行
```

## 核心文件

| 概念 | 文件 |
|---|---|
| Domain envelope | `Domain/EventStore.fs` |
| canonical bytes / identity | `Infrastructure/Persist/CanonicalEventCodec.fs` |
| authoritative vocabulary | `Infrastructure/Persist/EventVocabulary.fs` |
| local writer / payload truth | `Infrastructure/Persist/ProcessEventLog.fs` |
| one k-way primitive | `Infrastructure/Persist/EventKWayMerge.fs` |
| Integrator rule contract / structural frontier | `Infrastructure/Persist/IntegrationKernel.fs` |
| local append boundary | `Infrastructure/Persist/EventStore.fs` |
| sole history integrator CE | `Infrastructure/Persist/CanonicalIntegrator.fs` |
| Git sync encoding | `Infrastructure/Persist/WriterStreamSync.fs` |
| hook-process Git transport | `Infrastructure/Git/GitGateway.fs` |
| standalone hook entry | `Infrastructure/Git/HookSync.fs` + `resources/git/wanxiang-hook.mjs` |
| startup ensure | `Infrastructure/Git/HookDispatcher.fs` / `OpenCode/Plugin/PluginBoot.fs` |
| Journal adapter | `Persistence/Journal/EventStoreJournalWriter.fs` / `Persistence/Journal/AgentJournal.fs` |

## 关键红线

- 只有 `CanonicalIntegrator` 可枚举 event history；business modules 只注册 single-event oracle 并读 Current。
- boot replay 与 live append 都进入同一个 `integrateOne` program。
- structural frontier/DomainConflict 也是 Integrator 的 registered Current，不另造 projector/state machine。
- local append 成功与 remote 是否在线无关。
- startup 只 ensure hooks/refspec；同步必须能在 OpenCode/Wanxiangshu 不运行时由用户 Git hook 独立完成。
- `reference-transaction` 与 `pre-push` 都是 full bidirectional convergence；区别只在 initial remote root discovery。

## Proof（解冻后）

```bash
node --test requirements/durable-events/tests/local-process-event-log.test.mjs
node --test requirements/durable-events/tests/canonical-integrator.test.mjs
node --test requirements/durable-events/tests/event-store-append.test.mjs
node --test requirements/durable-events/tests/event-store-journal-boot.test.mjs
node --test requirements/durable-events/tests/event-store-journal-writer.test.mjs
node --test requirements/durable-events/tests/hook-dispatcher.test.mjs
node --test requirements/durable-events/tests/integration/persist/leave-unread.test.mjs
```

本轮按用户要求**未执行这些测试，也未执行 build**。
