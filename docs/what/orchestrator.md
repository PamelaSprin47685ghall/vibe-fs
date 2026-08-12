# Orchestrator — 可观察行为

条款前缀：`ORCH-`。  
Job / Gate 边界见 `shape/orchestrator.md`。  
主程序、事实与恢复分支见 `how/orchestrator.md`。

## ORCH-001：工具面

Orchestrator 的工具与可接受 Manager 类型由 AGENT-006、AGENT-009、AGENT-015
唯一规定：provider 面为 `commission` / `join` / `horizon`。本条只规定本领域应用：Orchestrator
不以自身权限执行 Manager Job 的仓库、冲突解决或 Git 工作；这些工作必须进入后续编排流程。

`commission` 委托独立集成之路（与 Manager 使命内 `fork` 不同 contract，故不同名）。  
成功只见 Byname 承接 charge。**禁止**向 provider 暴露 `job_id` / worktree / `reused` /
agent / role / tier / fallback_peer（机器精度见 EXEC-029/030；续做同 job/worktree/session 属墙内事实）。

## ORCH-002：Clean Gate

每次用户消息进入 Orchestrator 前，工作区必须 clean：

```text
计入：staged / tracked unstaged / untracked / submodule dirty
默认不计：ignored
```

禁止：自动 stash、自动 commit、猜测用户意图清理。  
插件 runtime、spool、lock、worktree **必须**位于目标工作树之外，以免污染 clean 判定。

## ORCH-008：target ref 安全（行为）

目标 branch 在 `commission` 时冻结。读取 target head 失败 → fail closed，不得静默落到 `HEAD`。  
ff-only publish 必须同时满足：当前 branch == 冻结 target，且 current head == expected head。

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
崩溃恢复完全依赖 Journal 最后一条事实折叠，严禁扫描磁盘文件状态反推进度。  
恢复路径可使用墙内 `job_id` / worktree；**不得**把这些字段投影回 Orchestrator provider horizon。
