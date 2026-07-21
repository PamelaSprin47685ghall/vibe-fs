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

**Shell**（`src/Runtime/Wanxiangzhen/`，15 文件，副作用）：

| 模块 | 职责 |
| :--- | :--- |
| `CoordinatorRuntime.fs` | `CoordinatorDeps` 记录 + `generateTaskId` |
| `CoordinatorBootstrap.fs` | 插件启动序列 |
| `CoordinatorOps.fs` | `handleSubmitCore`、`formatDagText`、`handleSlaveExit` |
| `CoordinatorLifecycle.fs` | `handleSquadKill`、`handleSquadUpdate` |
| `CoordinatorReplay.fs` | `replayFromEventLog` + `reconcileTask`（git 校正） |
| `CoordinatorRoutes.fs` | HTTP 路由 |
| `HttpServer.fs` | `startServer`（bind 127.0.0.1 + Bearer token 校验） |
| `HttpCodec.fs` | `encodeResult`/`encodeFfResponseBody` |
| `GitShell.fs` | `tryWorktreeAdd`/`tryWorktreeRemoveForce`/`tryBranchDeleteForce`/`revParseHead`/`revParseBranch`/`mergeBaseIsAncestor`/`mergeFfOnly` |
| `SlaveSpawn.fs` | `buildSlaveCommand`（终端映射）+ `spawnSlave` |
| `SlaveRuntime.fs` | `readSlaveConfig`（env 读取）+ `registerPid` + `submitToSquad` + `querySquad` + `doneBeacon` |
| `PidMonitor.fs` | `isPidAlive`（`process.kill(pid, 0)`）+ `startPolling`/`stopPolling` |
| `SquadEventWanCodec.fs` | `SquadEvent ↔ WanEvent`（payload 含 `tasksJson`） |
| `SquadEventLogRuntime.fs` | `readAllSquadEvents`/`appendSquadEvent`（经共用 `EventStore`） |
| `SquadEventDisplayCodec.fs`（`EventCodec.fs`） | **仅**展示用 yaml frontmatter + prose，**不参与** durable 重放 |
| `SymlinkShell.fs` | sharedDirs symlink |
| `ConfigReader.fs` | AGENTS.md frontmatter `squad:` 解析 |

### 2.2 双模式（环境变量区分）

```fsharp
// Coordinator 模式：无 SQUAD_COORDINATOR_URL
// Slave 模式：有 SQUAD_COORDINATOR_URL → readSlaveConfig() → Some cfg
```

Slave 工具注册：`submit_to_squad`、`query_squad`。

---

## 3. Coordinator 详细设计

### 3.1 启动序列（`CoordinatorBootstrap.fs`）

```
1. masterBranch = git rev-parse --abbrev-ref HEAD（cwd = input.worktree）
2. config = readSquadConfig(input.worktree/AGENTS.md)
3. token = crypto.randomBytes(16).toString("hex")
4. server = http.createServer(handler).listen(0, "127.0.0.1")
5. dag = empty DAG（masterSessionId 暂未知）
6. 起 PID 健康轮询 setInterval（默认 2000ms）
7. 返回 hook 字典：{ tool:{squad_update}, config, command.execute.before, event, dispose }
```

### 3.2 HTTP Server（`HttpServer.fs`）

- 只绑 `127.0.0.1`，`listen(0)` 由 OS 分配端口
- 全端点 Bearer token 校验（`Authorization: Bearer <token>`）
- 无 WebSocket/SSE，全短连接

### 3.3 HTTP API

| Method | Path | 说明 |
|--------|------|------|
| GET | `/task/:id` | 获取 task 详情 |
| POST | `/task/:id/submit` | ff 检查 + merge（SerialQueue 内） |
| POST | `/task/:id/register` | slave 上报 PID |
| POST | `/task/:id/done` | done beacon |
| GET | `/state` | DAG 全局状态 |
| POST | `/task/:id/log` | 可选进度日志 |

### 3.4 `/squad` 命令

1. 捕获 `masterSessionId`（`command.execute.before` hook）
2. `commitEvent(SquadCreated)` → **先 append NDJSON** → 后更新内存 DAG
3. `output.parts` 放入拆解指令（`encodeEvent` 展示给 LLM）

### 3.5 `squad_update` 工具

