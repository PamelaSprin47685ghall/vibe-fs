# E2E 测试设计要求

## 1. 硬性时间预算与约束指标

为彻底杜绝 E2E 测试在持续集成与本地执行中的偶发性超时（Flaky）与性能溃烂，测试体系的设计必须无条件服从以下四项时间预算硬约束。任何破坏以下不变量的提交均无法通过架构守卫与 CI 门禁：

* **单次异步等待超时 $\le$ 1s**：
  在 E2E 测试用例中，所有涉及网络请求（如 `sendPrompt`、`runCommand`）、事件触发（如 `fireEvent`）以及逻辑状态检测（如 `waitForCalls`、`waitForNdjson`）的异步等待，其 F# 侧或 JS 侧的超时上限强制设定为 1000ms（1秒）。必须使用类似 `withTimeoutCustom 1000` 的断言安全网进行包裹。超时未完成即视作测试失败，且必须立即抛出包含调用栈信息的 `TIMEOUT` 异常。
* **进程启动与初始化超时 $\le$ 10s**：
  宿主进程（如 `opencode serve`）从冷启动、绑定动态端口，到完成首个 warmup 提示词、插件模块就绪的完整 bootstrap 流程，必须在 10000ms（10秒）以内彻底收敛。
* **整体 E2E 测试总耗时 $\le$ 30s**：
  单个宿主或跨宿主的所有 E2E 测试用例，从整体套件拉起、批量执行完毕到彻底清理销毁，累计物理执行时间必须严格控制在 30 秒以内。
* **单次启动单例生命周期（Single-Start Singleton）**：
  严禁在不同的测试用例（Case）或套件（Suite）内部重复启动宿主进程。对每一个宿主环境（OpenCode、Mimocode、Mux、OMP），在整个 E2E 测试套件的生命周期中**有且仅有一次**启动机会。所有从属该宿主的测试用例必须一口气（Sequentially or Concurrently）跑完，最后在全局 Teardown 阶段对所有常驻宿主进程进行统一销毁（Tear down）与资源清理。

---

## 2. 宿主常驻单例设计（Host Singleton Lifecycle）

### 2.1 架构拓扑与生命周期控制
重构后的 E2E 测试生命周期将由物理上的“Case 级生命周期”提升为逻辑上的“常驻宿主服务周期”：

```text
E2E 启动 (tests/e2e.js)
  │
  ├───► 全局 Setup: HostSingletonManager 启动
  │       ├───► OpenCode 宿主 (spawn once, listen on Port A)
  │       ├───► Mux 宿主      (spawn once, listen on Port B)
  │       └───► OMP 宿主      (spawn once, listen on Port C)
  │
  ├───► 顺序/并发跑完所有测试用例 (Tests.fs, MuxTests.fs, etc.)
  │       ├───► 用例 1: 复用 Port A 实例 (创建新 sessionID)
  │       ├───► 用例 2: 复用 Port A 实例 (创建新 sessionID)
  │       └───► 用例 3: 复用 Port B 实例
  │
  └───► 全局 Teardown: HostSingletonManager 统一销毁
          └───► 强杀所有常驻进程 (SIGKILL) & 清理 /tmp/ 临时环境
```

### 2.2 宿主启动提速至 10s 内的核心支撑
为确保引导阶段能在 10s 内无条件完成，必须在沙盒化环境配置中采取如下缓存复用策略：
* **显式继承开发机 HOME 缓存**：
  在 `isolatedEnv` 构造中，不再将 `HOME` 环境变量绑定至一个完全空白的临时路径，而必须显式指向物理主机的 `HOME` 目录：
  ```javascript
  HOME: process.env.HOME || process.env.USERPROFILE || home
  ```
  这允许 `opencode serve` 底层调用的 NPM / Bun 机制无条件复用本地全局的包缓存（如 `~/.npm` 目录），从而将插件首次加载时 `@opencode-ai/plugin` 带来的 `arborist reify` / `npm install` 包下载和依赖装配时间从 30s+ 直接压缩至 2s 内。
* **隔离与保护原则**：
  在复用 `HOME` 缓存时，`XDG_CONFIG_HOME`、`XDG_DATA_HOME`、`XDG_STATE_HOME`、`XDG_CACHE_HOME` 以及 `OPENCODE_TEST_HOME` 等用于配置写入、临时数据库存储、快照落盘和日志输出的环境变量，必须依然牢牢绑定在每次随机生成的 `/tmp/wanxiang-e2e-XXXX/` 临时目录下，以保证宿主在读写其配置和数据库（如 `opencode.db`）时绝对不会写入或污染开发者的日常配置文件。

---

## 3. 多用例逻辑沙盒隔离（Logical Sandbox Isolation）

由于所有测试用例共享同一个常驻的宿主进程及其实体工作目录，若多个用例并发对工作区进行写操作，或者共用同一个数据库记录，必定会导致状态撕裂。为此，必须在共享宿主下实现高度的逻辑沙盒隔离：

### 3.1 会话隔离（Session ID Isolation）
* 每一个测试用例在开始时，必须向常驻宿主发起 `POST /api/session` 请求以获取一个全新的、随机生成的 `sessionID`。
* 严禁直接使用或复用共享的默认 session。所有的提示词请求（`sendPrompt`）和状态变更事件，必须通过该特定的 `sessionID` 进行精准的逻辑路由。

### 3.2 工作区子目录逻辑分区（Logical Workspace Partitioning）
* 为了防止测试文件在同一个 `{workDir}` 根目录下发生脏写，每一个测试用例在调用文件读写工具（如 `write`、`edit`）时，其操作路径必须被逻辑封装在以该用例 `sessionID` 命名的独立子工作区内：
  ```text
  [共享工作区] /tmp/wanxiang-e2e-XXXX/workspace/
    ├── [用例 A 子目录] sess_0ae74ba9.../
    │     ├── test.txt
    │     └── .wanxiangshu.ndjson
    └── [用例 B 子目录] sess_0bf85cb1.../
          ├── src/
          └── .wanxiangshu.ndjson
  ```
* 插件边界与 Host Codec 在拦截或路由文件系统 IO 时，应自动完成此子工作区路径的前缀补全，从而消除用例之间的物理竞态。

---

## 4. 强力 Teardown 与孤儿进程防范策略

长寿命常驻宿主一旦由于异常流产或强制中止（Ctrl+C）未被清理，会导致本地端口持续占用和 lock 文件残留。必须实施全封闭的清理防线：

* **全局同步析构（teardownSuite）**：
  在所有用例跑完后，测试入口必须显式执行全局析构，释放排他锁 `E2E_LOCK`，并对所有常驻子进程的 PID 执行 `SIGKILL` 强杀。
* **进程出口兜底捕获（process.once('exit')）**：
  HostSingletonManager 必须在加载的第一时间向当前 Node 进程挂载事件监听器。当主测试进程由于任何预期或非预期的原因（如 `RUNALL_FAILED`、未捕获的 rejection、用户手动中断）即将退出时，必须在退出前的 Tick 中同步强杀所有已 spawn 出来的宿主子进程。
* **物理文件锁加固**：
  E2E 的排他锁文件（`E2E_LOCK = '/tmp/wanxiang-e2e.lock'`）的获取必须处于 HostSingletonManager 的顶层 Setup 中。在 lock 被成功占有且常驻宿主进程尚未拉起前，如果系统存在同名旧进程残余，必须执行一次无条件的 `pkill -f` 物理清理，为新的宿主单例腾出干净的本地环境。
