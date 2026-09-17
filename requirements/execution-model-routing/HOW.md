# execution-model-routing — HOW

## 架构与核心机制

`execution-model-routing` 通过单向流水线将 MJS 策略与 Host 消息拦截打通：

```text
~/.config/opencode/wanxiangshu.mjs (唯一策略权威)
       │ (route: role, running, previous -> target | null)
       ▼
ModelRoutingRuntime (进程单例，管理 Lease multiset 与 Capacity Token)
       │
       ├──► chat.message hook (物理准入: (SessionId, PhysicalUserMessageId, Role, Participant, lender) -> ModelTarget)
       └──► messages.transform hook (容量仲裁: 显式 lender 信用借用与 Step Fence 拦截)
```

1. **Bootstrap 与 MJS 策略加载**：
   - 进程启动时探测 `~/.config/opencode/wanxiangshu.mjs`，若缺失则以原子方式写出内置推荐模板文件并加载。
   - 加载后保持函数引用，不维护多级 runtime 兜底策略。

2. **物理准入与租约管理**：
   - 调度请求仅在 Host `chat.message` 阶段触发，以 IdentitySeed 确立的 fixed canonical Role 作为 MJS 调度输入，将 `(SessionId, PhysicalUserMessageId)` 绑定至解析出的 ModelTarget 并修改 Host message。acquire 输入同时携带显式 participant 与可选的 `lenderSessionId`（后者由 accepted IdentitySeed 的 owner 派生）。
   - 同一 physical execution 重试严格复用已有 target 与 capacity fence，不重新执行调度器，亦严禁在同一 physical 内切换 Role、participant 或 agent；新物理消息到达（fresh physical execution）时原子取代并取消旧 pending demand，并以固定的 canonical Role 重新调度至新 target（仅当旧执行仍是当前活跃执行时，才将其 target 作为 `previous` 供偏好提示），但绝不改变 participant identity。`null` 返回值进入等待队列并在租约归还时事件驱动重试。
   - provider 恢复的失败结算（EMR-017）另以 exact witness 写入一次单次消费的 recovery retry 绑定：该 session 的下一次 fresh admission 优先使用该目标（即使旧执行已释放），随后绑定被消费；poison 后的 provider 不再提供容量。

3. **显式 lender 信用借用与召回**：
   - 真实 Token Ledger 记录全局占用；借用只承认 acquire/reserve 输入中 `lenderSessionId` 显式指定的 lender credit，不存在 ambient 派生树或隐式信用。
   - 借用方在调度时隐藏 lender 的 token（同一 provider 下复用同一 token，不新增 ledger 条目）；跨 provider 的借用需求走普通容量。
   - provider-step waiter 以单调序号统一仲裁；owned、borrowed、ordinary 资格只决定候选 token。同一 token 释放后选择最早的可执行 demand，防止 lender 连续续步饿死已等待的 borrower。
   - `experimental.chat.messages.transform` 入口是容量仲裁与 Step Fence 拦截点：属主为同一 owned credit 再次进入后续 provider step 时，`BorrowingCapacity.reconcileFence` 必须回收该 credit 上任何外来 InFlight/Retiring step（descendant borrow）。回合内借用可阻塞属主；属主 transform 触发即回收。显式 release/retire 仍等待借用方 step 结束。
   - `ToolRegistry` 在 managed tool invocation 的最外层先调用 `SessionExecutionBinding.endProviderStepAtToolBoundary`。后者只从当前 provider-attempt binding 取得冻结的 `PhysicalUserMessageId`，并结合 Host tool context 的 exact `ProviderRunIdentity` 调用 `ModelRouting.endProviderStep`；随后才进入 Strength/Role/Capability gate 与实际 tool body。这样同步 delegate、output distillation 等“工具内等待子模型”的路径不会把调用者 capacity 一起锁住。
   - 该 handoff 纯因果、时间无关；测试只推进显式状态边界，不使用 sleep、deadline 或真实 timeout。真实 `InFlight` provider step 仍不可被 borrower 越权并发使用。

4. **Physical terminal 证据收敛**：
   - `HostEventCodec` 把 assistant completion 分成 provider-step terminal 与 physical-execution terminal 两层。
   - physical terminal 只接受明确最终 `finish`：`stop | length | content-filter`；`tool-calls | unknown | error` 与显式 assistant error 仅结束 step。OpenCode 的 upstream stream failure 可落盘为 `completed + finish="unknown"` 且无 `error`，因此禁止用“非 tool-calls”反推 physical completion。

