# change-integration — 实现模型与约束

非 normative。WHAT 是唯一权威；本文件解释实现模型与历史裁决。

## 实现模型

### 主程序（`Application/Orchestration/Program.fs`，ORCH-004）

```text
runManagerJob:
  use worktree
  → create Manager（持久 Agent 名；Persona 已绑）
  → run guarded Manager → candidate
  → rebaseReviewPublishLoop(job, candidate)

rebaseReviewPublishLoop:
  read target head T
  → rebase candidate onto T
  → post-rebase dual PERFECT（同 worktree / 同 Manager；judge 工具）
  → acquire short Integration Gate
  → re-read head
       still T → ff-only + 写 Published
       changed → release lock，再进 loop
```

冲突递归只能发生在 `rebaseReviewPublishLoop` 内；Integration Gate 只覆盖 ref mutation 窗口，
不在 LLM Review / 冲突修复期间持有（ORCH-005）。

### 持久事实（`Journal/OrchestratorFactFold.fs` / `OrchestratorProjection.fs`，ORCH-006）

```fsharp
ManagerJobCreated = { ManagerJobId; ManagerSessionId; ManagerAgent
                      WorktreeIdentity; WorktreePath; TargetRef; TargetBranchFrozen }
CandidateReady / RebasedCandidateReady（含 TargetHeadSnapshot + PostRebaseReviewWitnessId）
ConflictDetected = { ManagerJobId; CandidateCommit; TargetHeadSnapshot; ConflictFiles; DiagnosticsDigest }
PublishClaimed = { ManagerJobId; TargetRef; ExpectedHead }
Published / JobFailed / JobAbandoned
```

Witness ID 必须指向已持久化 `ConfirmedReviewWitness`；`ConflictDetected` 是恢复所必需（区分「正在解决
冲突」与「尚未产出 candidate」）。

### 恢复（ORCH-007）

Fold 取每个活跃 Job 的最后事实，决定**唯一**恢复动作；PublishClaimed 三分支固定顺序不可换：

```text
1. currentHead = rebasedCommit   → ff 已完成，补写 Published（幂等）
2. currentHead = ExpectedHead    → 从未 ff；短 gate + 再确认 head → ff-only → Published
3. 其它                          → claim 过期；丢弃旧 post-rebase witness；回 rebaseReviewPublishLoop
```

禁止：恢复时新建 worktree 或换 Manager；跳过 post-rebase review；用文件系统状态代替事实。

### 原子发布（`Infrastructure/Git/`）

- `IntegrationGate.fs`：proper-lockfile 短锁；`lockPath(repo, branch)` 稳定；acquire/release/dispose
  幂等；跨实例互斥（`tests/integration-gate.test.mjs`）。
- `GitOperations.fs`：typed Git 动词——`freezeTargetBranch`（symbolic-ref；detached/blank → refuse）、
  `isDirty`（porcelain 非空）、`rebase`/`conflictedFiles`/`hasRebaseHead`、`readHead`/`getTargetHead`
  （空 → missing）、`ffMerge`（ORCH-008 CAS 梯：branch==frozen ∧ head==expected ∧ ff-only）。
- `WorktreeResource.fs`：owned worktree（identity 定位，path 仅诊断）；create/release/adopt/durable 生命周期。
- `HookDispatcher.fs`：store sync 的 pre-push / reference-transaction hooks（**归属 `durable-events`**，
  见 SPLIT@cutover）。

### 编排运行时（`Application/Orchestration/Runtime.fs`）

`forkManager`/`join`/`resumeManager`/`reverify`：通过 GitPort/ManagerPort 缝驱动真实引擎；
`JoinPublished`/`NeedsReview` 结果分型；`WorktreeCreateRequested → WorktreeCreated → ManagerJobCreated`
事实顺序（effect-accounting 消费）。

## 物理落点（CURRENT EVIDENCE）

- 类型/wiring：`Infrastructure/Git/{IntegrationGate,GitGateway,WorktreeResource,HookDispatcher,GitOperations,GitSubject}.fs`。
- Fact：`Journal/{OrchestratorProjection,OrchestratorFactFold}.fs`。
- Failure：PublishClaimed 三分支 CAS、`Application/Reconciliation`（restart reconcile）。
- Tests：包内 4 文件（MOVE）；REUSE 清单见 PROOF.md。

## 边界与弃权（非 normative）

- **GARBAGE——`fork-manager`/`list` 旧面**：Orchestrator 旧工具名（`agent=fast-manager|job_id` +
  worktree + `reused=true`）已 clean-break 删除，无 alias（`archive/docs/why/orchestrator.md` 备选节）。
- **GARBAGE——`Steward`**：`proposals/Steward.md` 明确「不在本轮创建」（orchestrator why）；
  不进入未来 WHAT。
- **HOW——机制常数**：`MaxJoinBatch=32`（join 批次）、`DevOpsJoinTimeoutMs=10_000`（join 等待预算）、
  gate 锁重试参数（proper-lockfile 50×≤500ms）——有界性与原子性才是 WHAT。
- **HOW——Git 命令序列**：具体 git 子命令/参数序列（rebase 参数、porcelain 解析、lockfile 库）可换；
  发布语义不动。
- **HOW——恢复动作选择表**：事实 → 动作的映射表是当前实现形状；「唯一动作、穷尽分支」才是 WHAT。
- **归属他包**：Requested/Accepted/Published 效果记账 → `effect-accounting`；store ref 的 hooks 与
  持久化 → `durable-events`；崩溃后重入 → `crash-reconciliation`；post-rebase witness 的有效性规则 →
  `review-assurance`；道路语义 → `delegation`。
