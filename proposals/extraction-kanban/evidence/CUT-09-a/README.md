# CUT-09-a 证据

返回 [卡片](../../cards/CUT-09-a.md)。环境：Linux，node v22.21.1，共享 8 核机器。原始日志在 box 的 `/workspace/vfs-cut/wt/logs-cut09d/`（不进仓）。

| 项 | 值 |
|---|---|
| 库实现 / 发布 | wanxiangli `da86791` / `64ae12a` **0.36.0** |
| 主仓 base | master `c109341d7`（gitlink `d2ce694`，0.35.0），干净 worktree，build exit 0 |
| 主仓 candidate | base + Intent.fs 接线 + gitlink `64ae12a`，build exit 0 |

- `node differential.mjs <base> <cand>` → [differential.out](differential.out)：1,290,240 次比较，0 处不同；`decisionTagCoverage` 键为 `Decision.tag[.Rejection.tag]`（0.0/0.1 NoManagedExecution、1 ExternalRoot、2 ActiveHumanContinuation、3 PendingPrompt、5 HostInternal、6.0–6.10 全部 Rejection）。
- 变异：`python3 mutants.py <cand>/node_modules/wanxiangli/dist/authority/Authority.js <name>`（作用在 candidate dist 的拷贝上），结果见 [mutants.out](mutants.out)，6/6 被抓住。
- 套件（base = candidate）：interaction-authority 117/104 pass/13 todo；participant-identity 52/51/1 todo；dispatch-protocol 108/99/1 cancel/1 skip/7 todo；managed-chat-execution 88/82/2 skip/4 todo；crash-reconciliation 69/52/2 skip/15 todo；host-boundary 232/225/2 fail/5 skip。失败与取消用例名集合 base 与 candidate 逐行 diff 为空。
