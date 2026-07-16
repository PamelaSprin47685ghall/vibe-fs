# 万象阵文档索引

本目录保存从原 `PRD/Wanxiangshu/` **完整迁入**的万象阵（wanxiangzhen）规格，与万象术 `docs/01`–`18` 并列，**不混写**。

## 文件

| 文件 | 行数级 | 内容 |
| :--- | ---: | :--- |
| [00-index.md](./00-index.md) | — | 本索引 |
| [01-master-spec.md](./01-master-spec.md) | ~1590 | 保姆级主规格：DAG、Coordinator/Slave、HTTP、git、错误处理、附录 API/事件 |
| [02-event-sourcing.md](./02-event-sourcing.md) | ~120 | NDJSON SSOT（物理 `.wanxiangshu.ndjson`）、事件表、分层、废弃行为对照 |
| [03-dev-talk.md](./03-dev-talk.md) | ~363 | API 核实与决策纪要（轮次记录）；读 master-spec 前可先扫修正点 |

## 阅读顺序

1. 总览：[../19-wanxiangzhen.md](../19-wanxiangzhen.md)（万象术 docs 侧导航）
2. **02-event-sourcing**（持久化公理，与万象术 [../05-event-sourcing.md](../05-event-sourcing.md) 对照）
3. **01-master-spec**（实现主文档，按章检索）
4. **03-dev-talk**（为何某段写「修正」、hook/API 核实出处）

## 源码锚点

- `src/Kernel/Wanxiangzhen/`
- `src/Runtime/Wanxiangzhen/`
- `src/Hosts/OpenCode/PluginWanxiangzhen.fs`
- `tests/EventLog*`、`EventReplayTests`、`wanxiangzhenTestEntries`

## 与万象术

| 项 | 万象术 | 万象阵 |
| :--- | :--- | :--- |
| SSOT | `.wanxiangshu.ndjson`（与万象术共用） | 万象阵 `squad_*`/`task_*` 行写入同一 NDJSON（见 [02-event-sourcing.md](./02-event-sourcing.md) §2） |
| npm | `wanxiangshu` / `omp` | `wanxiangshu/wanxiangzhen` |
| 协同 | `/loop`、todowrite | slave prompt / slash，无代码 import |

权威顺序：**实现 > 本目录 > 03-dev-talk 历史叙述**。