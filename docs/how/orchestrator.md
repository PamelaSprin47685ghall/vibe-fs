# Orchestrator — 目标实现

## Implements

行为合同见 `what/orchestrator.md`；本文件只描述 job、rebase-review-publish 和恢复算法。

## Ownership

Manager、worktree、integration gate 和事实 writer 见 `shape/orchestrator.md`。

---

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

---

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

---

## 恢复编排机制（行为见 what/orchestrator.md ORCH-007）

行为（唯一恢复动作、PublishClaimed 固定三分支顺序不可换、崩溃恢复依赖 Journal 事实折叠、禁止用文件系统状态反推进度）权威定义见 `what/orchestrator.md` ORCH-007。  
本处只留机制：恢复循环 `rebaseReviewPublishLoop` 的编排见 ORCH-004；目标 head 冻结见 ORCH-008。

---

## Target ref 实现注记（归属 ORCH-008）

定义在 `what/orchestrator.md`。实现要点：

```text
fork 时：git symbolic-ref 冻结 TargetBranchFrozen
GetTargetHead 失败 → fail closed（不 fallback HEAD）
git merge --ff-only：branch == frozen ∧ head == expected
```
