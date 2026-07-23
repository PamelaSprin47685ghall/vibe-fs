# 实施计划

---

## 零、两时代

第一：OpenCode only；冻结 Mux/OMP；next 禁 import 旧生产。  
第二：他宿主；≥3 重复再抽共享。

---

## 一、冻结路径

Mux/Omp 生产、相关 e2e/tests/docs。

---

## 二、next 工程

```
next/StructuredFlow.fs
next/OpenCode/{Driver,Inbox,PromptProtocol,Journal,Session,Plugin,…}
.wanxiangshu-next/runtimes/<runtime-id>.ndjson   # 运行时产物，非源码
tests-next/GuideContract/
tests-next/JournalIsolation/   # 多 Runtime 场景
```

门禁：无 legacy import；无业务 EventBus；无 Flow AST；无 callOnce 公共 API；无 Nudge/idle；无谓词 Inbox；无共享 Context Flow 并行；  
**无** workspace/session lockfile 依赖；**无** Previous/Fork/Owner 代写路径。

---

## 三、总账

行为 ID；状态字段分类；耦合三问。无旧 NDJSON 兼容。

---

## 四、Phase

|Phase|内容|
|---|---|
|**0**|文稿闭合 + host spike + GuideContract 编译骨架|
|A|Flow 内核|
|B|Host + Gateway + **BootSnapshot + 本 Runtime 日志 CreateNew**|
|C|codec + Origin|
|D|Tools + CommandPort|
|E|MessageTransform|
|F|Journal：Frontier / k-way merge / 单写 / 损坏单源降级|
|G|Process|
|H|Session + Driver + 本地 PromptProtocol|
|I–P|Todo…万象阵|

### Phase A 详细出口

Phase A 只验证 Flow 闭包执行基座，不实现 OpenCode、Journal、Prompt 或 Session 业务：

1. `Flow`、`Bind`、`Delay`、`While`、`For`、`Using`、`TryFinally` 真编译；
2. `ProgressGuard` 不依赖肥胖 Context；
3. 通用 `Flow.run` 不吞 OCE，领域 run 才映射取消；
4. 异步释放只走 `Using`，同步 TryFinally 不承诺异步 compensation；
5. `attempt` 不引入伪 `never`；
6. 找到即停和有界重试使用尾递归，不依赖 CE early-return；
7. Task 层 `mapBounded` 保序、全 await、独立 Context、finally 释放 semaphore。

Phase A 的 Context 使用内存 fake；真实宿主和文件故障在后续 Phase 验证。

### Phase B～H 边界

|Phase|必须证明|禁止引入|
|---|---|---|
|B|RuntimeId、CreateNew、BootSnapshot、Hook Post|workspace 总锁、第二 Writer|
|C|DTO、Origin、UserMessageId/parentID|领域读取 `obj`|
|D|ToolContext、绝对 Deadline、CommandPort|Tool 直接写 Session Fact|
|E|固定纯 Transform、幂等|动态 Stage Registry、Transform IO|
|F|Frontier、k-way merge、单写、单源降级|Repair 他人文件、实时 tail|
|G|Spawn 前装 pump、async dispose|嵌套 timeout、fire-and-forget 清理|
|H|本 Runtime Driver、FIFO、local PromptProtocol|跨 Runtime Pending 全局锁、谓词 Wait|

### Phase 0 出口

Phase 0 闭合需满足四项协议清稿、host spike 验证、GuideContract 真实编译与 Journal 26 条隔离测试方案：

