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

### 8. HOST-016 空 Content 预防：在 transform 阶段兜底 vs 依赖上游网关修补
- **被拒方案**：依赖外部网关（如 OneAPI / NewAPI）或不同 Provider 自身的容错逻辑；各厂商实现不一，遇到严格校验的 OpenAI/DeepSeek 兼容端点必然导致 400 `messages[i].content cannot be empty`。
- **选择方案**：在 `experimental.chat.messages.transform` 管道的末尾（seal 之前）由插件主动做非空 content 兜底，提取 reasoning 文本或注入安全占位符，从根本上杜绝非法空请求体。

### 9. HOST-013 前缀稳定：durable gap anchor 原位 replay vs 移动 / 重定位 marker
- **被拒方案**：每次 transform 删除历史 marker，再把单条 completed tool-result 挪到新位置。旧请求已发送的 marker 会在后续请求中消失或换位，provider-visible 历史不再以前次请求为字节前缀，Prefix Cache 因而失效；裸 tool-result 还依赖外部 anchor 才合法。
- **被拒方案**：删除历史 synthetic 后把全部历史 pair 压缩成 `historyBlock` 放到当前 call/result 批前，或按当前 trailing user / 当前 tool batch 给历史 pair 重新定位。当前 transcript 的形态随时间变化，历史字节随之搬家，前次 wire 不再是后次 wire 的字节前缀；且 renderer 与同构 oracle 可以一起错而测试仍然通过。
- **选择方案**：每次 transform 插入一组自足的 synthetic tool-call + 对应 completed tool-result。pair 一经加入即成为不可变永久历史；每个 synthetic half 的 transcript 位置由它自己 durable 的 gap anchor（`Start` / `Before id` / `After id`）唯一决定，replay 是纯函数逐条注入。当前真实消息中找不到 gap anchor 的 historical pair 不重放（不重定位、不 AbortSession）；XWire prefix probe 的 DropLeading 会合法 drop 已覆盖前缀。旧无 anchor journal fail closed，不猜 ordinal ≈ 第 N 个 tool batch。

### 10. HOST-013 位置：trailing user 之后 vs 之前
- **被拒方案**：把本次 pair 挂在全局末尾（trailing user 之后）。模型看到的顺序变成「先读 user，再出现 tool-call/result」，不像真实 tool 轮次；有多 tool 时更不像 `tool1 tool2 … result1 result2` 批。
- **选择方案**：本次 pair 的 gap 由当前真实消息末端结构决定：有同轮 tool batch 时 `call` 挂 call 批末、`result` 挂 result 批末（bracket 语义：`real calls → synthetic call → real results → synthetic result`）；无 tool 时二者同 gap 相邻并紧挨 user 前；空历史用 `Start`；无 trailing user 时挂末尾。

### 11. HOST-013 范围：全 session 注入 vs 排除 Blogger
- **被拒方案**：对全部 provider transcript（含 Companion Blogger）注入结对编程 auto-injected。Blogger 的唯一任务是把 TOML delta 压成 `blog` 工作日志；中文「以“我”开头」的思考约束与 tip nudge 会污染其 system/tool 合同，导致偏离 `blogger-system.md` 与 ENFORCER 工具纪律。
- **选择方案**：HOST-013 仅作用于非 Companion session。Blogger 身份以 durable `SessionAssociationProjection.isCompanion` 判定，transform 短路跳过 pair 注入与 durable append。

### 12. HOST-013 幂等：placement identity vs 每次 transform 无条件新增
- **被拒方案**：每次 transform 无条件 `ordinal+1` 并 append 一组新 pair，以 `history.Length + 1` 判断“这一定是新 round”。Hook invocation 被错误提升为业务 round identity；Host retry、测试重放、同一 request 重入会凭空多出 pair，前缀性质在无新增真实内容时即被破坏。
- **选择方案**：每个尚未存在 HOST-013 synthetic bracket 的真实 placement occasion 恰好一组 pair；同一 occasion 的重复 transform 只 replay。placement identity = SessionId + CallGap + ResultGap；durable fact 原子携带两个 gap，不拆成两个事实（否则 crash 留下 FakeReq durable、FakeResp 不 durable 的半状态）。

### 13. HOST-013 判定：`isAppendOnlyPrefix` vs 近似断言
- **被拒方案**：只检查 pair 数量、callID 相同、markerText 正确、FakeReq 在 Req 后、FakeResp 在 Resp 后——这些在 Prefix Cache 已坏（历史被搬家）的实现上全部通过；或自己写 `JSON.stringify(next).startsWith(...)` 第二套“差不多是前缀”的 helper。
- **选择方案**：以 `ProviderProjection.isAppendOnlyPrefix` 为 PREFIX LAW 唯一权威判定（比较 provider/model/variant/tools/system 及完整 message prefix），生产前置 proof 与回归测试共用同一函数。

### 14. idle-derived continuation：QuiescenceGate vs 重复读 snapshot / busy 状态
- **被拒方案**：把 busy/running 加进业务 HostSignal 并在几十处维护 if/else——transport 状态机不得搬进 Domain（HOST-002 只允许 coarse wake 进入业务）；连续多读几次 snapshot 称为“仍 idle”——snapshot 只证明观测稳定，不证明发送瞬间仍 idle；给 `TurnUnknown` 加 `Task.Delay`；每发现新 race 加 `isXxxRun` 特判——把 symptom 类别清单当正确性证明，永远补不完。
- **选择方案**：process-local `SessionQuiescenceGate`，状态转换 `BeginProviderAttempt / ObserveIdle / TryConsume / RevokeCurrentAttempt / DropSession`。permit 从 idle 观察随 reconcile 携带到发送边界，物理发送前再次 `TryConsume`（与 dispatcher send 之间零 await）。typed `AttemptAborted` 立即 `RevokeCurrentAttempt` 并以 `AbortWake` 进入 Reconciler；该 wake 永远无 repair/idle rights。业务决策 × 物理发送资格 = 允许的副作用；stale 或 revoked permit → `Superseded`（不写 claim、不发消息）。不写 Journal、不参与 crash recovery、重启清空（安全侧失败）。

### 15. HOST-008 所有权：ExecutionClass × Ownership vs SatelliteKind 单轴
- **被拒方案**：继续以 `SatelliteKind = { Companion, Teacher }` 为唯一所有权模型，并把 SyncInspector /
  SyncCoder / Bookkeeper 硬塞进 SatelliteKind。Dedicated Sync* 是长期 hot-knowledge Work Session，需要
  Companion/context 能力；塞进 Teacher-style leaf/no-Companion 拓扑会在长上下文下撞容量，也与
  Bookkeeper ephemeral leaf 混为一谈。
- **被拒方案**：为 SyncDelegate 复制一套独立 parent/child map，或宣称 Teacher Satellite 已删除后只留新模型。
  G2 只落地所有权正交化与 SyncDelegate 基础；Student/Teacher 删除属后续 gate。
- **选择方案**：分离 `SessionExecutionClass`（Work | InternalLeaf）与 `SessionOwnership`
  （Root | Attached of AttachmentKind）。Dedicated SyncInspector/SyncCoder = Work+Attached（MAY Companion）；
  Companion/Bookkeeper = InternalLeaf+Attached。复用 Teacher 的 Returned→Completion 调用代数，不复用其
  leaf/no-Companion Session 分类。G2 过渡期 Teacher 仍可作为 transitional InternalLeaf 存在（非长期
  AttachmentKind）。HOST-015 物理扁平与恢复 fail-closed 不变：逻辑可嵌套 Attached，物理一律挂 family root。