```
1. 校验 events[]（tasks_created 非空、title/description 必填、dependsOn 引用存在）
2. 拓扑校验（循环检测）→ 有环则拒绝整批
3. 聚合所有 tasks，缺 taskId 自动生成（4 hex，碰撞重试，上限 10 次）
4. commitEvent(TasksCreated) → append NDJSON → fold → 更新内存 DAG
5. schedulerTick()
```

校验失败返回类型分支（不抛异常）：

```
拓扑有环 → "Dependency cycle detected: ... Please re-decompose without cycles."
依赖悬空 → "Task ... dependsOn unknown ... Fix dependencies."
```

### 3.6 Scheduler（`Scheduler.fs` + `SquadTaskLifecycle.fs`）

`schedulerTick`：
- `re-entrance guard`（`rt.Scheduling` 布尔，防重叠 tick）
- `decide(dag, maxConcurrent)` → `TasksToStart`
- 对每个 ready task：`startTask`（`createWorktree` → `spawnSlave` → `commitEvent(TaskStarted)`）
- `TasksWaiting` 留 pending

### 3.7 Git Executor（`GitShell.fs`，SerialQueue 串行化）

所有 masterBranch git 操作经 `SerialQueue`：

```
tryFastForward(taskId, branchName, reportedSha):
  0. stale 校验：rev-parse branch == reportedSha？否 → StaleCommit
  1. 前置校验：coordinator 仍在 masterBranch + worktree clean
  2. merge-base --is-ancestor masterBranch branch → yes → merge --ff-only → Merged
  3. else → RebaseNeeded
```

关键不变式：检查与合并在 SerialQueue 内原子执行，两 slave 的 ff 不交叉。

### 3.8 Slave 进程管理

**Spawn**（`SlaveSpawn.fs`）：

```fsharp
buildSlaveCommand(terminal, worktree, prompt) → (cmd, args)
// alacritty:  alacritty --working-directory <wt> -e opencode tui --prompt <p>
// headless:   opencode tui --prompt <p>  (cwd=<wt>, 无窗口)
// 其他终端映射见 SlaveSpawn.fs
```

环境变量注入：`SQUAD_COORDINATOR_URL` / `SQUAD_TASK_ID` / `SQUAD_WORKTREE_PATH` / `SQUAD_MASTER_BRANCH` / `SQUAD_TOKEN`。

**PID 健康轮询**（`PidMonitor.fs`）：`isPidAlive(pid)` → `process.kill(pid, 0)`；ESRCH = 已死，EPERM = 存活。`setInterval` 定时探测 register 上报的 PID。

**done beacon**：slave `dispose` hook → `POST /task/:id/done` → coordinator 立即 `handleSlaveExit`。

**Slave Exit 处理**（`SquadTaskLifecycle.fs`）：

```
handleSlaveExit(taskId):
  幂等保护：已 Merged/Done/Cancelled → return
  → commitEvent(TaskDone) → append NDJSON
  → tryWithStatus(..., Done, now)
  → cleanupAndReport（removeWorktree + deleteBranch）
  → schedulerTick()
```

### 3.9 Worktree 管理（`GitShell.fs` + `SymlinkShell.fs`）

```
createWorktree(task):
  git worktree add -b <branchName> <worktreePath> <masterBranch>
  createSymlinks(worktreePath, projectRoot, config.SharedDirs)
  记 task.WorktreePath / task.BranchName
```

路径：`{projectRoot}/../worktree-{taskId}`。taskId = `squad-` + 4 hex，碰撞重试（上限 10 次）。

删除时机（`cleanupTask`）：`Merged` / `Done` → `removeWorktree + deleteBranch`；`Cancelled` → **不删**（保留现场）。

### 3.10 Slave 初始 Prompt（`SquadPrompts.fs`）

```fsharp
buildSlavePrompt(taskId, title, description, masterBranch) →
  "---\ntask: {title}\n---\n\nYou are executing squad task {taskId}: {title}\n...\n\
   Activate With-Review Mode by following the review workflow.\n\
   After development, call submit_review for review.\n\
   After review PASS, git commit, then call submit_to_squad.\n\
   If asked to rebase, run: git rebase {masterBranch}, then resubmit."
```

`task:` frontmatter 锚点经 `opencode tui --prompt` 注入后，万象术 `messages.transform` 识别 With-Review 激活。

### 3.11 重放（`CoordinatorReplay.fs`）

