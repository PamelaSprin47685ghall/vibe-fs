# 19 — 万象阵 (wanxiangzhen) 导航

万象阵 = **独立** OpenCode 协调器插件（DAG + worktree + ff-only）。与万象术**互不 import**；安装顺序：先万象术，后万象阵。

## 完整规格

全文在 `docs/wanxiangzhen/`：

| 文件 | 说明 |
| :--- | :--- |
| [wanxiangzhen/00-index.md](./wanxiangzhen/00-index.md) | 索引 |
| [wanxiangzhen/01-master-spec.md](./wanxiangzhen/01-master-spec.md) | **主规格**（~449 行） |
| [wanxiangzhen/02-event-sourcing.md](./wanxiangzhen/02-event-sourcing.md) | NDJSON SSOT 专篇 |
| [wanxiangzhen/03-dev-talk.md](./wanxiangzhen/03-dev-talk.md) | 决策与 API 核实纪要 |

## 30 秒摘要

- Coordinator：HTTP + Scheduler + `SerialQueue` git + 本地 NDJSON append
- Slave：独立 `opencode tui --prompt …` + worktree；`submit_to_squad` / `query_squad`
- 合并：仅 fast-forward；并行 submit 竞争见 master-spec §7
- Durable：物理 `.wanxiangshu.ndjson` 内万象阵 `squad_*`/`task_*` 行 + git 第二真相源
- Review：依赖万象术 `/loop`，万象阵不自实现 reviewer

## 入口与代码

- npm：`wanxiangshu/wanxiangzhen` → `build/src/Hosts/OpenCode/PluginWanxiangzhen.js`
- `src/Kernel/Wanxiangzhen/`（8 文件）、`src/Runtime/Wanxiangzhen/`（24 文件）
