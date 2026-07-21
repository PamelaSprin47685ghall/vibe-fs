# 万象阵 — Multi-Agent OpenCode Coordinator（完整规格）

> 本文档基于 `src/` 实际代码重写。凡与旧 PRD 冲突处，已按源码事实修正。

## 0. 一句话定义

万象阵是一个 OpenCode 插件。正常启动的 OpenCode 进程（coordinator）加载插件后自起本地 HTTP server，把用户需求经自身 LLM 拆解为 DAG 任务图；对每个就绪任务从最新 master 创建 git worktree，用终端模拟器启动独立 slave OpenCode 进程在隔离 worktree 中开发；slave 完成后经 `submit_to_squad` 通知 coordinator，coordinator 以 ff-only 协议把分支线性合并回 master。

---

## 1. 核心概念（实际代码）

### 1.1 角色

| 角色 | 定义 |
|------|------|
| **Coordinator** | 用户的 OpenCode 进程 + 万象阵插件。自起 HTTP server；NDJSON 唯一写入入口；DAG SSOT = `.wanxiangshu.ndjson` 内 `squad_*`/`task_*` 行 |
| **Slave** | coordinator `child_process.spawn` 的独立 OpenCode 进程，在隔离 worktree 工作。经 HTTP 短连接向 coordinator 发请求 |
| **masterBranch** | coordinator 启动时 `git rev-parse --abbrev-ref HEAD` 取当前分支（可被 `AGENTS.md` `squad.masterBranch` 覆盖） |

### 1.2 Task（`SquadTask.fs`）

```fsharp
type SquadTaskStatus = Pending | Running | Submitted | Merged | Done | Cancelled

type SquadTask =
    { Id: string; Title: string; Description: string; DependsOn: string list
      Status: SquadTaskStatus; WorktreePath: string option; BranchName: string option
      SlavePid: int option; LastHeartbeatAt: string option; MergedSha: string option
      CreatedAt: string; UpdatedAt: string }
```

合法转移（`canTransition`，穷举匹配）：

| from | to | 触发 |
|------|----|------|
| Pending | Running | 依赖全 merged + 并发未满 → Scheduler 启动 |
| Pending | Cancelled | `/squad-kill` |
| Running | Submitted | slave `submit_to_squad` |
| Running | Done | slave PID 消失 / done beacon |
| Running | Cancelled | `/squad-kill` |
| Submitted | Merged | ff 成功 |
| Submitted | Running | ff 非合并结果（rebase_needed / stale_commit / coordinator_not_ready） |
| Submitted | Done | slave 在 ff 检查期间 PID 消失 |
| Submitted | Cancelled | `/squad-kill` |
| Merged / Done / Cancelled | — | 终态 |

`isTerminal` = `Merged | Done | Cancelled`。`tryWithStatus` 校验转移合法性，非法转移 `failwith`。

### 1.3 DAG（`Dag.fs`）

```fsharp
type Dag = { SessionId: string; Tasks: Map<string, SquadTask>; RootRequirement: string }
```

`isReady(task, dag)`：`task.Status = Pending` 且 `DependsOn` 全为 `Merged`。

`Scheduler.decide(dag, maxConcurrent)`（`Scheduler.fs`）：`occupied = runningCount`；`available = maxConcurrent - occupied`；`ready = readyTasks`；`TasksToStart = ready |> truncate available`。

### 1.4 事件（`SquadEvent.fs` + `EventKind.fs`）

8 种事件 DU：

```fsharp
type SquadEvent =
    | SquadCreated of sessionId * requirement
    | TasksCreated of sessionId * tasks: TaskItem list
    | TaskStarted of sessionId * taskId * worktreePath * branchName
    | TaskSubmitted of sessionId * taskId * commitSha
    | TaskMerged of sessionId * taskId * masterSha
    | TaskDone of sessionId * taskId * merged: bool
    | TaskError of sessionId * taskId * error: string
    | SquadCancelled of sessionId
```

物理路径：`src/Runtime/Wanxiangzhen/SquadEventWanCodec.fs`（`squadEventToWanEvent`）→ `.wanxiangshu.ndjson`（与万象术共用）；`src/Runtime/Wanxiangzhen/CoordinatorReplay.fs`（`replayFromEventLog`）读 NDJSON → fold → git reconcile。

---

## 2. 系统架构（实际代码）

### 2.1 Kernel / Shell 分层

**Kernel**（`src/Kernel/Wanxiangzhen/`，8 文件，纯规则）：