```
replayFromEventLog:
  1. ReadAllSquadEvents → WanEvent list
  2. foldEvent(squad_created → archive prior dag → new empty dag)
           foldEvent(tasks_created → add tasks)
           foldEvent(task_started → status=Running)
           foldEvent(task_submitted → status=Submitted)
           foldEvent(task_merged → status=Merged, mergedSha)
           foldEvent(task_done → status=Done)
  3. reconcileTask：对 Running/Submitted task 做 merge-base --is-ancestor 校正
     - GitError 存在时：Submitted → Running（允许 slave 重试），Running 不变
     - branch 是 masterBranch 祖先 → Merged（校正 mergedSha）
     - else → Submitted → Running
  4. 返回 dag + sessions
```

### 3.12 并行 ff 竞争

不变式：`executeOnMaster` 内 `merge-base --is-ancestor` + `merge --ff-only` 作为 SerialQueue 单个 Enqueue 单元。后到者 `RebaseNeeded` → `git rebase {masterBranch}`（本地，无 fetch）→ 重新 review + submit → 循环至 merged。

### 3.13 `/squad-kill` 命令

```
1. 对 running/submitted task：process.kill(pid, SIGTERM)
2. 不删 worktree（保留现场）
3. 不删分支
4. commitEvent(SquadCancelled) → fold → 所有非终态 task → Cancelled
```

幂等保护：已 Merged/Done/Cancelled → 二次触发不覆盖。

---

## 4. Slave 详细设计

### 4.1 启动

`opencode tui --prompt "<buildSlavePrompt(task)>"`（cwd=worktreePath）。slave 插件检测 `SQUAD_COORDINATOR_URL` → Slave 模式 → 注册 `submit_to_squad` / `query_squad` → `POST /register { pid }`。

### 4.2 `submit_to_squad`

```
POST /task/:id/submit
  headers: { Authorization: "Bearer " + SQUAD_TOKEN }
  body: { commitSha }

match response.result:
  Merged _             → "✅ Merged. Task complete. You may stop."
  RebaseNeeded _       → git rebase {masterBranch} → re-loop → re-submit
  StaleCommit          → git commit 后重 submit
  CoordinatorNotReady _→ 稍候重试
  NotSubmittable _     → 向用户报告，idle
  TaskNotFound         → 向用户报告，idle
  CoordinatorUnreachable → 向用户报告，idle
```

### 4.3 `query_squad`

`GET /state` → 返回 sessions 数组；`GET /task/:id` → 单 task 详情。HTTP 失败不阻塞 slave。

### 4.4 工作循环

```
do {
  /loop review（开发或 rebase）
} while (submit_to_squad → RebaseNeeded)
review PASS → git commit → submit_to_squad → merged → 完成
```

---

## 5. 配置（`AGENTS.md` frontmatter）

```yaml
squad:
  maxConcurrent: 3          # 同时运行 slave 上限
  terminal: alacritty       # 终端模拟器（缺省 → 平台探测）
  masterBranch: main        # 集成分支名（缺省 → coordinator 启动时所在分支）
  sharedDirs:               # 只读共享目录（symlink）
    - node_modules
    - .venv
```

解析：`src/Runtime/Wanxiangzhen/ConfigReader.fs`（全量 yaml，`yaml` 包）。

---

## 6. 错误处理摘要

| 场景 | 行为 |
| :--- | :--- |
| coordinator 崩溃 | HTTP server 随进程死 → slave ECONNREFUSED → slave 工具返回 `CoordinatorUnreachable` → idle |
| slave 崩溃 | PID 轮询发现 PID 消失 → `TaskDone(false)` → 删 worktree → tick |
| ff 非合并 | 返回 `RebaseNeeded` → slave rebase → 重 review + 重 submit |
| NDJSON 损坏行 | 截断（该行及之后丢弃），不跳过坏行 |
| `/squad-kill` | 杀进程，保留 worktree + 分支，commit `SquadCancelled` |

---

## 7. 不做的事

| 不做 | 原因 |
| :--- | :--- |
| Slave 嵌套 spawn 子 slave | 避免 worktree 嵌套地狱 |
| Coordinator 自动解决合并冲突 | ff-only，冲突由 slave LLM 在 worktree 解决 |
| 非 ff 合并 | 违反线性历史 |
| Coordinator 主动推消息给 slave | slave 发起短连接，coordinator 不主动 |
| Coordinator 崩溃后自动重连旧 slave | 用户 `/squad-kill` 重来 |
| 内置 review | 依赖万象术 `/loop` |
| 以 session 历史 fold DAG | SSOT = NDJSON fold |
| rebase 重试上限 | 无限猴子形式主义完成 |

