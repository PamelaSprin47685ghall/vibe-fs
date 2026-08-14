# Orchestrator — 目标实现

## Implements

行为合同见 `what/orchestrator.md`；本文件只描述 commission、job、rebase-review-publish 和恢复算法。

## Ownership

Manager、worktree、integration gate 和事实 writer 见 `shape/orchestrator.md`。

---

## commission 流程（ORCH-001 / EXEC-029）

provider 面动词：`commission` / `join` / `horizon`。旧名 `fork-manager` / `list` 非法、无 alias。

```text
commission(calling?, name, charge):
  Clean Gate（ORCH-002）→ fail closed 若 dirty
  if calling 在场:
      新 ManagerJob / worktree / Manager session
      ManagerAgent ∈ {fast-manager, deep-manager}（墙内）
      Persona = Integrator|Director（session 创建一次绑定；本域不重绑）
      冻结 TargetBranchFrozen（ORCH-008）
  else:
      按 Byname=name 续做既有路（同 job / worktree / session；墙内）
      禁止新建 worktree、换 Manager、重绑 Persona
  → runManagerJob（ORCH-004）
  → provider 成功后果：仅「# <Byname> has taken your charge.»（或等价自然语言）
```

禁止向 Orchestrator provider horizon 投影：`job_id` / worktree / `reused` / agent / role / tier / fallback_peer / `status`/`code`/`error` DTO / UUID。

`join` / `horizon` 后果服从 EXEC-004/005：自然语言 + WorkRecord；无 status plane。

---

## ORCH-004：主程序

直接 CE，禁止「回到创建 Job 入口」的递归：

```text
runManagerJob:
  use worktree
  → create Manager（持久 Agent 名；Persona 已绑）
  → run guarded Manager → candidate
  → rebaseReviewPublishLoop(job, candidate)

rebaseReviewPublishLoop:
  read target head T
  → rebase candidate onto T
  → post-rebase dual PERFECT（同 worktree / 同 Manager；judge 工具，REVIEW-003）
  → acquire short Integration Gate
  → re-read head
       still T → ff-only + 写 Published
       changed → release lock，再进 loop
```

冲突递归只能发生在 `rebaseReviewPublishLoop` 内。  
Integration Gate 只覆盖 ref mutation 窗口，不在 LLM Review / 冲突修复期间持有（ORCH-005）。

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

禁止：省略 `ManagerAgent`；Barrier 事实省略 witness ID；用「CandidateCreated 然后随便走」这种无确定恢复动作的分支；把上述墙内字段投影回 provider。

---

## 恢复编排机制（行为见 what/orchestrator.md ORCH-007）

行为（唯一恢复动作、PublishClaimed 固定三分支顺序不可换、崩溃恢复依赖 Journal 事实折叠、禁止用文件系统状态反推进度）权威定义见 `what/orchestrator.md` ORCH-007。  
本处只留机制：恢复循环 `rebaseReviewPublishLoop` 的编排见 ORCH-004；目标 head 冻结见 ORCH-008。  
恢复路径可使用墙内 `job_id` / worktree；**不得**投影回 Orchestrator provider horizon。

---

## Target ref 实现注记（归属 ORCH-008）

定义在 `what/orchestrator.md`。实现要点：

```text
commission 时：git symbolic-ref 冻结 TargetBranchFrozen
GetTargetHead 失败 → fail closed（不 fallback HEAD）
git merge --ff-only：branch == frozen ∧ head == expected
```