| 模块 | 职责 |
| :--- | :--- |
| `Dag.fs` | DAG 数据结构、`isReady`、`readyTasks`、`runningCount`、`topologicalSort`、`validateNoCycles`、`findTask` |
| `Scheduler.fs` | `decide(dag, maxConcurrent)` → `ScheduleDecision` |
| `FfDecision.fs` | `FfResult` DU（`Merged/RebaseNeeded/StaleCommit/CoordinatorNotReady/NotSubmittable`）、`SubmitOutcome`、`formatSubmitOutcome` |
| `SquadTask.fs` | `SquadTaskStatus` DU（6 状态）、`SquadTask`、`canTransition`/`tryWithStatus`/`applyStatus`、`isTerminal` |
| `SquadConfig.fs` | `SquadConfig`、`defaults`（`MaxConcurrent=3`、`Terminal="alacritty"`）、`mergeWithDefaults` |
| `SquadPrompts.fs` | `buildSlavePrompt`（含 `task:` frontmatter 锚点） |
| `SquadUpdateIdAssign.fs` | taskId 自动分配（`squad-` + 4 hex）+ 碰撞重试 |
| `SquadEvent.fs` | 8 事件 DU + `foldEvent`/`foldEvents` + `eventTypeName`/`eventSessionId`/`eventProse` |

**Shell**（`src/Runtime/Wanxiangzhen/`，24 文件，副作用）：

| 模块 | 职责 |
| :--- | :--- |
| `HttpServer.fs` | Node `http.createServer` + `listen(0)` + 路由 |
| `HttpCodec.fs` | HTTP 请求/响应编解码 |
| `CoordinatorRoutes.fs` | `/submit`、`/query`、`/heartbeat` 路由 |
| `CoordinatorBootstrap.fs` | 插件启动：起 HTTP、注册 slash、启动 scheduler |
| `CoordinatorRuntime.fs` | 运行时状态持有 |
| `CoordinatorOps.fs` | 核心操作（submit、ff、cleanup） |
| `CoordinatorLifecycle.fs` | 生命周期管理 |
| `CoordinatorReplay.fs` | `replayFromEventLog`：读 NDJSON → fold → git reconcile |
| `CoordinatorSquadUpdate.fs` | DAG 更新逻辑 |
| `CoordinatorSquadUpdateValidation.fs` | DAG 更新校验 |
| `CoordinatorDepsFactory.fs` | 依赖工厂 |
| `SquadEventWanCodec.fs` | `SquadEvent ↔ WanEvent` 持久化编解码 |
| `SquadEventLogRuntime.fs` | 经共用 `EventStore` 读写 `.wanxiangshu.ndjson` |
| `SquadEventDisplayCodec.fs` | 展示用编解码 |
| `SquadTaskLifecycle.fs` | Task 生命周期管理 |
| `SquadTaskLifecycleStart.fs` | Task 启动逻辑 |
| `SlaveSpawn.fs` | `child_process.spawn` 启动 slave |
| `SlaveRuntime.fs` | Slave 运行时管理 |
| `GitShell.fs` | git 命令封装（worktree、ff、merge-base） |
| `SymlinkShell.fs` | symlink 共享目录管理 |
| `PidMonitor.fs` | PID 轮询 + done beacon |
| `OrphanNotify.fs` | 孤儿任务告警 |
| `SessionIo.fs` | Session IO |
| `ConfigReader.fs` | AGENTS.md frontmatter 解析 |

### 2.2 HTTP API

| 端点 | 方法 | 说明 |
| :--- | :--- | :--- |
| `/submit` | POST | slave 提交任务完成（commit_sha） |
| `/query` | GET | slave 查询 DAG 状态 |
| `/heartbeat` | POST | slave 心跳上报（PID） |

### 2.3 Slave 启动

```bash
opencode tui --prompt "task: <title>\n<description>"
```

slave 工作在独立 worktree，共享主仓库 `.git` gitdir。

### 2.4 合并策略

- 仅 fast-forward（`git merge --ff-only`）
- 并行 submit 竞争：后到者 rebase
- ff 失败处理：`RebaseNeeded` → slave rebase 后重试

---

## 3. 事件溯源

详见 [02-event-sourcing.md](./02-event-sourcing.md)。

核心公理：
- 意图不落盘
- 事件才落盘
- 当前状态 = 积分
- 先写盘后改内存
- 一行一事件
- 按 session 分区
- git 仍为第二真相源

---

## 4. 配置

AGENTS.md frontmatter：

```yaml
squad:
  maxConcurrent: 3
  terminal: alacritty
  masterBranch: main  # 可选，默认取当前分支
```

---

## 5. 已删除/废弃的行为

| 旧行为（已删除） | 新行为 |
| :--- | :--- |
| `session.messages()` 扫文本 fold DAG | `.wanxiangshu.ndjson` 内 `squad_*`/`task_*` 行 fold |
| `session.prompt` 写事件作为 SSOT | `appendSquadEvent` = SSOT；prompt 可选且失败不丢事实 |
| `.squad/state.json` MVP 兜底 | MVP 不需要；NDJSON 已足够 |
| 独立 `.wanxiangzhen.ndjson` | 与万象术共用 `.wanxiangshu.ndjson` |

---

## 6. 验收标准

- 重启 coordinator 后，**仅**依赖 `.wanxiangshu.ndjson` 中 `squad_*`/`task_*` 行可恢复当前 DAG
- compaction 后无 anchor 注入，DAG 仍正确
- `npm run build-and-test` 全绿
- Kernel 无 `Dyn`；fold 在 Kernel；append/fs 仅在 Shell
