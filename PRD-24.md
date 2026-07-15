# E2E 测试设计要求：Flow-first 验证基础设施

## 一、Flow 管线位置

E2E 测试是 Flow-first 架构（PRD-00）的端到端验证层。它验证完整管线 `Host events → Channel → scanCommit → append → publish → provider → tool result → feedback` 在真实宿主中的行为正确性。

E2E 测试不验证纯函数内核（那些用单元测试覆盖），而是验证 Host adapter 归一化、per-session mailbox 串行化、lease 校验、projection 恢复等 Shell 层行为。

## 二、时间预算、网络与套件边界

E2E 必须使用按等待对象分类的超时，而不是一个适用于所有操作的全局值。超时是测试失败的安全网，失败信息必须包含操作、宿主、session 和调用栈：

* **本地事件观察**（`fireEvent`、`waitForCalls`、`waitForNdjson` 等）：1s；
* **本地 Host API**（`sendPrompt`、`runCommand` 等）：3–5s，按接口契约选择；
* **宿主 bootstrap**（冷启动、动态端口、插件就绪和 warmup）：10–20s，按宿主的已知启动预算选择；
* **外部网络**：正确性套件禁止访问外网，必须使用 fake provider。确需网络的实验不属于这些 E2E 门禁。

四个宿主在 30s 内完成一轮是**性能目标**，不是 correctness gate；机器负载或冷启动差异不得使正确性测试失效。CI 分为 `quick`（本地事件和 Host API 的核心路径）、`full`（完整 Flow-first 矩阵）和显式的 `restart`（恢复/崩溃路径）套件，各自使用上面的分类超时。

正常的 `quick`/`full` 套件对每个 Host 只启动一个受管实例，在用例间复用它但不重启 Host。任何用于测试行为的 kill/restart 仅允许出现在显式的 `restart` 套件；不得把重启隐藏在普通 suite 的 setup、teardown 或测试用例中。普通 suite 的 teardown 仅执行资源清理，不构成重启测试。

## 三、宿主常驻单例设计（Host Singleton Lifecycle）

### 3.1 架构拓扑

```text
E2E 启动 (tests/e2e.js)
  │
  ├──► 全局 Setup: HostSingletonManager 启动
  │       ├──► OpenCode 宿主 (spawn once, listen on Port A)
  │       ├──► Mimocode 宿主 (spawn once, listen on Port B)
  │       ├──► Mux 宿主      (spawn once, listen on Port C)
  │       └──► OMP 宿主      (spawn once, listen on Port D)
  │
  ├──► 顺序/并发跑完所有正常测试用例 (Tests.fs, MuxTests.fs, etc.)
  │       ├──► 用例 1: 复用 Port A 实例 (创建新 sessionID)
  │       ├──► 用例 2: 复用 Port A 实例 (创建新 sessionID)
  │       └──► 用例 3: 复用 Port C 实例
  │
  └──► 全局 Teardown: HostSingletonManager 统一销毁
          └──► 对受管 PID/进程组先 SIGTERM，等待有界时间后才 SIGKILL & 清理临时环境
```

`restart` suite 使用同一套 Host 管理器但拥有显式的 stop/start 阶段；只有该 suite 可以验证重启后的恢复。崩溃 suite 必须对已记录的 PID/进程组执行 direct kill，以模拟崩溃，不得用模式匹配杀进程。

### 3.2 隔离环境与缓存

每次 suite 使用随机临时目录作为 `HOME`，禁止继承开发者的 `HOME`、用户配置或用户缓存。缓存加速只能通过显式挂载专用的、只读的 NPM/Bun 缓存目录完成；不得挂载整个 HOME，也不得让宿主写入这些缓存：

```javascript
HOME: path.join(tempRoot, "home"),
NPM_CONFIG_CACHE: path.join(tempRoot, "cache", "npm-ro"),
BUN_INSTALL_CACHE_DIR: path.join(tempRoot, "cache", "bun-ro"),
```

