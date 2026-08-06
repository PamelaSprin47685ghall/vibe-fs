# Orchestrator — 目标实现

## ORCH-004：主程序

直接 CE，禁止「回到创建 Job 入口」的递归：

```text
runManagerJob:
  use worktree
  → create Manager（持久 Agent 名）
  → run guarded Manager → candidate
  → rebaseReviewPublishLoop(job, candidate)

rebaseReviewPublishLoop:
  read target head T
  → rebase candidate onto T
  → post-rebase dual PERFECT（同 worktree / 同 Manager）
  → acquire short Integration Gate
  → re-read head
       still T → ff-only + 写 Published
       changed → release lock，再进 loop
```

冲突递归只能发生在 `rebaseReviewPublishLoop` 内。

## ORCH-006：持久事实

```fsharp
ManagerJobCreated =
    { ManagerJobId; ManagerSessionId
      ManagerAgent              // "fast-manager" | "deep-manager"
      WorktreeIdentity          // 稳定身份，非可变路径
      WorktreePath              // 诊断用；恢复按 Identity 定位
      TargetRef
      TargetBranchFrozen }

CandidateReady =
    { ManagerJobId; CandidateCommit; PreRebaseReviewWitnessId }

RebasedCandidateReady =
    { ManagerJobId; RebasedCommit; TargetHeadSnapshot
      PostRebaseReviewWitnessId }

ConflictDetected =
    { ManagerJobId; CandidateCommit; TargetHeadSnapshot
      ConflictFiles; DiagnosticsDigest }

PublishClaimed = { ManagerJobId; TargetRef; ExpectedHead }
Published = { ManagerJobId; CandidateCommit; ResultingTargetHead }
JobFailed = { ManagerJobId; Reason }
JobAbandoned = { ManagerJobId }
```

Witness ID 必须指向已持久化 `ConfirmedReviewWitness`。  
`ConflictDetected` 为恢复所必需：否则无法区分「正在解决冲突」与「尚未产出 candidate」。

禁止：省略 `ManagerAgent`；Barrier 事实省略 witness ID；用「CandidateCreated 然后随便走」这种无确定恢复动作的分支。

## ORCH-007：恢复

Fold 取每个活跃 Job 的最后事实，决定**唯一**恢复动作。

```text
Published / JobAbandoned → 清理 worktree，移出活跃 Map
JobFailed                → 清理 worktree，明确失败
无事实                   → Job 不存在
```

### PublishClaimed（固定三分支，顺序不可换）

```text
currentHead = GetTargetHead(TargetRef)     // 失败 → fail closed
rebasedCommit = 最后 RebasedCandidateReady.RebasedCommit

1. currentHead = rebasedCommit
       → ff 已完成，补写 Published（幂等）

2. currentHead = ExpectedHead
       → 从未 ff；短 gate + 再确认 head → ff-only → Published

3. 其它
       → claim 过期；丢弃旧 post-rebase witness
       → 回 rebaseReviewPublishLoop
```

### 其它事实

```text
RebasedCandidateReady → 进 CAS：head 仍为 snapshot 则 ff，否则重 rebase+review
ConflictDetected      → 同 worktree/同 Manager 恢复冲突解决
CandidateReady        → 进 rebaseReviewPublishLoop
ManagerJobCreated     → 从 worktree 恢复同一 Manager 继续
```

禁止：恢复时新建 worktree 或换 Manager；跳过 post-rebase review；用文件系统状态代替事实。

## Target ref 实现注记（归属 ORCH-008）

定义在 `what/orchestrator.md`。实现要点：

```text
fork 时：git symbolic-ref 冻结 TargetBranchFrozen
GetTargetHead 失败 → fail closed（不 fallback HEAD）
git merge --ff-only：branch == frozen ∧ head == expected
```