1. **四协议清稿修订**：
   - **TurnId 与 LocalEpoch**：`TurnId` 即宿主 `MessageId`（跨 Runtime 可靠持久化锚点），彻底放弃伪造 `humanTurnId` / `sessionGeneration` / `cancelGeneration` 等复合结构；`LocalEpoch` 仅作本 Runtime 内存取消/递增计数，严禁写入持久化 Journal。
   - **Outcome / Error / CommitUnknown 三态契约**：动词与 Journal 返回区分 `Ok` 成功后置、`Error` 明确断言失败与 `CommitUnknown of EventId * JournalFailure`（Journal 写/flush 不确定回执，Poisoned 停写并需重启恢复）；绝对禁止伪 `never`，禁止静默吞没异常。
   - **raw ObservedAt 时间戳**：保留 OpenCode 宿主接收消息时的原始 `ObservedAt` 时间戳；下层严禁重置/篡改 `TimeSpan` Deadline，并在 host spike 中完成消息 ACK 回执与宿主时钟回拨/偏差观测。
   - **HistoricalPromptIndex 与 LocalPromptProtocol**：彻底删除全局 `Wait(predicate)` 与跨进程 `PendingPrompt` 锁，转为本 Runtime 内 FIFO `LocalPromptProtocol` 与 `HistoricalPromptIndex` 本地定位。

2. **OpenCode host spike 验证**：
   - MessageID / ParentID / synthetic 事实生成及时序验证；
   - 消息 ACK 回执确认与 raw `ObservedAt` 宿主时钟回拨/偏差观测；
   - 确认无 workspace/session lockfile、无代写路径。

3. **GuideContract 必须真实编译的类型/签名清单** (`tests-next/GuideContract/`)：
   - **Flow 基座**：
     - `type Flow<'ctx, 'error, 'a> = private Flow of ('ctx -> CancellationToken -> Task<Result<'a, 'error>>)`
     - `type ProgressGuard<'ctx, 'error> = { Stamp: 'ctx -> int64; NoProgress: string -> 'error }`
     - `type FlowBuilder<'ctx, 'error>(progress: ProgressGuard<'ctx, 'error> option)`
     - `val run: 'ctx -> CancellationToken -> Flow<'ctx, 'error, 'a> -> Task<Result<'a, 'error>>`
     - `val fail: 'error -> Flow<'ctx, 'error, 'a>`
     - `val attempt: Flow<'ctx, 'error, 'a> -> Flow<'ctx, 'error, Result<'a, 'error>>`
     - `val mapBounded: int -> CancellationToken -> ('t -> CancellationToken -> Task<'u>) -> 't seq -> Task<'u list>`
     - 领域类型与 run：`SessionFlow<'a>`, `ChildFlow<'a>`, `ProcessFlow<'a>`, `JournalFlow<'a>`, `SquadFlow<'a>`；`SessionFlow.run: SessionContext -> CancellationToken -> SessionFlow<'a> -> Task<Result<'a, SessionError>>` (OCE → `SessionCancelled`)
   - **动词三态与 Outcome**：
     - `type SendOutcome = Delivered of HostMessageId | Retryable of string | AcceptanceUnknown of string * HostMessageId option | Fatal of string`
     - 动词与 Journal 返回三态：`Result<'a, 'error>` 与 `CommitUnknown of EventId * JournalFailure`
   - **消息与身份**：
     - `type TurnId = private TurnId of string` （即宿主 MessageId，持久化身份）
     - `type LocalEpoch = int64` （仅本 Runtime 内存递增，严禁写入 Journal）
     - `type HostMessageId = MessageId`, `type RuntimeId`, `type LocalSeq`, `type DispatchId`, `type PromptKey`
     - `type MessageOrigin = Human of TurnId | PluginGenerated of PromptKey | HostInternal`
     - `type ObservedAt = DateTimeOffset`
   - **Journal 与快照隔离**：
     - `type StreamId = Workspace | Session of SessionId | Child of ChildId | Squad of SquadId | Process of ProcessId`
     - `type Envelope = { RuntimeId: RuntimeId; LocalSeq: int64; ObservedAt: DateTimeOffset; EventId: EventId; Stream: StreamId; TurnId: TurnId option; Fact: Fact }`
     - `type RuntimeSnapshot = { Frontier: Map<RuntimeId, int64>; Projections: ProjectionSet; OwnRuntimeId: RuntimeId; OwnLocalSeq: int64 }`
     - 归并排序键：`ObservedAt → RuntimeId → LocalSeq`
   - **Driver & Prompt 局部化**：
     - `type LocalPromptProtocol = Map<SessionId, PendingPrompt option>`
     - `type HistoricalPromptIndex = Map<PromptKey, PromptHistory>`
     - `type SessionInbox = abstract TryPost: SessionInboxEvent -> Result<unit, InboxFull>; abstract Receive: CancellationToken -> Task<SessionInboxEvent>`

