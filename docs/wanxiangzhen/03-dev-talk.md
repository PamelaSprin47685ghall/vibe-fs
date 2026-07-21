# DEV_TALK.md — 万象阵设计决策历程

本文档记录万象阵设计过程中所有关键决策，按讨论轮次排列，每轮附出处核实。

## 轮次 0：初始架构提案

用户提出完整架构蓝图：正常启动的 OpenCode = coordinator，插件起 HTTP server；slave 经环境变量连接；DAG 任务图 + git worktree 隔离 + ff-only 线性合并；`/squad` 拆解 → worktree → /loop 开发 → submit_to_squad → ff 检查 → 可能 rebase → 清理。

## 轮次 1：12 个细化决策

| # | 决策点 | 用户选择 |
| :--- | :--- | :--- |
| 1.1 | slave 提交机制 | 工具调用触发 ff 检查，拒绝时返回提示让 slave rebase |
| 1.2 | DAG 拆解执行者 | coordinator LLM 拆，不会失败，顶多 nudge |
| 1.3 | SSOT 存储 | **演进**：原选对话历史 → 后改为 `.wanxiangshu.ndjson`（`02-event-sourcing.md`） |
| 1.4 | 通信模型 | slave 发起短连接，coordinator 不主动 |
| 1.5 | coordinator 定位 | OpenCode 插件，借宿主进程跑 |
| 1.6 | slave 能力边界 | 不能 spawn 子 slave；乐观 git 约束 |
| 1.7 | 并发控制 | 可配置上限 |
| 1.8 | 共享目录 | symlink 只读共享 |
| 1.9 | 环境变量注入 | `opencode tui --prompt` CLI 注入（核实 5.6） |
| 1.10 | 崩溃处理 | slave 崩溃=task done；coordinator 崩溃=slave idle |
| 1.11 | 终端配置 | AGENTS.md frontmatter 可配 |
| 1.12 | worktree 路径 | `项目/../worktree-hex4` |

## 轮次 2：6 个深入决策

| # | 决策点 | 用户选择 |
| :--- | :--- | :--- |
| 2.1 | 事件与 LLM | **演进**：后台事件默认静默；session.prompt 仅 LLM 驱动场景显式发起 |
| 2.2 | review/submit 嵌套 | `do { /loop } while (不能 ff)` |
| 2.3 | done 语义 | 形式主义，不管内容 |
| 2.4 | 重试上限 | 无限（无限猴子） |
| 2.5 | prompt() 对象 | coordinator 自己的 LLM（仅 /squad 拆解、nudge、孤儿告警时） |
| 2.6 | DAG 依赖语义 | merged 后才 fork worktree（lazy creation） |

## 轮次 3：3 个收尾决策

| # | 决策点 | 用户选择 |
| :--- | :--- | :--- |
| 3.1 | 并行竞争 | 后到者 rebase |
| 3.2 | 清理时机 | merged 删，done 删 |
| 3.3 | `/squad-kill` | 只杀进程，保留现场 |

## 轮次 4：最终确认

用户确认所有决策点锁定，进入 PRD 撰写。

## 轮次 5：源码核实与技术修正

撰写 PRD 前对照 OpenCode 本体（`packages/opencode`）与万象术实际源码逐条核实。

### 核实 5.1：插件入口

`PluginInput` 含 `client`（`session.*`/`event.subscribe`）、`worktree`、`$: BunShell`。Coordinator 需自起独立 HTTP server（Node `http.createServer` + `listen(0)`）；opencode `serverUrl` 无法挂自定义路由。

### 核实 5.2：无 child-exit hook

opencode 插件 Hooks **无**进程生命周期 hook。slave 退出探测改为：slave 上报自身 PID → coordinator PID 轮询（`process.kill(pid,0)`）+ done beacon 双保险。

### 核实 5.3：LLM 不能自打 slash command

`client.session.command` 可程序化触发，但 LLM 在对话中无法自己打 `/loop`。slave 进入 /loop 两条可行路径：① coordinator 构造 `task:` frontmatter 经 `--prompt` 注入（主）；② `session.command({ command: "loop" })` 程序化触发（备）。

### 核实 5.4：compaction 不删存储消息

`messages.transform` hook 拿的是 `filterCompacted` 后的切片，不是全量。万象阵 DAG 长生命周期，compaction 跨越概率高，**不能复用 transform-slice 路径**。SSOT 改为 NDJSON。

### 核实 5.5：frontmatter 数组解析

万象术 Kernel frontmatter 解析器只认标量。万象阵自带 codec，Shell 层用 `yaml` 包全量解析 `depends_on` 序列。

### 核实 5.6：`opencode tui --prompt`

`packages/opencode/src/cli/cmd/tui.ts:99` 证实支持 `--prompt`。slave 初始 prompt 由 CLI 注入，消除 session 时序复杂度。

### 核实 5.7：git worktree 共享 `.git`，无 origin

所有 worktree 共享主仓库 `.git` gitdir。`master` 是本地分支，对所有 worktree 可见。PRD 全文 `origin/master` → `<masterBranch>`，删除所有 `git fetch`。

### 核实 5.8：opencode 原生 Worktree 服务

`packages/opencode/src/worktree/index.ts` 证实有 `Worktree` 服务，但 Effect 架构难直接调用。万象阵用 `child_process` 直接跑 `git worktree` 命令。

### 核实 5.9：masterBranch 解析

不硬编码 `master`。启动时 `git rev-parse --abbrev-ref HEAD` 取当前分支（可被 AGENTS.md 覆盖）。ff 前校验"仍在 masterBranch + worktree clean"。

### 核实 5.10：配置来源

只用 AGENTS.md frontmatter，万象阵 Shell 层自行解析 `squad:` 节。

## 决策汇总

| # | 决策点 | 选择 |
| :--- | :--- | :--- |
| 1.1 | slave 提交 | 工具调用触发 ff 检查，拒绝时返回提示 |
| 1.2 | DAG 拆解 | coordinator LLM 拆 |
| 1.3 | SSOT | `.wanxiangshu.ndjson`（万象阵事件行） |
| 1.4 | 通信 | slave 发起短连接 |
| 1.6 | slave 权限 | 乐观 git 约束 |
| 1.9 | prompt 注入 | `opencode tui --prompt` CLI |
| 1.10 | 崩溃处理 | slave=done；master=slave idle |
| 2.1 | 事件与 LLM | 后台默认静默 |
| 2.2 | review/submit | `do { /loop } while (!ff)` |
| 2.3 | done 语义 | 形式主义 |
| 2.4 | 重试上限 | 无限 |
| 2.6 | DAG 依赖 | merged 后才 fork |
| 3.1 | 并行竞争 | 后到者 rebase |
| 3.2 | 清理 | merged/done 删 |
| 3.3 | /squad-kill | 保留现场 |
| 5.1 | HTTP server | Node `http.createServer` + `listen(0)` |
| 5.2 | slave 退出探测 | PID 轮询 + done beacon（无 child-exit hook） |
| 5.3 | /loop 触发 | `task:` frontmatter 经 `--prompt` 注入 |
| 5.4 | SSOT 重放 | NDJSON fold + git 校正 |
| 5.5 | frontmatter 数组 | Shell 层 yaml 包全量解析 |
| 5.6 | slave 自动开工 | `opencode tui --prompt` CLI |
| 5.7 | git rebase/ff | 本地 `<masterBranch>`，无 fetch |
| 5.8 | Worktree | `child_process` 跑 `git worktree` |
| 5.9 | masterBranch | 启动时 `git rev-parse --abbrev-ref HEAD` |
| 5.10 | 配置来源 | AGENTS.md frontmatter |
