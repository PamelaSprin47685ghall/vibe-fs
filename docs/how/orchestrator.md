# Orchestrator — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在多 Agent（Manager/Coder）并行协作与多分支 Publish 场景下，并发的分支变基（Rebase）、代码评审（Dual PERFECT Review）与分支 Push 操作可能导致 Git 树历史损坏、脏工作区混入或竞争条件下的覆盖写。Orchestrator 模块旨在通过干净工作区门禁（Clean Gate）、短 Integration Gate CAS 锁与严格的 Journal 持久化事实，实现原子化、可重入的分布式 Rebase-Review-Publish 工作流。

### 2. 输入输出与规则边界
- **输入**：ManagerJob 创建请求、Target Branch Snapshot HEAD、Dual PERFECT Review Witness。
- **输出**：`ManagerJobCreated`、`CandidateReady`、`RebasedCandidateReady`、`PublishClaimed`、`Published` 事实。
- **核心边界与不变量**：
  1. Clean Gate 门禁（ORCH-002）：绝不在存在未提交修改的脏工作区上发起编排或猜测用户意图。
  2. 短 Integration Gate（ORCH-005）：锁的范围严格限定在 Ref Mutation CAS 提交阶段，允许 Job 之间并行进行 Rebase 与 Review。
  3. Journal 唯一出口恢复（ORCH-007）：崩溃恢复完全依赖 Journal 最后一条事实折叠，严禁扫描磁盘文件状态反推进度。
  4. PublishClaimed 确定性三分支：崩溃恢复穷尽 `currentHead == rebasedCommit`（已完成）、`currentHead == ExpectedHead`（未完成）与 `其它`（已过期）三分支。

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