`cache/npm-ro` 和 `cache/bun-ro` 必须来自测试专用 fixture，并以只读方式挂载；下载或生成临时内容时使用 `tempRoot/cache/rw`，而不是开发者目录。`XDG_CONFIG_HOME`、`XDG_DATA_HOME`、`XDG_STATE_HOME`、`XDG_CACHE_HOME` 以及 `OPENCODE_TEST_HOME` 等写路径也必须位于本次 suite 的临时目录中。

## 四、多用例逻辑沙盒隔离（Logical Sandbox Isolation）

由于所有测试用例共享同一个常驻的宿主进程及其实体工作目录，若多个用例并发对工作区进行写操作，或者共用同一个数据库记录，必定会导致状态撕裂。必须在共享宿主下实现高度的逻辑沙盒隔离。

### 4.1 会话隔离（Session ID Isolation）

* 每一个测试用例在开始时，必须向常驻宿主发起 `POST /api/session` 请求以获取一个全新的、随机生成的 `sessionID`。
* 严禁直接使用或复用共享的默认 session。所有的提示词请求和状态变更事件，必须通过该特定的 `sessionID` 进行精准的逻辑路由。

这与 Flow-first 架构的 per-session mailbox（PRD-02）天然对应：每个 sessionID 拥有独立的串行邮箱和独立的 projection（PRD-09），测试用例之间的状态不会互相干扰。

### 4.2 工作区子目录逻辑分区（Logical Workspace Partitioning）

* 为了防止测试文件在同一个 `{workDir}` 根目录下发生脏写，每一个测试用例在调用文件读写工具时，其操作路径必须被逻辑封装在以该用例 `sessionID` 命名的独立子工作区内：

```text
[共享工作区] /tmp/wanxiang-e2e-XXXX/workspace/
  ├── [用例 A 子目录] sess_0ae74ba9.../
  │     ├── test.txt
  │     └── outputs/
  └── [用例 B 子目录] sess_0bf85cb1.../
        ├── src/
        └── outputs/
  └── .wanxiangshu.ndjson  # workspace 的唯一 production journal
```

* 插件边界与 Host Codec 在拦截或路由文件系统 IO 时，应自动完成此子工作区路径的前缀补全，从而消除用例之间的物理竞态。

生产拓扑是**每个 workspace 一个 journal**，并由该路径唯一的进程内 `JournalWriter` 串行写入；跨进程访问再由 interprocess lock 保护。session 子目录不得被解释为生产 journal 拓扑。测试可以为 fixture workspace 建立一个 `.wanxiangshu.ndjson`，各 session 通过 workspace/path 路由到同一 writer。

## 五、受管进程生命周期与孤儿进程防范策略

长寿命常驻宿主一旦由于异常流产或强制中止未被清理，会导致本地端口持续占用和 lock 文件残留。必须实施全封闭的清理防线。

### 5.1 全局同步析构（teardownSuite）

在所有正常用例跑完后，测试入口必须显式执行全局析构，释放排他锁 `E2E_LOCK`，并按记录的 PID/进程组逐个停止受管 Host：发送 `SIGTERM`，等待一个有界的 graceful-shutdown 窗口；仍未退出时只对该 PID/进程组发送 `SIGKILL`。正常 teardown 不执行 restart。

### 5.2 进程出口兜底捕获（process.once('exit')）

`HostSingletonManager` 必须在加载的第一时间向当前 Node 进程挂载事件监听器。当主测试进程由于任何预期或非预期的原因（如 `RUNALL_FAILED`、未捕获的 rejection、用户手动中断）即将退出时，必须对已记录的宿主 PID/进程组执行同样的 SIGTERM→有界等待→SIGKILL 兜底。此出口处理只负责回收受管进程，不得启动或重启 Host。

不得使用按名称或命令行模式匹配的批量 kill 命令。

