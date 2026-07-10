# 万象术文档索引

`docs/` 为**唯一**系统化技术文档目录，与 `src/`、`tests/` 对齐。**权威顺序：实现 > docs。**

## 文档地图

| 编号 | 文件 | 内容 |
| :--- | :--- | :--- |
| 00 | [00-index.md](./00-index.md) | 本索引 |
| 01 | [01-overview.md](./01-overview.md) | 产品、公理、万象阵边界 |
| 02 | [02-architecture.md](./02-architecture.md) | 三层架构、依赖纪律、演进路线 |
| 03 | [03-kernel.md](./03-kernel.md) | 纯内核模块族 |
| 04 | [04-shell.md](./04-shell.md) | IO、codec、运行时 |
| 05 | [05-event-sourcing.md](./05-event-sourcing.md) | `.wanxiangshu.ndjson` |
| 06 | [06-review-and-nudge.md](./06-review-and-nudge.md) | `/loop`、reviewer、nudge |
| 07 | [07-work-backlog.md](./07-work-backlog.md) | todowrite / 五报告 |
| 08 | [08-tools-and-permissions.md](./08-tools-and-permissions.md) | ToolCatalog、权限 |
| 09 | [09-methodology.md](./09-methodology.md) | 54× `methodology_*` |
| 10 | [10-message-transform.md](./10-message-transform.md) | 管线、Amend、Semble、并行提示 |
| 11 | [11-subagents.md](./11-subagents.md) | spawn、continue、iterator |
| 12 | [12-fallback.md](./12-fallback.md) | 模型降级 FSM |
| 13 | [13-context-budget.md](./13-context-budget.md) | F 触发、budget nudge |
| 14–16 | host 文档 | OpenCode / Mux / OMP |
| 17 | [17-build-test-verify.md](./17-build-test-verify.md) | 构建与 ArchitectureTests |
| 18 | [18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md) | 术语与真相源 |
| 19 | [19-wanxiangzhen.md](./19-wanxiangzhen.md) | 万象阵导航（完整规格在 `wanxiangzhen/`） |

### 万象阵专题目录 `docs/wanxiangzhen/`

| 文件 | 内容 |
| :--- | :--- |
| [wanxiangzhen/00-index.md](./wanxiangzhen/00-index.md) | 索引 |
| [wanxiangzhen/01-master-spec.md](./wanxiangzhen/01-master-spec.md) | 完整主规格（~1590 行） |
| [wanxiangzhen/02-event-sourcing.md](./wanxiangzhen/02-event-sourcing.md) | `.wanxiangzhen.ndjson` |
| [wanxiangzhen/03-dev-talk.md](./wanxiangzhen/03-dev-talk.md) | 决策与 API 核实 |

## 推荐阅读路径

**新人**：01 → 02 → 17 → 05 → 06 → 07  

**宿主适配**：02 → 04 → 08 → 14/15/16 → 10  

**改内核**：03 + 相关 05–13 → 先 tests 后代码  

## 设计文档并入说明（原 PRD/）

下列专题已写入上表章节，**勿再依赖已删除的 `PRD/`**：

| 原 PRD | 现 docs |
| :--- | :--- |
| master-spec | 01–11、08、17 |
| event-sourcing | 05 |
| continue-subagent | 11 |
| fallback-recovery | 12 |
| semble-mcp | 10 § Semble |
| amend / parallel / empty-output | 10 |
| hooks-complexity | 10 § 复杂度 |
| context-budget 09/10 | 13 |
| architecture-refactoring | 02 § 演进路线 |
| Wanxiangshu/* | `wanxiangzhen/01`–`03` + [19](./19-wanxiangzhen.md) |

## 源码体量

| 目录 | 约 .fs |
| :--- | ---: |
| Shell | 139 |
| Kernel | 92 |
| Opencode | 49 |
| Omp | 33 |
| Mux | 21 |
| Methodology | 11 |

npm：`"."` Mux；`./omp`；`./wanxiangzhen`。