# crash-reconciliation — WHY

进程或插件中断会导致 process-local 内存状态完全丢失，但不会自动撤销已经发生的外部物理事实。崩溃恢复必须严格基于持久化的 durable facts 与可信物理观察重新进入普通程序流程，严禁依赖缓存、墙钟时间或日志猜测来推断状态。

**crash-reconciliation 保证：崩溃恢复仅从 durable 事件与可信物理快照重建世界，以 fail-closed 原则闭合恢复，且不发明第二状态机或程序计数器。**

## 核心不变量与张力

- **内存临时性 vs 持久真实性**：进程重启后临时内存状态（armed 标记、permit、waiter）安全消失；所有恢复决策必须由已提交的 Journal 事件与 Host 物理观察共同证明。
- **重入普通程序 vs 协程恢复**：恢复不是恢复协程的「执行到第几步」，而是通过纯函数决策计算出合法状态后，重入普通 workflow 入口。
- **证据严密 vs 宁缺毋滥**：面对未决、冲突或缺失的证据，系统必须显式 fail-closed 停止或等待，严禁猜测继续。

## 违反边界的失败意义

- 重启后必须相信内存残留或日志散文猜测才能继续。
- 外部未决 effect 被当作未发生而产生不安全的重复执行。
- 发明常驻的 `RecoveryStage` 程序计数器代替普通程序入口。
- 在没有 fresh evidence 的情况下自动发出副作用或 continuation。
- 显式 `/continue` 后未将断点信息暴露给 LLM，导致模型在未完成历史中迷失。

## DEPENDS ON

- `durable-events`
- `effect-accounting`
- `structured-workflow`
- `host-boundary`
