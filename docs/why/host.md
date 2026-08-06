# Host — 理由

碎片事件拼真相会把因果绑在传输噪声上；粗粒度唤醒 + SDK snapshot 把真相源固定在完整消息。

Compaction 预防+收容分层：预防依赖上游键名会漂，收容只认 transcript 事实，故收容是主防线。关掉配置单独不算已证明——必须首轮探测。

Transform input 为空对象是 Host 能力现实；绑定必须用「已创建未完成 assistant」因果读，不能猜。Canary 用 journal 代理等式，因为 transform 内存 id 与 ToolContext 不共盘。

多实例按 directory 分叉时，跨实例共享的只能是身份注册表，不能是 Journal writer——实测第二实例读不到主实例 verdict 注册即来自此边界。

---

## 决策理由与被拒方案（A4 设计理由）

### 1. LoopKill 终态映射：一律视作 Abandoned vs 二阶段 Armed 识别 (红线闭环)
- **被拒方案**：无条件把 Host 暴露的 `TurnAborted` 映射为 `TurnAbandoned`（会导致 LOOP 强杀引发的取消被误判为用户手动取消，从而无法自动推进 Fallback 恢复槽）；或者将 `TurnAborted` 作为 DU 直接透传进领域层（违反 `EXEC-020` 的 Agent 终态代数 `Completed | Failed | Abandoned`）。
- **选择方案**：Reconciler 消费 `TurnAborted` 时优先拦截进程内 `LoopKillArmed` 标志。若 Armed 命中，清除标志并将事件转换为 `TurnFailed("LoopDetectedKill")` 推进 FallbackController；若未命中，转换为 `TurnAbandoned(UserOrSystemCancelled)`。

### 2. HOST-012 多实例共享并发：就地修改字典 vs 不可变 Swap 快照 (C2 并发安全)
- **被拒方案**：在模块级全局单例上直接使用可变 Map/Dictionary 就地修改（In-place mutation），或者使用裸对象加锁。在 Node.js 微任务/事件循环并发交错下，跨 Worktree 的并发读写会导致严重的 Lost Update 与遍历异常。
- **选择方案**：共享字典（`SessionParents`、`VerdictSessions`、`SessionDirectories`）统一采用 Thread-safe Immutable Swap 机制。写操作生成新 Map 并执行原子 CAS / 指针替换；读操作使用不可变 Snapshot 引用。

### 3. 多实例状态管理：全局单例 vs PluginRuntimeScope 隔离
- **被拒方案**：全部使用全局单例（会导致两个 worktree 实例互相覆盖 Journal 句柄与 Companion 缓存）或全部使用每实例隔离（会导致第二 worktree 读不到父会话关系与 Verdict 注册）。
- **选择方案**：分类治理：跨实例关联表（`SessionParents`、`VerdictSessions`）使用模块级单例共享，每 Worktree 运行时状态（`AgentJournal`、`Companions`、`OwnedSessions`）封入 `PluginRuntimeScope` 隔离。

### 4. 真相源：碎片事件积分 vs 粗粒度唤醒 + snapshot
- **被拒方案**：流式碎片积分。流式碎片顺序/形状随 Host 版本漂，把因果绑在传输噪声上（ARCH-002）。
- **选择方案**：唤醒后读取完整 SDK snapshot 固定真相源。

### 5. Compaction 收容：配置关闭 vs 运行时探测 + 收容
- **被拒方案**：仅依赖配置关闭。上游键名可能漂移，预防层不可单独证明（HOST-006 必须首轮伪-run 为零，否则启动失败）。
- **选择方案**：收容层把任何观察到的 compaction 转为 `ContextReanchored`，作为主防线——只认 transcript 事实。

### 6. ProviderRunIdentity 绑定：唯一未完成 assistant 因果读 vs same-root 猜测
- **被拒方案**：基于 same-root 猜测。Host 重排消息时假绿。
- **选择方案**：因果读（role=assistant、completed 未设、parentID 匹配、id 最大，命中 0/≥2 放弃写 seal → fail closed）。宁可放弃 seal，不赌唯一胜出（REVIEW-003 双 PERFECT 依赖此边界）。

### 7. 跨实例：Journal 共享 writer vs 身份注册表共享
- **被拒方案**：共享 writer。实测第二实例读不到主实例 verdict。
- **选择方案**：只共享不可变身份注册表，避免折叠写盘。
