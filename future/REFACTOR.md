# 待完成的重构项（已完成项已移除）

# 一、仍未收口的系统级问题

### 1. 内核仍未彻底摆脱动态边界
* `Kernel/` 下仍有多处 `Dyn.get` / `Dyn.str` / `Dyn.truthy` 调用。
* 当前已把一部分入口收敛到类型边界，但 `CapsFormat.fs`、`SessionText.fs`、`TreeSitterKernel.fs`、`JsBoundary.fs` 等仍直接依赖动态对象。

### 2. JS `obj` 入口尚未系统记录化
* `Registration.fs`、`Hooks.fs`、`Delegate.fs`、`MuxSlashCommands.fs` 等入口仍以裸 `obj` 读字段。
* 目前只完成了局部整理，尚未统一收敛为强类型 F# record / decoder。

---

# 二、具体未完成条目

## Kernel/

### 1. `Boundary.fs`
* `SessionId` / `WorkspaceId` 已开始进入 `UnifiedContext`，但仍未在所有调用入口强制推行。
* `NudgeHook.fs`、`Session.fs`、`ReviewRuntime.fs`、`MuxSlashCommands.fs` 等仍广泛以 `string` 传递会话/工作区标识。

### 2. `Dyn.fs`
* 动态辅助工具仍未被彻底限制在外壳层。
* 目前内核中剩余的动态访问主要集中在消息/宿主协议解包逻辑，仍需继续外移到 decoder 或 boundary 模块。

## Mux/

### 1. `Dedup.fs`
* `foldMuxReadPartsIntoSeenByPath` 的核心嵌套已降低，但整个模块仍深度侵入动态 `parts` 结构。
* 仍缺少针对“协议形状变化”的集中 decoder；上游消息格式变更时依旧可能静默失效。

## Opencode/

### 1. `NudgeHook.fs`
* 已拆出纯状态机，但 Detached Send 仍未变成单一更新信道。
* 当前仍可能出现：会话在外部 I/O 期间被清理，返回后旧流程再对状态做补写。
* `collectSnapshot` 仍直接依赖宿主私有 client 调用，尚无防抖与重试退避。

## Shell/

### 1. `ExecutorJavascript.fs`
* `rewriteJavascriptModuleSpecifiers` 仍通过字符串区间替换重写 import specifier。
* 虽然现在先用 `es-module-lexer` 定位静态 import，但本质上仍是源码重写策略，不是 AST 级结构化变换。

---

# 三、后续重构方向

### 1. 以类型彻底收口边界
* 为宿主 `event`、`tool args`、`message parts`、`runtime config` 建立统一 decoder。
* 将 `SessionId` / `WorkspaceId` / `CallId` 推到所有入口与状态存储层。

### 2. 继续缩减内核中的动态协议知识
* 将 `CapsFormat`、`SessionText`、`TreeSitterKernel`、`Dedup` 中剩余的动态字段读取外移到 boundary 层。

### 3. 收口 Nudge 的并发模型
* 将 `NudgeHook` 的 Claim、Snapshot、Send、Record 串到单一顺序信道中，彻底避免旧流程回写新状态。

### 4. 将 JS 重写改为结构化变换
* 用 AST 或更强约束的模块解析方式替代 `rewriteJavascriptModuleSpecifiers` 的源码片段替换。
