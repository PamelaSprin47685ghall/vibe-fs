# 万象阵文档索引

本目录保存从原 `PRD/Wanxiangshu/` **完整迁入**的万象阵规格，与万象术 `docs/01`–`19` 并列，**不混写**。

## 文件

| 文件 | 说明 |
| :--- | :--- |
| [00-index.md](./00-index.md) | 本索引 |
| [01-master-spec.md](./01-master-spec.md) | 主规格（~449 行）：§0–14 + 附录 A–D |
| [02-event-sourcing.md](./02-event-sourcing.md) | NDJSON SSOT 专篇 |
| [03-dev-talk.md](./03-dev-talk.md) | 决策与 API 核实纪要 |

## 阅读顺序

1. [../19-wanxiangzhen.md](../19-wanxiangzhen.md)（万象术侧导航，30 秒摘要）
2. **02-event-sourcing.md**（持久化公理，与万象术 [../05-event-sourcing.md](../05-event-sourcing.md) 对照）
3. **01-master-spec.md**（实现主文档，按章检索）
4. **03-dev-talk.md**（为何某段写「修正」、hook/API 核实出处）

## 源码锚点

| 层 | 路径 |
| :--- | :--- |
| Kernel | `src/Kernel/Wanxiangzhen/`（8 文件：Dag.fs、Scheduler.fs、FfDecision.fs、SquadTask.fs、SquadConfig.fs、SquadPrompts.fs、SquadEvent.fs、SquadUpdateIdAssign.fs） |
| Runtime | `src/Runtime/Wanxiangzhen/`（24 文件） |
| 宿主 | `src/Hosts/OpenCode/PluginWanxiangzhen.fs` |

## 与万象术的边界

| 项 | 万象术 | 万象阵 |
| :--- | :--- | :--- |
| SSOT（物理） | `.wanxiangshu.ndjson`（与万象阵共用） | 同上 |
| npm | `wanxiangshu` / `omp` | `wanxiangshu/wanxiangzhen` |
| 协同 | `/loop`、宿主原生待办写入 | slave prompt / slash，无代码 import |

**权威顺序：实现 > 本目录 > 03-dev-talk 历史叙述。**
