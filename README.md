# 万象术

在 OpenCode、Mimocode、Mux、oh-my-pi 里用的**多代理开发插件**：子代理帮你写代码、查仓库、搜网页、跑命令、做推理笔记；带审查循环、待办与进度交接、方法论笔记本、上下文预算、fallback 重试，并在重启后仍能恢复工作状态。

---

## 适合谁

- 已在上述任一宿主里写代码，希望 **Manager 委派专业子代理**（编码 / 调查 / 浏览 / 推理笔记），而不是一个会话包办一切。
- 需要 **With-Review**：开发过程中自动拉起独立审查者，按结论改到通过为止。
- 长任务希望 **todowrite 落盘进度**（含交接报告），不依赖对话被压缩后「失忆」。
- 遇到 LLM 失败或超时，希望 **fallback 自动重试/换模型**，无需手动干预。
- 上下文预算紧张时，希望系统**自动催促折叠**，不丢失进度。
- （可选）配合 **万象阵** 插件，把大需求拆成 DAG，多 worktree 并行开发再 ff 合并。

---

## 你能用到什么

| 能力 | 说明 |
| :--- | :--- |
| 子代理 | `coder`、`inspector`、`browser`、`meditator`；支持 `continue` 对同一会话追问 |
| 审查 | `/loop <任务>` 进入 With-Review；`submit_review` / `return_reviewer` |
| 待办与交接 | `todowrite`（Mux / OpenCode / OMP）或 Mimocode 的 `task`；须带完整待办列表 + `select_methodology` |
| 方法论 | `meditator` 单一工具，内部枚举 54 种方法论；与待办里的 `select_methodology` 联动 |
| 工具箱 | 读写改、模糊搜索（`fuzzy_find`/`fuzzy_grep`/`fuzzy_continue`）、网页搜索/抓取、执行器（`executor`，含模式与输出上限约束）、PTY（`pty_spawn`/`write`/`read`/`list`/`kill`）、`glob`、`question` |
| 智能催促 | 待办未完成、审查卡住、子代理空闲等场景下的 nudge（规则在后台，用户主要感知为系统提醒） |
| Fallback 重试 | 模型调用失败时自动按配置链扫描/重试/换模型，事件日志完整记录全过程 |
| 子会话 Actor | 子代理运行由 `SubsessionActor` 状态机管理，确保 dispatch/abort/超时/恢复的确定性 |

具体工具名、角色权限因宿主略有差异（例如 Mimocode 用 `task` 代替 `todowrite`），以宿主里实际注册名为准。

---

## 安装与入口

本仓库编译为 npm 包 **`wanxiangshu`**（F# → Fable → JavaScript）。常见引用方式：

| 用途 | npm 子路径 | 构建产物 |
| :--- | :--- | :--- |
| **Mux**（包默认 main） | `wanxiangshu` | `build/src/Hosts/Mux/Plugin.js` |
| **oh-my-pi 扩展** | `wanxiangshu/omp` | `build/src/Hosts/Omp/Plugin.js` |
| **万象阵**（协调器，需 OpenCode 系） | `wanxiangshu/wanxiangzhen` | `build/src/Hosts/OpenCode/PluginWanxiangzhen.js` |

OpenCode / Mimocode 插件入口在包内构建结果中（`Hosts/OpenCode/Plugin.js`、`PluginMimo.js` 等），按你所用宿主的插件加载方式配置即可。

**万象阵**：与万象术**独立插件、互不 import**；建议先装万象术再装万象阵。协调器用 `/squad` 拆任务；slave 在独立 worktree 里开发，并依赖万象术的 `/loop` 做审查，不重复实现 review。

---

## 常用命令（用户侧）

| 命令 / 工具 | 作用 |
| :--- | :--- |
| `/loop <任务描述>` | 开启 With-Review；空任务可取消循环 |
| `todowrite` / `task` | 提交全量待办 + 五份工作报告（各不少于约定字数）+ 至少一种方法论 |
| `submit_review` | 向审查者提交本轮工作（可标记 WIP） |
| `return_reviewer` | 审查者提交 verdict（PERFECT / REVISE）；仅子会话可用 |
| `/squad …` | **仅万象阵**：提交需求，由协调器 LLM 拆解 DAG（需已装万象阵） |
| `/squad-kill` | **仅万象阵**：结束 slave 进程（保留 worktree 现场） |

工作区根目录会维护 **`.wanxiangshu.ndjson`**（及锁文件）；万象阵 `squad_*`/`task_*` 事件与万象术事件**共用**该文件。审查任务、待办快照、fallback 尝试、催促记录等**持久状态**写在这里，而不是只靠聊天历史。用户一般无需手改该文件。

---

## 使用上应知道的几件事

1. **待办工具**：每次要交**整份**待办列表（及 `select_methodology`），爱用不用。
2. **审查是闭环**：loop 活跃时应用 `submit_review`；审查者结论会驱动继续改或结束任务。
3. **改代码类工具**带有 TDD / 原则确认字段（宿主 schema 中的 `warn_tdd` 等），这是契约的一部分，不是可选装饰。
4. **Fallback 自动重试**：模型调用失败时系统按配置链自动扫描/重试/换模型，无需手动干预。整个过程写入 NDJSON 事件日志，重启后可恢复。
6. **从源码参与开发**：需要 .NET（`net10.0`）、Fable、`npm run build-and-test`（完整构建+测试约数十秒）。日常改代码请遵守仓库 `AGENTS.md`（宿主上游改动范围、hook 写字段方式等）。

---

## 和万象阵的分工

- **万象术**：单工作区内的多代理、审查、事件日志、待办 SSOT、fallback 重试、上下文预算。
- **万象阵**：多进程 + worktree + 本地 HTTP 协调、ff-only 合并；事件与万象术**共用**同一 NDJSON 文件中的不同事件类型。

两者只在 **提示词 / slash** 层配合（例如 slave 侧 `task:` frontmatter 触发 `/loop`），没有代码级耦合。

---

## 想了解更多

- 产品总览：[docs/01-overview.md](./docs/01-overview.md)
- 第一性原理（架构动机）：[docs/01-first-principles.md](./docs/01-first-principles.md)
- 文档索引（01–19 + 万象阵专题）：[docs/00-index.md](./docs/00-index.md)
- 构建与测试：[docs/17-build-test-verify.md](./docs/17-build-test-verify.md)

实现细节、架构边界、事件种类表均以 **`docs/` 与源码为准**；本 README 只面向**使用者**快速建立预期。

---

**一句话**：万象术把「委派、审查、待办、持久进度、fallback 重试、上下文预算」做成可插拔的多宿主插件；你要的是更稳的长程协作，而不是多几个零散工具名。