5. **Durable admission 与 fenced capacity**：
   - `managed-chat-execution` 提供 exact `Accepted` witness（携带 fixed Role/Persona/SelectedAgent canonical participant 证据）后，runtime 才建立 bounded `PendingDemand` 或调用 acquire。
   - acquire 原子签发 opaque `CapacityFence`；exact capacity identity 由 `(SessionId, PhysicalUserMessageId, Role, Participant, ModelTarget, CapacityFence)` 共同构成，严格区分本地 participant 与远端 ModelTarget。execution binding 保存同一 fence identity，且只变更 target/lease，绝不修改 participant identity。Host projection 只读取已建立 binding。
   - queue 以 typed capacity/supersession/session events 推进；满载产生 `CapacityQueueFull`，不产生 provider 重试。
   - settlement 解释 `execution-failure-policy` 的 typed command，并以包含 fixed Role+Participant 与 exact target 的 exact fence 做 retain/release/transfer 的单次原子比较；不提供 count-based cleanup、timer expiry 或 session-wide release。

6. **Immutable snapshot 与 fail-closed reconciliation**：
   - capacity owner 在同一串行化边界复制 ledger、token state、exact custody、execution、waiter、role/participant 身份与 transition counter，surface 递归冻结该值，不泄漏 dictionary、queue node 或 mutable handle。
   - `CapacityReconciliation.decide : CapacityInvariantEvidence -> CapacityReconciliationDecision` 只比较 canonical evidence；合法状态返回 `NoOp`，ledger/map、owner/custody、state count 或 counter 不可能态返回 typed `FailClosed`。该函数不持有 runtime，因而不能 repair、清 counter/config 或推进 queue。
   - commit、release、cancel 的唯一边界 outcome 为 `Applied | AlreadyApplied | StaleFence | Conflict`；同一 counter owner 只按后三类单调累加 duplicate/stale/conflict。

`ChatParamsHook`、`ChatAdmissionTransaction` 及 admission 的两个证明 Surface 静态消费 `SessionExecutionBinding` 的既有 public contract。provider 校验、物理执行绑定／释放与 exact binding count 不再经过动态模块查找、手写 union 或缺失模块时的 no-op／零值。`chat.params` 仍只观察既有绑定，不取得调度 authority；绑定异常仍进入 transaction 的既有 pre-provider settlement。`interaction-authority/tests/chat-params-hook.test.mjs` 与 `managed-chat-execution/tests/pre-provider-settlement.test.mjs` 分别执行真实 hook 和 exact 绑定／释放路径，不以源码函数名匹配代替行为证明。

### 新角色集合路由与 DevOps 模型锁定

- **新角色调度矩阵**：MJS `route(role, running, previous)` 处理 `engineer`、`devops`、`manager`、`orchestrator`、`blogger`。遗留角色名直接返回 `null` 或抛出未识别角色错误。
- **DevOps 绑定持久性**：DevOps 首次 acquire 时建立的 target 记录于道路持久化元数据中，后续所有 resume 请求直接提取该固定 target，绕过常规 MJS 动态重新选型，确保模型锁定。

## SDK 类型编译边界

在 `cd6ef0ded` 上，`OpenCodeContract` 与 `strength-policy` 的宽 Host 引用收窄到零引用 `host-opencode-types`，复用原 `OpencodeTypes.fs/.fsi`，不改 SDK 字段、routing decision 或公开签名。前者仅需 `OpencodeModel`、Identity 与 Outcome；后者十四个源码／签名输入中，只有 `ModelRouting` pair 消费 `OpencodeModel`。SDK wire 类型演进与终端事件、MessagePart、摘要实现分离，不把模型类型搬入无领域 platform。

按 production inventory 的声明递归闭包，端口从 7 项目／28 输入降至 5／22，policy 从 55／338 降至 54／334；policy 仍经 Grounding 等真实依赖编入摘要原语，未宣称完全无摘要。端口与 policy 独立 Fable 编译分别通过 60、372 parsed sources；OpencodeTypes、OpenCodeContract、ModelRouting 签名反向消费者的 flat 并集通过 1426 parsed sources／1388 items，包含实际 admission、binding、bootstrap 和插件装配路径。

既有 `host-boundary/tests/host-session-contract-closure.test.mjs` 的 HOST-BOUNDARY-026 闭包证明分别拒绝端口与 policy 恢复宽 Host 引用，窄引用下通过；不设项目数或源码数新预算。新消费者产物上的 `ModelRoutingSurface.createSdkClientPort/sendPrompt` smoke 观察真实 adapter 交付的 SDK payload：显式模型保留 provider/model，reasoning 投影为顶层 variant；未指定模型时不从 agent 恢复模型。该注入 SDK client 的 smoke 不是真实 Host canary，也不证明全部 capacity 时序；既有正式行为证明入口保持如下。

## 正式行为证明入口

- EMR-001..016: `requirements/execution-model-routing/tests/*.test.mjs`
- EMR-017: `requirements/execution-model-routing/tests/017.test.mjs` 与 `requirements/provider-attempt-recovery/tests/021.test.mjs`
- EMR-018: `requirements/execution-model-routing/tests/018.test.mjs`
- EMR-019: `requirements/execution-model-routing/tests/019.test.mjs`
