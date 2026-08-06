# Orchestrator — 可观察行为

条款前缀：`ORCH-`。  
Job / Gate 边界见 `shape/orchestrator.md`。  
主程序、事实与恢复分支见 `how/orchestrator.md`。

## ORCH-001：工具面

Orchestrator 只有 `fork-manager` 与 `join`。  
不能读写仓库、解决冲突、操作 Git、调用普通子角色。  
`fork-manager` 只接受 `fast-manager` / `deep-manager`。

## ORCH-002：Clean Gate

每次用户消息进入 Orchestrator 前，工作区必须 clean：

```text
计入：staged / tracked unstaged / untracked / submodule dirty
默认不计：ignored
```

禁止：自动 stash、自动 commit、猜测用户意图清理。  
插件 runtime、spool、lock、worktree **必须**位于目标工作树之外，以免污染 clean 判定。

## ORCH-008：target ref 安全（行为）

目标 branch 在 fork 时冻结。读取 target head 失败 → fail closed，不得静默落到 `HEAD`。  
ff-only publish 必须同时满足：当前 branch == 冻结 target，且 current head == expected head。
