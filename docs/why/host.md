# Host — 理由

碎片事件拼真相会把因果绑在传输噪声上；粗粒度唤醒 + SDK snapshot 把真相源固定在完整消息。

Compaction 预防+收容分层：预防依赖上游键名会漂，收容只认 transcript 事实，故收容是主防线。关掉配置单独不算已证明——必须首轮探测。

Transform input 为空对象是 Host 能力现实；绑定必须用「已创建未完成 assistant」因果读，不能猜。Canary 用 journal 代理等式，因为 transform 内存 id 与 ToolContext 不共盘。

多实例按 directory 分叉时，跨实例共享的只能是身份注册表，不能是 Journal writer——实测第二实例读不到主实例 verdict 注册即来自此边界。

---

## 决策理由与被拒方案（A4 设计理由）

### 1. Provider turn 取消与 Agent 终态分型
- **被拒方案**：把 provider `TurnAborted` 直接写成 Agent completion；或在 Reconciler 内过早删除 abort 分类。前者违反 EXEC-020，后者让 LOOP-006 无法区分插件强杀与用户取消。
- **选择方案**：Reconciler 保留 provider-turn `TurnAborted`。消费边界命中 `LoopKillArmed` 时走 provider failure/Fallback；未命中时只终止和清理 turn，绝不构造 Agent `RunCompletion`。

### 2. HOST-012 多实例共享并发：事件循环所有权 vs 未指定 CAS
- **被拒方案**：在没有 Worker、原子引用类型与内存模型的 Fable/Node 边界上宣称“Thread-safe CAS”；该措辞无法实现也无法验证。
- **选择方案**：共享注册表由单一 event loop 所有；每次操作不跨 `await`，跨异步边界先取不可变快照。未来若引入真正并行执行，再先定义消息 owner 或原子端口。

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