---

## 附录 A：术语

| 术语 | 定义 |
| :--- | :--- |
| Coordinator | 用户的 OpenCode 进程 + 万象阵插件；NDJSON 唯一写入入口 |
| Slave | coordinator spawn 的独立 OpenCode 进程（`opencode tui --prompt`） |
| DAG | 有向无环图，描述 task 间依赖 |
| Task | DAG 节点；状态机 pending→running→submitted→merged→done（`SquadTaskStatus`） |
| Worktree | `git worktree add` 隔离工作目录，与主仓库共享 `.git` |
| FF | `git merge --ff-only`，masterBranch 仅线性前进 |
| SerialQueue | 串行锁队尾保 masterBranch git 操作原子 |
| SSOT | `.wanxiangshu.ndjson` 内 `squad_*`/`task_*` 行 fold；git refs 作第二真相源 |
| done beacon | slave `dispose` hook → `POST /task/:id/done` |

## 附录 B：与万象术对照

| 维度 | 万象术 | 万象阵 |
| :--- | :--- | :--- |
| SSOT（物理文件） | `.wanxiangshu.ndjson`（与万象阵共用） | 同上 |
| SSOT（逻辑行） | `loop_*` / `nudge_*` / `assistant_completed` | `squad_*`/`task_*` |
| 串行化 | `PromiseQueue.SerialQueue` | 同上（复用） |
| Review | 内置 `/loop` | 依赖万象术 `/loop` |
| Worktree | 无 | `git worktree` 隔离 + 共享 `.git` |
| 多进程 | 单进程内多 subagent | 多 OpenCode 进程 + HTTP 短连接 |
| 进程生命周期 | 进程内 Promise / abort | spawn 句柄 + PID 轮询 + done beacon（无 child-exit hook） |
| 首条 prompt | 进程内 `session.prompt` | `opencode tui --prompt` CLI 注入 |
| 并发控制 | `AgentSemaphore` | `maxConcurrent` + Scheduler 就绪计数 |
| 崩溃恢复 | 重启重放对话历史 | 重启重放 + git 真相校正 |

## 附录 C：HTTP API 速查

全端点绑 `127.0.0.1`，全程 Bearer token 校验。

```
GET  /task/:id
  200 { id, title, description, dependsOn[], status }
  401 { result: "unauthorized" }
  404 { result: "task_not_found" }

POST /task/:id/submit
  body { commitSha: string }
  200 { result: "merged",                masterSha: string }
  200 { result: "rebase_needed",         masterSha: string }
  200 { result: "stale_commit" }
  200 { result: "coordinator_not_ready", reason: "not_on_master"|"dirty" }
  200 { result: "not_submittable",       currentStatus: string }
  401 / 404 同上
```

`FfResult` DU（`FfDecision.fs`）：`Merged | RebaseNeeded | StaleCommit | CoordinatorNotReady | NotSubmittable`。
`SubmitOutcome` DU（含传输层）：`Response of FfResult | TaskNotFound | CoordinatorUnreachable | Unauthorized | LocalGitError`。

## 附录 D：NDJSON 事件 Schema

8 类事件（`SquadEvent.fs`）：

| kind | payload 要点 |
| :--- | :--- |
| `squad_created` | `requirement` |
| `tasks_created` | `tasksJson`（编码的 TaskItem[]） |
| `task_started` | `task_id`, `worktree_path`, `branch_name` |
| `task_submitted` | `task_id`, `commit_sha` |
| `task_merged` | `task_id`, `master_sha` |
| `task_done` | `task_id`, `merged` (bool) |
| `task_error` | `task_id`, `error` |
| `squad_cancelled` | — |

无 `task_rebased` 事件：rebase 发生在 slave 本地 worktree，coordinator 不感知过程，只见重新 `task_submitted`。

重放规则：`CoordinatorReplay.fs` → `foldEvent` 按文件行序；`reconcileTask` 对 `Running`/`Submitted` 做 git 校正（`merge-base --is-ancestor`）。`Cancelled` 任务**不**受 git 校正覆盖（用户显式中止的权威性高于 git 推断）。

---

*版本：基于 `src/` 实际代码重写。权威顺序：实现 > 本文 > 03-dev-talk 历史叙述。*
