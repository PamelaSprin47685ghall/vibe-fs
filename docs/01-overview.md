# 01 — 产品总览

## 一句话

**万象术 (wanxiangshu)**：F# 经 Fable 编译为 JavaScript 的**多代理插件运行时**。同一套 **Kernel（纯规则）+ Shell（副作用边界）** 接到四套宿主：**OpenCode、Mimocode、Mux、oh-my-pi (OMP)**。

## 表面能力 vs 真实难题

### 对外能力

- 子代理委派（coder / inspector / browser / meditator / reviewer 等）
- 文件读写、模糊搜索、执行器、审查循环、`submit_review` / `return_reviewer`
- Nudge（todo / loop / runner）
- 54 个结构化方法论笔记本工具（`methodology_<id>`）
- 四套宿主各自的 tool schema 与 hook 接线

### 工程根问题（不解决则一切退化为脆弱 hook）

1. **宿主协议漂移**：Zod / Mux catalog / TypeBox / Pi 事件模型互不兼容。
2. **LLM 弱类型**：消息与工具参数是动态对象，须在边界立刻强类型化。
3. **宿主 compaction**：对话历史不可靠，不能作 durable 状态真相。
4. **跨轮状态**：review、todo backlog、nudge 须在进程重启后恢复。
5. **副作用**：Node IO 不可避免，但业务规则须可单测、可重放。

## 七条设计公理

速查表如下；**逐条展开、推论与纪律**见 **[01-first-principles.md](./01-first-principles.md)**（原 README 第一性原理正文已迁入该文）。

| # | 公理 | 工程含义 |
| :---: | :--- | :--- |
| 1 | 稳定资产是领域规则 | `src/Kernel/`，非宿主 API |
| 2 | 事件流是事实 | `[workspace]/.wanxiangshu.ndjson` |
| 3 | 副作用压到边界 | `src/Runtime/` + 各宿主薄适配 |
| 4 | 边界强类型 | `obj` → DU/record，内核只吃强类型 |
| 5 | 进度不靠 compaction 锚点 | backlog 靠 `work_backlog_committed` |
| 6 | 并发 = 共享可变状态 | `PromiseQueue` / 按 session 串行域 |
| 7 | 测试时间无关 | 注入时钟、轮询收敛，禁脆弱 sleep |

## 与万象阵 (wanxiangzhen) 的边界

- **万象术**：单工作区内多代理工具、review；durable 事件 SSOT = `.wanxiangshu.ndjson`（`loop_*`、`work_backlog_*`、`nudge_*` 等 kind）。
- **万象阵**：独立协调器插件，DAG + worktree + ff-only 合并；durable 事件与万象术**共用** `.wanxiangshu.ndjson`（`squad_*`/`task_*` 行，见 [19-wanxiangzhen.md](./19-wanxiangzhen.md)）。
- 两插件**互不 import**；协同靠 slash / prompt front-matter（如 slave 侧触发 `/loop`）。
- npm 子路径 `wanxiangshu/wanxiangzhen` → `build/src/Hosts/OpenCode/PluginWanxiangzhen.js`，与默认 Mux 入口不同。

## 用户可见主流程（概念）

```mermaid
flowchart LR
  U[用户 / LLM] --> H[宿主 Hook]
  H --> S[Shell: decode + IO]
  S --> K[Kernel: 校验 + 状态转移]
  K --> E[追加 NDJSON 事件]
  E --> P[Fold → 内存投影]
  P --> T[MessageTransform → caps / replay]
  T --> H
```

## 角色与工具（摘要）

权限矩阵 SSOT：`src/Kernel/ToolPermission.fs` + `ToolCatalog`。典型划分：

- **Manager**：`todowrite`/`task`、子代理、`submit_review`
- **Coder**：读写改、patch、executor（写模式）
- **Inspector**：read、fuzzy_*、executor（只读）
- **Reviewer**：read、`return_reviewer`

细节见 [08-tools-and-permissions.md](./08-tools-and-permissions.md)。

## 命令接口（摘要）

| 命令 | 作用 |
| :--- | :--- |
| `/loop <task>` | 激活 With-Review；空 task 取消 |

状态与事件见 [06-review-and-nudge.md](./06-review-and-nudge.md)。

## 下一步阅读

- 公理展开：[01-first-principles.md](./01-first-principles.md)
- 结构：[02-architecture.md](./02-architecture.md)
- 持久化：[05-event-sourcing.md](./05-event-sourcing.md)
- 构建验证：[17-build-test-verify.md](./17-build-test-verify.md)