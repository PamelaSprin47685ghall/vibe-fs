# 万象术文档索引

`docs/` 为系统化技术文档目录，与 `src/`、`tests/` 对齐。**权威顺序：实现与测试 > docs。**

源码当前按三块组织：`src/Hosts/` 是宿主绑定，`src/Kernel/` 是纯规则，`src/Runtime/` 是 IO、事件日志、codec 与运行时编排。文档路径均以这三个真实目录为准；旧的 `Shell/`、`Kernel/EventLog/`、`Opencode/` 仅在历史文字中出现时视为过时称呼。

## 文档地图

| 编号 | 文件 | 内容 |
| :--- | :--- | :--- |
| 00 | [00-index.md](./00-index.md) | 本索引 |
| 01 | [01-overview.md](./01-overview.md) | 产品、公理速查、万象阵边界 |
| 01′ | [01-first-principles.md](./01-first-principles.md) | 七条公理展开（设计动机与纪律） |
| 02 | [02-architecture.md](./02-architecture.md) | Hosts / Kernel / Runtime 三层、依赖纪律、子系统概要 |
| 03 | [03-kernel.md](./03-kernel.md) | `src/Kernel/` 纯内核模块族 |
| 04 | [04-runtime.md](./04-runtime.md) | `src/Runtime/` IO、codec、事件日志与运行时 |
| 05 | [05-event-sourcing.md](./05-event-sourcing.md) | `.wanxiangshu.ndjson`、事件种类全集 |
| 06 | [06-review-and-nudge.md](./06-review-and-nudge.md) | `/loop`、reviewer、nudge 三层架构 |
| 07 | [07-work-backlog.md](./07-work-backlog.md) | todowrite / 五报告 |
| 08 | [08-tools-and-permissions.md](./08-tools-and-permissions.md) | ToolCatalog、权限 |
| 09 | [09-methodology.md](./09-methodology.md) | `methodology_*` schema 与注册 |
| 10 | [10-message-transform.md](./10-message-transform.md) | 管线、Semble、并行提示 |
| 11 | [11-subagents.md](./11-subagents.md) | spawn、SubsessionActor、continue、iterator |
| 12 | [12-fallback.md](./12-fallback.md) | 模型降级运行时、续命六阶段、门闩 |
| 13 | [13-context-budget.md](./13-context-budget.md) | F 触发、R 参数、budget nudge |
| 14–16 | host 文档 | OpenCode / Mux / OMP |
| 17 | [17-build-test-verify.md](./17-build-test-verify.md) | 构建与行为验证 |
| 18 | [18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md) | 术语与真相源 |
| 19 | [19-wanxiangzhen.md](./19-wanxiangzhen.md) | 万象阵导航（完整规格在 `wanxiangzhen/`） |

### 万象阵专题目录 `docs/wanxiangzhen/`

| 文件 | 内容 |
| :--- | :--- |
| [wanxiangzhen/00-index.md](./wanxiangzhen/00-index.md) | 索引 |
| [wanxiangzhen/01-master-spec.md](./wanxiangzhen/01-master-spec.md) | 完整主规格（~1590 行） |
| [wanxiangzhen/02-event-sourcing.md](./wanxiangzhen/02-event-sourcing.md) | 万象阵事件（物理：`.wanxiangshu.ndjson`） |
| [wanxiangzhen/03-dev-talk.md](./wanxiangzhen/03-dev-talk.md) | 决策与 API 核实 |

## 推荐阅读路径

**新人**：01 → 01-first-principles → 02 → 17 → 05 → 06 → 07 → 12  

**宿主适配**：02 → 04 → 08 → 14/15/16 → 10 → 11（SubsessionHostAdapter）  

**改内核**：03 + 相关 05–13 → 先 tests 后代码  

**子系统**：
- Subsession Actor：11 → 05（subsession 事件）→ 12（子会话 Fallback）
- Fallback：12 → 05（续命事件）→ 06（nudge 抑制）
- Nudge：06 → 05（nudge 事件）→ 13（预算 nudge）

## 设计文档并入说明（原 PRD/）

下列专题已写入上表章节，**勿再依赖已删除的 `PRD/`**：

| 原 PRD | 现 docs |
| :--- | :--- |
| master-spec | 01–11、08、17 |
| event-sourcing | 05 |
| continue-subagent | 11 |
| subsession-actor | 11（§ SubsessionActor） |
| fallback-recovery | 12 |
| semble-mcp | 10 § Semble |
| parallel / empty-output | 10 |
| hooks-complexity | 10 § 复杂度 |
| context-budget 09/10 | 13 |
| architecture-refactoring | 02 § 演进路线 |
| Wanxiangshu/* | `wanxiangzhen/01`–`03` + [19](./19-wanxiangzhen.md) |

## 当前源码入口

| 能力 | 源码入口 | npm/build 入口 |
| :--- | :--- | :--- |
| Mux | `src/Hosts/Mux/Plugin.fs` | `wanxiangshu` / `build/src/Hosts/Mux/Plugin.js` |
| OMP | `src/Hosts/Omp/Plugin.fs` | `wanxiangshu/omp` / `build/src/Hosts/Omp/Plugin.js` |
| OpenCode | `src/Hosts/OpenCode/Plugin.fs` | 构建产物中的 OpenCode 插件入口 |
| Mimocode | `src/Hosts/OpenCode/PluginMimo.fs`、`PluginMimoTui.fs` | 无独立 npm export |
| 万象阵 | `src/Hosts/OpenCode/PluginWanxiangzhen.fs` | `wanxiangshu/wanxiangzhen` / `build/src/Hosts/OpenCode/PluginWanxiangzhen.js` |

构建、单元/集成测试与 OpenCode E2E 命令见 [17-build-test-verify.md](./17-build-test-verify.md)。
