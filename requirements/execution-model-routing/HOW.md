# execution-model-routing — HOW

## 架构与核心机制

`execution-model-routing` 通过单向流水线将 MJS 策略与 Host 消息拦截打通：

```text
~/.config/opencode/wanxiangshu.mjs (唯一策略权威)
       │ (route: role, running, previous -> target | null)
       ▼
ModelRoutingRuntime (进程单例，管理 Lease multiset 与 Capacity Token)
       │
       ├──► chat.message hook (物理准入: (SessionId, PhysicalUserMessageId) -> ModelTarget)
       └──► messages.transform hook (容量仲裁: Lineage Token 借用与 Step Fence 拦截)
```

1. **Bootstrap 与 MJS 策略加载**：
   - 进程启动时探测 `~/.config/opencode/wanxiangshu.mjs`，若缺失则以原子方式写出内置推荐模板文件并加载。
   - 加载后保持函数引用，不维护多级 runtime 兜底策略。

2. **物理准入与租约管理**：
   - 调度请求仅在 Host `chat.message` 阶段触发，根据 `(SessionId, PhysicalUserMessageId)` 绑定目标模型并修改 Host message。
   - 新物理消息到达时原子取代并取消旧 pending demand；`null` 返回值进入等待队列并在租约归还时事件驱动重试。

3. **Lineage 令牌借用与召回**：
   - 真实 Token Ledger 记录全局占用；Borrowing Decorator 维护 session 派生树。
   - 子节点在 step 级别借用祖先等待中的 token，并在祖先恢复或 step 终结时按序归还。
   - `ToolRegistry` 在 managed tool invocation 的最外层先调用 `SessionExecutionBinding.endProviderStepAtToolBoundary`。后者只从当前 provider-attempt binding 取得冻结的 `PhysicalUserMessageId`，并结合 Host tool context 的 exact `ProviderRunIdentity` 调用 `ModelRouting.endProviderStep`；随后才进入 Strength/Role/Capability gate 与实际 tool body。这样同步 delegate、output distillation 等“工具内等待子模型”的路径不会把调用者 capacity 一起锁住。
   - 该 handoff 纯因果、时间无关；测试只推进显式状态边界，不使用 sleep、deadline 或真实 timeout。真实 `InFlight` provider step 仍不可被 borrower 越权并发使用。

4. **Physical terminal 证据收敛**：
   - `HostEventCodec` 把 assistant completion 分成 provider-step terminal 与 physical-execution terminal 两层。
   - physical terminal 只接受明确最终 `finish`：`stop | length | content-filter`；`tool-calls | unknown | error` 与显式 assistant error 仅结束 step。OpenCode 的 upstream stream failure 可落盘为 `completed + finish="unknown"` 且无 `error`，因此禁止用“非 tool-calls”反推 physical completion。

5. **Durable admission 与 fenced capacity**：
   - `managed-chat-execution` 提供 exact `Accepted` witness 后，runtime 才建立 bounded `PendingDemand` 或调用 acquire。
   - acquire 原子签发 opaque `CapacityFence`；execution binding 保存同一 fence identity，Host projection 只读取已建立 binding。
   - queue 以 typed capacity/supersession/session events 推进；满载产生 `CapacityQueueFull`，不产生 provider retry/fallback。
   - settlement 解释 `execution-failure-policy` 的 typed command，并以 exact fence 做 retain/release/transfer 的单次原子比较；不提供 count-based cleanup、timer expiry 或 session-wide release。

6. **Immutable snapshot 与 fail-closed reconciliation**：
   - capacity owner 在同一串行化边界复制 ledger、token state、exact custody、execution、waiter、lineage 与 transition counter，surface 递归冻结该值，不泄漏 dictionary、queue node 或 mutable handle。
   - `CapacityReconciliation.decide : CapacityInvariantEvidence -> CapacityReconciliationDecision` 只比较 canonical evidence；合法状态返回 `NoOp`，ledger/map、owner/custody、state count 或 counter 不可能态返回 typed `FailClosed`。该函数不持有 runtime，因而不能 repair、清 counter/config 或推进 queue。
   - commit、release、cancel 的唯一边界 outcome 为 `Applied | AlreadyApplied | StaleFence | Conflict`；同一 counter owner 只按后三类单调累加 duplicate/stale/conflict。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EMR-001 | `requirements/execution-model-routing/tests/scheduler-module-config.test.mjs` |
| EMR-002 | `requirements/execution-model-routing/tests/scheduler-module-config.test.mjs` |
| EMR-003 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs`；`requirements/execution-model-routing/tests/capacity-restart-soak.test.mjs`；`requirements/execution-model-routing/tests/process-shared-routing.test.mjs` |
| EMR-004 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` |
| EMR-005 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs` |
| EMR-006 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` |
| EMR-007 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs`；`requirements/host-boundary/tests/provider-retry-host-edge.test.mjs` |
| EMR-008 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs` |
| EMR-009 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs`；`requirements/execution-model-routing/tests/process-shared-routing-output.test.mjs` |
| EMR-010 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs`；`requirements/execution-model-routing/tests/tool-provider-step-boundary.test.mjs`；`requirements/execution-model-routing/tests/capacity-soak.test.mjs` |
| EMR-011 | `requirements/managed-chat-execution/tests/admission-transaction.test.mjs` |
| EMR-012 | `requirements/execution-model-routing/tests/execution-admission-lease.test.mjs`；`requirements/execution-model-routing/tests/capacity-lifecycle.test.mjs` |
| EMR-013 | `requirements/execution-model-routing/tests/admission-queue.test.mjs` |
| EMR-014 | `requirements/execution-model-routing/tests/capacity-reconciliation.test.mjs`；`requirements/execution-model-routing/tests/capacity-soak.test.mjs` |
| EMR-015 | `requirements/execution-model-routing/tests/diagnostics.test.mjs` |