4. **Status 转换**：
   - 完成四协议清稿、host spike 校验、GuideContract 编译骨架及 26 条隔离测试方案后，方可切换状态为 `Phase 0 已闭合 · 准许 Phase A`。

文档检查必须确认旧语义没有回流：无 workspace/session lockfile、无 Previous/Fork/Owner 代写、无 `PromptKind.Nudge`/`idleProposals`、无 `Wait(predicate)`、无公共 `callOnce(stepId)`、无 for early-return、无下层重置 TimeSpan Deadline。历史反例必须标为 `[FORBIDDEN]`。

---

## 五、Journal 26 条隔离测试方案（Phase F / 集成 exit checklist，为拟设计测试方案，非已实现代码）[NORMATIVE]

*(以下 26 条为 Phase F 集成验证的拟设计测试方案 / exit checklist，非当前已实现测试代码)*

1. 两 Runtime 同 workspace、不同 Session  
2. 两 Runtime 同 workspace、**同 Session**  
3. 同 Session 事件按 ObservedAt 交错重放  
4. 较晚 Todo 快照最终生效  
5. 两侧 Prompt 事实均保留  
6. 运行中 A 不见 B 新增  
7. 第三进程启动见双方已 flush 完整事件  
8. Frontier 后追加不进本次快照  

9. 活跃 Writer 半行被 Reader 忽略  
10. Reader 永不改 Writer 文件  
11. 同 Runtime 时钟回拨仍保 LocalSeq 序  
12. 同时刻 RuntimeId/LocalSeq 结果确定  
13. 一日志损坏仅按有效可见前缀截止（不修改/截断原文件）  
14. Snapshot+own ≡ 对应时间线子序列 Fold  
15. 多次重启归并确定  
16. **不存在** workspace/session lockfile  
17. 每进程唯一 FileStream Writer  
18. 日志不交叉写入  
19. 进程崩溃/Kill 遗留半行不影响其他 Runtime 与重启归并（Reader 自动按有效可见前缀截止，零修改零修复；测试方案）  
20. 同 Session 跨进程陈旧视图合法操作（运行期隔离，下一次重启物理时间线汇合与领域 Fold；测试方案）  
21. 启动时多文件 CreateNew 碰撞自治重试（RuntimeId 碰撞随机换 ID 重试，无 lockfile 死锁；测试方案）  
22. 单源文件损坏全局降级（破坏点后数据忽略并按有效可见前缀截止，其他合法日志正常载入；测试方案）  
23. 本 Runtime 时钟回拨与 Tick 处理（ObservedAt 记录 Raw Wall Time，同一源依靠 LocalSeq 严格递增保序，跨源按 ObservedAt → RuntimeId 破平局归并；测试方案）  
24. Prompt 重复提交在 LocalPromptProtocol 本地拦截（相同 `PromptKey` 本侧 sendOnce，跨进程事实全留存；测试方案）  
25. `CommitUnknown` 重启恢复可重放（重启后结合 `HistoricalPromptIndex` 与 `BootSnapshot` 校验消除重复发单；测试方案）  
26. 禁止任何进程/Reader 修改非己方 `.ndjson` 文件（Reader 仅读稳定前缀，零写零锁；测试方案）  

---

## 六、体量 / 验证 / 切换

同前：看 next 同能力体积；E2E；重启；泄漏；Driver 死锁；AcceptanceUnknown；**多 Runtime 隔离 26 条**。

验证分层：编译（所有 normative 签名）；纯单测（Fold、Progress、PromptKey、Verdict、Fallback）；协议测（FIFO、取消、MessageId、CommandPort）；故障注入（半行、flush、泵死锁、kill、ProjectionBroken）；多 Runtime 集成；架构门禁。调试结论必须落成正式测试，不接受一次性脚本。

---

## 七、纪律

先甜后机制；迁行为不迁架构；迁事实不迁 PC；Phase 0 未闭合不编码业务。  
允许陈旧视图合法操作；拒绝实时一致性无底洞。

---

*KISS-13 终。*
