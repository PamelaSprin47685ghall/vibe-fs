# managed-session-lifecycle

> 一句话 WHY：只要系统创建 managed session，就必须有唯一 owner 负责创建、复用、停止、回收与
> replacement；否则每个 feature 都会复制 parent map、cancel、retire、restore 规则。

## WHAT 概览

managed session 的整条生命周期只有一个合同：

```text
create（先写 SessionAssociation 再发首个 prompt）
→ reuse（journal 关联 + id/agent/title 精确匹配；ReuseScope key 复用）
→ completion / abandon / retire（Handle 四态，tombstone 不可回退）
→ owner closure（级联 cancel；HostOwnedHidden 对父不可见）
→ restart（按 durable handle 投影恢复；冲突 / 查询失败 fail closed）
→ proven permanent loss 才 Replacement
```

- **AttachedSessionRuntime / SatelliteRuntime / SyncDelegateRuntime / HostForkRuntime** 是各
  managed session 家族的 runtime owner；任何 feature 不得复制 parent map / cancel / retire 框架。
- **ReuseScope**：`(OwnerReuseScopeId, SyncDelegateRole)` → at most one live dedicated Session；
  同 scope 兼容续问复用，不同 scope 不共享。
- **Handle**（`Execution/Delegation/LinkageProjection.fs`）：Active / CompletedAwaitingJoin / Abandoned /
  Retired 四态；completion cell 单赋值；consume 唯一写 retire；Abandoned 与 Retired 不可回退。

## HOW 概览

```text
Session/AttachedSessionRuntime.fs    (ReuseScopeId, role) → binding；GetOrCreate 复用或创建
Session/SatelliteRuntime.fs          Companion leaf：root children → journal 匹配 → Reused|Replacement|Created
Session/HostForkRuntime.fs           fork child：create → HandleLinked → send；reuse 不重新 spawn
Session/ForkRuntime.fs               in-process ChildRun 注册 / mailbox / cancel
Session/HostForkRestart.fs           restoreLinkedChildren：durable handle 投影 → re-enlist
Session/HandleController.fs          HandleLinked/Completed/Abandoned/Retired 唯一 writer
Execution/Delegation/LinkageProjection.fs         HandleProjection 四态 + rejectFalseCompletion + 视图
Execution/Session/Association.fs        关联事实（Work↔Companion；Sync* 走 hints）
```

## proof 概览

- MOVE：`tests/child-run-projection.test.mjs`（ChildRun 生命周期）、
  `tests/distiller-ownership.test.mjs`（EXEC-014 hidden handle）、
  `tests/host-fork-agent.test.mjs`（Fork/Reuse 错误分支与复用合同）。
- NEW：`tests/attached-session-runtime.test.mjs`（AttachedSessionRuntime + ReuseScope）。
- REUSE：`requirements/managed-session-lifecycle/tests/handle.test.mjs`（Handle 四态状态机）、
  `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs`、`host-fork-restart/runtime`、
  `sync-delegate-runtime`、`requirements/managed-session-lifecycle/tests/terminal-policy.test.mjs`、
  `requirements/session-ontology/tests/session-flattening.test.mjs`、`session-ownership-ratchet` 问卷。

## 阅读顺序

1. `WHY.md` → 2. `WHAT.md` → 3. `HOW.md`（含历史与弃权）→ 4. `PROOF.md` → 5. `tests/`。

## DEPENDS ON

- `session-ontology`（执行类 × 归属分类；本包消费其 existence/ownership 事实）。
- `crash-reconciliation`（generic 恢复协议；本包只定义 session-specific 合法恢复结果）。