### 5.3 物理文件锁与 PID 记录

E2E 的排他锁文件（`E2E_LOCK = '/tmp/wanxiang-e2e.lock'`）的获取必须处于 `HostSingletonManager` 的顶层 Setup 中。成功获取 lock 后，管理器必须记录每个 spawn 的 PID、进程组 ID、端口和退出状态；发现旧的 lock 元数据时只能校验其 PID 是否仍为受管进程，并按 PID/进程组执行有界的 SIGTERM→SIGKILL 清理。绝不能按名称或命令行模式清理其他进程。

## 六、E2E 测试与 Flow 管线的映射

E2E 测试用例应覆盖 PRD-02 到 PRD-08 中定义的必测场景，以真实宿主行为验证 Flow 管线各环节。

| PRD 问题 | E2E 验证目标 | 关键断言 |
| :--- | :--- | :--- |
| PRD-02 Esc | Abort committed 后旧 lease 不再产生新 Host dispatch；已开始 dispatch 收到 abort 请求且迟到结果不改状态 | dispatch claim/调用计数、Host abort 记录、`StaleEffectIgnored` 事件 |
| PRD-03 控制字段 | 下游参数中控制字段出现次数为 0 | execute args 中无 warn 字段 |
| PRD-04 budget | 达到真实增长阈值后触发 | synthetic nudge 出现在 outbound |
| PRD-05 日志 | stdout 中 `DEBUG:` 数量为 0 | stdout/stderr 捕获断言 |
| PRD-06 compaction | compaction 完成后普通 nudge 数量为 0 | nudge 调用计数 |
| PRD-07 model | nudge 使用当前真人轮次模型 | outbound request model 字段 |
| PRD-08 review | review nudge 包含 `original_task` | outbound front-matter 解析 |
| PRD-21 行为修复 | warn 缺失不拒绝、todowrite 短报告不拒绝 | tool success + 批评 marker |

### 6.1 测试用例间的 NDJSON 隔离

正常套件只在受管 Host 上创建隔离 `sessionID`，不执行 kill 或 restart。重启恢复测试（PRD-10）必须放在 `restart` suite：显式停止 Host、重新启动并从 workspace journal fold，随后断言 projection 恢复一致。

### 6.2 验证 projection 恢复

E2E 测试应覆盖 PRD-09 六个投影的恢复路径：
* 写入若干事件到 workspace journal；
* 在 `restart` suite 显式停止并重启宿主进程（复用 HostSingletonManager 的受管 spawn 通道）；
* 从 NDJSON fold 恢复所有投影；
* 断言 HumanTurnProjection、CancellationProjection、ContinuationProjection、CompactionProjection、ContextBudgetProjection、ReviewProjection 状态正确。

### 6.3 验证跨 Host 一致性

PRD-12 要求 OpenCode、Mimocode、Mux、OMP 行为一致。E2E 测试框架的 HostSingletonManager 天然支持跨宿主验证：同一组输入依次发送到不同宿主实例，断言结果一致。

## 七、禁止行为

* 禁止在用例内部 `spawn` 新宿主进程（违反 Single-Start Singleton）。
* 禁止复用默认 session（违反 Session ID Isolation）。
* 禁止用例之间共享工作目录（违反 Logical Workspace Partitioning）。
* 禁止不带分类超时的异步等待：本地事件观察使用 1s，本地 Host API 使用 3–5s，bootstrap 使用 10–20s。
* 禁止在 E2E 中依赖 `sleep` / `setTimeout` 做正确性保证（PRD-02 lease 校验同理）。
* 禁止在正常 suite 中 kill/restart Host；仅 `restart` suite 可执行显式停止、启动或崩溃模拟。
* 禁止使用名称匹配或命令行模式匹配的批量 kill 命令清理进程；必须使用已记录的 PID/进程组，并按 SIGTERM→有界等待→SIGKILL 处理（crash suite 使用 direct kill）。
