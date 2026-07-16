# 19 — 万象阵 (wanxiangzhen) 导航

万象阵 = **独立** OpenCode 协调器插件（DAG + worktree + ff-only）。与万象术**互不 import**；安装顺序：先万象术，后万象阵。

## 完整规格（无删减）

全部正文在子目录 **`docs/wanxiangzhen/`**（自 git 恢复 `PRD/Wanxiangzhen/` 后迁入）：

| 文档 | 说明 |
| :--- | :--- |
| [wanxiangzhen/00-index.md](./wanxiangzhen/00-index.md) | 万象阵文档索引与阅读顺序 |
| [wanxiangzhen/01-master-spec.md](./wanxiangzhen/01-master-spec.md) | **主规格**（~1590 行）：§0–14 + 附录 A–D |
| [wanxiangzhen/02-event-sourcing.md](./wanxiangzhen/02-event-sourcing.md) | NDJSON SSOT 专篇 |
| [wanxiangzhen/03-dev-talk.md](./wanxiangzhen/03-dev-talk.md) | 决策与 API 核实纪要 |

下文仅 **30 秒摘要**；任何实现争议以 **01-master-spec** 与源码为准。

## 摘要

- Coordinator：HTTP + Scheduler + `SerialQueue` git + 本地 NDJSON append
- Slave：独立 `opencode tui --prompt …` + worktree；`submit_to_squad` / `query_squad`
- 合并：仅 fast-forward；并行 submit 竞争见 master-spec §7
- Durable：**实现**为 `.wanxiangshu.ndjson` 内万象阵 `kind` 行 + git 第二真理源（见 [05](./05-event-sourcing.md)、[wanxiangzhen/02](./wanxiangzhen/02-event-sourcing.md)）
- Review：依赖万象术 `/loop`，万象阵不自实现 reviewer

## 入口与代码

- npm：`wanxiangshu/wanxiangzhen` → `build/src/Hosts/OpenCode/PluginWanxiangzhen.js`
- `src/Kernel/Wanxiangzhen/`、`src/Runtime/Wanxiangzhen/`

## 相关

- [01-overview.md](./01-overview.md)
- [18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md)
- 万象术事件溯源：[05-event-sourcing.md](./05-event-sourcing.md)