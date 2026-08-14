# HOW — managed-session-lifecycle（实现模型与约束；非 normative）

## 实现模型

### Handle 状态机（`src/Wanxiangshu/Journal/LinkageProjection.fs`）

```fsharp
type HandleLifecycle =
    | Active
    | CompletedAwaitingJoin of HandleCompletion     // join 可消费；list 显示 CompletedAwaitingJoin
    | Abandoned of HandleAbandonReason              // durable terminal；不可 join
    | Retired                                       // tombstone；不可回退

type HandleRecord =
    { Handle: HandleId            // = agent id（HandleController.agentHandle）
      ChildSessionId: SessionId   // 只由 Host 签发
      TargetAgent: string
      Byname: string
      CanonicalRole: Role
      Ownership: Fact.HandleOwnership               // DurableParentHandle | HostOwnedHidden
      Lifecycle: HandleLifecycle
      CreationOrder: int
      LastCompletion: HandleCompletion option }
```

`HandleProjection`（纯 fold）：`linkNamed`（重链 live handle 重绑不重复）、`complete`（单赋值，
后到者 `AlreadyCompleted`）、`abandon`（单赋值）、`retire`、`rejectFalseCompletion`（仅
CompletedAwaitingJoin 且 ref+digest 精确匹配才回 Active）、视图（`listable` / `joinable` /
`reportableAbandoned` / `activeHandles`，全部经 `parentVisible` 过滤 HostOwnedHidden）、
`tryFindByByname`（Retired 仍可搜，防名字回收）、`linkedChildren`、`lifecycleSealsBlogger`。

`HandleController`（`src/Wanxiangshu/Session/HandleController.fs`）是四个 fact 的唯一 writer：
`linkNamed / recordCompletion / recordAbandon / retire / consume / cancelChildren`。
`consume` 先读投影再 append `HandleRetired`；CommitUnknown 不交 payload。

### runtimes

- **AttachedSessionRuntime**（`Session/AttachedSessionRuntime.fs`）：`Dictionary<(scope, role),
  (childId, agent)>`，`GetOrCreate` 先查后建；`isUsable` 回调把已删 child 视为 absent（安全侧
  重新创建）；`Remove` / `RemoveByDelegateSession` 是唯一解绑。
- **SatelliteRuntime**（`Session/SatelliteRuntime.fs`）：Companion leaf 的
  `Ensure(owner, spec)` → `start`：查 root children（+ owner children，兼容扁平前）→ 按
  `RestoredSessionId` 精确匹配 → `Reused | Replacement | Created`；`Link` 先于首个 prompt；
  `Retire` → Abort + Close + Invalidate；`Ensure` single-flight（per-kind flight cache）。
- **HostForkRuntime / ForkRuntime**（`Session/{HostForkRuntime,ForkRuntime}.fs`）：fork child 的
  install → HandleLinked（失败则 abort 新 child）→ SendPrompt（失败则 fail pending run）；
  reuse 不 spawn、沿用已绑 agent；`ForkRuntime` 维护 in-process ChildRun 注册 + 双通道 mailbox。
- **HostForkRestart**（`Session/HostForkRestart.fs`）：`restoreLinkedChildren` 按 durable handle
  投影 re-enlist；`restoreLinkedChildrenWithoutRuntime` 是 journal-only walk。

### 复用判据（restart，HOST-009/015）

```text
query family root children（owner ≠ root 时并查 owner children）
→ journal 关联（RestoredSessionId）且 id+agent+title 恰 1 匹配 → 复用
→ journal 关联的 id 不存在 → Replacement（新建，物理挂 root）
→ 无 journal 关联 → 新建（不收养同 agent/title sibling）
→ id 匹配但 agent/title 冲突 / 多候选 / 查询失败 → fail closed
```

## 历史与弃权

- **ReuseScope 概念升级**（universal.md §11）：dedicated key 从 `(owner SessionId, role)` 升级为
  `(OwnerReuseScopeId, role)`；「owner Session 最终 dispose」≠ ReuseScope 终结；只有 scope 被证明
  关闭才 freeze draft → synthesize → publish → retire/release。
- **同 caller ReuseScope 串行**（universal.md §12）：serialization key = immediate caller
  ReuseScope（非 family root，防 DevOps→Coder→Inspector 死锁）——该不变量实现于
  SyncDelegateRuntime（batch mailbox），归 `delegation` 消费，本包只拥有 binding 生命周期。
- **P0-RECOVERY-JOIN-001**：`recordCompletion` 只接受 `JoinableCompletion`，raw Aborted 不能占
  cell；parent cancel 走 durable `HandleAbandoned(ParentCancelled)` 而非 aborted cell
  （`ForkRuntime` 注释）。
- **Student/Teacher**：G3 clean-break 删除（`universal.md`、`ce-student-teacher-collapse.md`）；
  无 Student/Teacher lifecycle 残留（GARBAGE）。
- **cache.md §17（QuiescenceGate）**：idle-derived continuation 资格归 `causal-wait`（process-local
  permit）；本包只消费其「已消费 completion 才 retire」的投递纪律，不拥有 quiescence 语义。
- **explicit 不写 Host 的 session API**：Host 具体 session 接口（ListChildren/CreateChildSession/
  AbortSession/FamilyRootOf）是 `host-boundary` 提供的物理 port；本包通过 `ISessionHostPort` 消费，
  不拥有其 shape。
