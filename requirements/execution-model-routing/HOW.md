# execution-model-routing — HOW

本文件描述当前推荐实现形状，不拥有产品语义；若实现与 WHAT 冲突，以 WHAT 为准。

## 1. 首次生成的推荐 MJS

`~/.config/opencode/wanxiangshu.mjs` 是普通 ESM，default export 一个同步函数。文件不存在时，bootstrap 原子写出随包发布的 `resources/wanxiangshu.mjs`；下面展示它的推荐策略与风格，实际生成字节以该 resource 为唯一实现源，避免 loader 再维护另一份隐藏默认。

```js
// Wanxiangshu model scheduler. Edit this file freely.
// `running` is a multiset of active { model, reasoning } leases.
// `previous` is the last successful physical execution target for this session,
// or null for a new conversation. It is a preference hint, not occupancy.
// Return a target to acquire it, or null to wait for an occupancy change.

// Provider-level concurrency limits (maximum concurrent active leases per provider).
const PROVIDER_LIMITS = {
  "ollama-cloud": 16,
  "opencode-go": 8,
  "stepfun": 8,
  "cursor": 4,
  "neuralwatt": 4,
}

const DEFAULT_LIMIT = 4

const providerOf = (model) => model.slice(0, model.indexOf("/"))

const isAvailable = (running, model) => {
  const provider = providerOf(model)
  const limit = PROVIDER_LIMITS[provider] ?? DEFAULT_LIMIT
  const count = running.filter((item) => providerOf(item.model) === provider).length
  return count < limit
}

const targetOf = ([model, reasoning]) => ({ model, reasoning })

const pick = (running, previous, candidates) => {
  if (previous) {
    const preferred = candidates.find(
      ([model, reasoning]) => model === previous.model && reasoning === previous.reasoning,
    )
    if (preferred && isAvailable(running, preferred[0])) return targetOf(preferred)
  }

  for (const candidate of candidates) {
    if (isAvailable(running, candidate[0])) return targetOf(candidate)
  }
  return null
}

const FASTEST = [
  ["ollama-cloud/gemma4:31b", "none"],
  ["opencode-go/deepseek-v4-flash", "none"],
]

const FASTEST_II = [
  ["opencode-go/deepseek-v4-flash", "none"],
]

const FASTER = [
  ["stepfun/step-3.5-flash-2603", "none"],
]

const MEDIUM = [
  ["opencode-go/deepseek-v4-flash", "low"],
]

const HIGHER = [
  ["cursor/cursor-grok-4.6-xhigh", "xhigh"],
  ["neuralwatt/glm-5.2-flex", "high"],
]

const FAST_BROWSER = [
  ["ollama-cloud/minimax-m3", "none"],
]

const DEEP_BROWSER = [
  ["opencode-go/minimax-m3", "none"],
]

const pools = new Map([
  ["fast-distiller", FASTEST],
  ["fast-blogger", FASTEST],
  ["deep-distiller", FASTEST_II],
  ["deep-blogger", FASTEST_II],
  ["fast-inspector", FASTER],
  ["fast-bookkeeper", FASTER],
  ["deep-inspector", MEDIUM],
  ["deep-bookkeeper", MEDIUM],
  ["fast-manager", MEDIUM],
  ["fast-orchestrator", MEDIUM],
  ["fast-coder", MEDIUM],
  ["fast-devops", MEDIUM],
  ["fast-inquiry", MEDIUM],
  ["fast-reviewer", MEDIUM],
  ["deep-manager", HIGHER],
  ["deep-orchestrator", HIGHER],
  ["deep-coder", HIGHER],
  ["deep-devops", HIGHER],
  ["deep-inquiry", HIGHER],
  ["deep-reviewer", HIGHER],
  ["fast-browser", FAST_BROWSER],
  ["deep-browser", DEEP_BROWSER],
])

export default function route(role, running, previous) {
  const candidates = pools.get(role)
  if (!candidates) throw new Error(`unknown model-routing role: ${role}`)
  return pick(running, previous, candidates)
}
```

这就是推荐的 MJS 风格：**小纯函数 + 明确常量 + Provider 独立并发上限表 (`PROVIDER_LIMITS`) + 显式 role 表 + provider 聚合计数 + `previous` 优先 + 顺序 `pick` + 满载 `null`**。同一 provider 下无论 model/reasoning 是否不同都合并占用 provider budget；上一 target 只有在仍属于当前候选且其 provider 未满时才优先。不依赖闭包外可变状态，不读时钟/网络/Host，不把模型策略藏进 runtime。

当前推荐模型对应此前七档意图：最快非推理 `ollama-cloud/gemma4:31b` / `opencode-go/deepseek-v4-flash`；快速只读 `stepfun/step-3.5-flash-2603`；常规中档 `opencode-go/deepseek-v4-flash` low；高档 `cursor/cursor-grok-4.6-xhigh` xhigh / `neuralwatt/glm-5.2-flex` high；Browser 分别使用 `ollama-cloud/minimax-m3` 与 `opencode-go/minimax-m3` 的非推理档。Provider 级别的并发上限统一在 `PROVIDER_LIMITS` 表中明确声明。

这些模型名/limit 是**首次生成模板的当前推荐值**，不是 runtime schema。推荐模板把 Provider 级别的并发上限集中在 `PROVIDER_LIMITS` 中；同 provider 的其它 model/reasoning occurrence 统一计入。用户可以直接编辑整个 MJS：删掉七档、换模型、换计数方法、调整各 provider 上限都合法。已有文件在升级时绝不被新模板覆盖。

`model` 必须是完整非空 `provider/model`；裸 `modelID` 非法，因为 provider 同样属于 MJS routing authority。`reasoning` 直接映射 OpenCode 的 reasoning/variant 档位，非推理显式写 `none`。返回值结构之外的策略信息不穿过 ABI。

## 2. bootstrap + loader

进程启动时解析固定路径：

```text
homedir()/.config/opencode/wanxiangshu.mjs
```

若文件不存在：

1. `mkdir ~/.config/opencode`（recursive/idempotent）；
2. 在同目录用 exclusive create 写完整临时文件；
3. 用原子 hard-link/create-if-absent 把该完整 inode 发布为 `wanxiangshu.mjs`；目标已存在 (`EEXIST`) 表示另一 bootstrap 已有 winner，禁止覆盖；
4. 删除临时名；无论自己发布还是别人先赢，最后都从磁盘 import winner 文件。

不要直接对最终路径做 `writeFile(..., flag="wx")`：虽然它防覆盖，但目标名会先出现、内容后写完，另一个进程可能在极窄窗口 import 到半截模块。

这样“推荐默认”只存在于可见、可编辑、持久化的 MJS 文件中；runtime 内存里没有另一套 fallback。目录/写入的其它错误 fail closed。

用 Node ESM dynamic import / file URL 加载；只接受同步 default function。import 成功后保留函数引用到当前 process epoch 结束，不做 file watcher/hot reload。修改 MJS 后重启 OpenCode 获得新策略。

Load Phase 允许 mkdir/create/import 这个配置载体，但不调用 Host session API、不执行任何 model demand。scheduler 本身第一次被调用发生在真实 demand 上。

不再需要 TOML parser，也不需要把 `smol-toml` 因本 feature 移入 runtime dependency。

## 3. 运行时最小类型

推荐只保留这些概念：

```text
ModelTarget = { Model: string OpenCode model selector; Reasoning: string }
ExecutionLeaseKey = SessionId × PhysicalUserMessageId
ExecutionLease = { EffectiveAgent; ModelTarget }
Running = ModelTarget list                        // active execution multiset，重复保留
Previous = ModelTarget option                     // same-session last successful physical target; no capacity
Scheduler = role:string × Running × Previous -> ModelTarget option
PendingDemand = { ExecutionLeaseKey; EffectiveAgent; Previous; cancellation }
```

不再有：

```text
ExecutionLane
ModelLaneConfig
ModelCandidate
MaxSessions
laneOf
firstFree
physicalOccupants:Set<SessionId>
```

所有这些若有需要，都只是 MJS 内部实现。

## 4. process-shared occupancy owner

推荐 Fable 模块级 process singleton，而不是 `PluginRuntimeScope` 字段。现有 Host 会按 directory/worktree 产生多个 plugin instance，`OpenCode.Host.SharedState` 已证明 module-level singleton 是 root/worktree 跨实例共享边界。

registry 至少维护：

```text
managedExecutions: SessionId → { PhysicalUserMessageId option; EffectiveAgent; ModelTarget }
lastPhysicalTarget: SessionId → ModelTarget
pending: SessionId → latest arrival-ordered physical-execution demand
```

`running` 每次调用临时由 active execution lease 表的 values 生成新数组。不要缓存第二份计数 truth；MJS 自己从 multiset 计数。`lastPhysicalTarget` 不计 occupancy，只在成功 physical admission/adopt 时更新；exact terminal 释放 active lease 时保留，session drop/scope cleanup 时删除。session 是否还可 reuse、handle 是否 retired、业务是否 completed 均不进入 active occupancy 的生命周期判断。

同一 SessionId 同时至多一个 current physical execution；不同 SessionId 的并发 execution 若指向同一 target，values 中自然出现重复元素。`PhysicalUserMessageId=None` 只用于 Strength 在 Host 物理 message 创建前的 optional reservation，chat.message 必须把它原位 rebind 成 exact physical id，而不是再占一个 occurrence。

## 5. 串行事件驱动 acquire/release

模型资源 mutation 通过一个 process-wide serial queue/actor 顺序处理，避免并发 scheduler call 基于同一旧 snapshot 同时提交。

### Dispatch/enqueue（不 acquire）

`fork` / continuation / repair 先经 PromptDispatcher 写 claim/authority，再调用 `prompt_async` enqueue。这里仅验证/freeze EffectiveAgent；`OpenCodePromptOptions.Model = None`，不得调用 scheduler，也不得等待 model capacity。`promptAsync` 的 Promise settle 也不属于 fork/tool completion：同步调用成功后即返回 admission-shaped transport receipt；之后若该异步 dispatch promise rejection，acceptance 已未知，直接 process-fatal，禁止自动重发。

### Acquire managed required（唯一入口 = `chat.message`）

Host 已接收该物理 user message 后：

1. 从 Host message 取得 `PhysicalUserMessageId`；从 PromptKey authority（plugin prompt）或真实外部用户选择解析 EffectiveAgent；任一 managed execution 缺 physical id → fail closed；
2. 当前 SessionId 已绑定**同一个** PhysicalUserMessageId → 要求 EffectiveAgent 完全一致并直接复用 target；
3. 当前 SessionId 若绑定旧 physical id / 旧 pending → 原子 retire/cancel；新 physical message 自身就是 supersession 证据，不等待 idle；
4. 若存在 Strength 的 `PhysicalUserMessageId=None` reservation 且 agent 一致 → 原位 adopt 成当前 physical id，不重调 scheduler、不增加 `running` occurrence；
5. 否则生成当前 `running` snapshot，并读取该 Session 的 `lastPhysicalTarget`（不存在为 `null`），调 `route(agent, running, previous)`；
6. target → 校验结构、原子写 exact execution lease，并把 target 投影到 mutable Host message；`null` → 把 exact demand 挂入 pending。

pending acquisition 的结果是封闭 `Acquired target | Superseded`。步骤 3 / owner cleanup 若移除正在等待的旧 demand，只完成 `Superseded`，不制造 `OperationCanceledException`。`chat.message` 收到 `Superseded` 后立即成功返回：不投影 model、不进入 PromptIngress、不 commit execution capability；scheduler/invariant exception 仍原样穿过 fatal membrane。

### Occupancy changed

每次成功 acquire 或 release 后，对 pending 按 arrival order 做一轮：

1. 对当前仍有效 demand 构造最新 `running`；
2. 用 demand 在首次 admission 时冻结的 `previous` 调 scheduler；不得因等待期间其它 occupancy 变化而改写这份接续提示；
3. `null` → 保留该 demand，继续检查后面的 demand，避免不同 role 的 head-of-line blocking；
4. target → 立即提交 occupancy，再处理下一个 demand，使后续调用看到更新后的 multiset。

一轮结束后若仍有 pending，不自行再次循环；等待下一次真实 occupancy event。没有 timer/poll。

普通 provider 完成由 raw Host `message.updated` 的最终 assistant observation 释放：读取 `properties.info.parentID` 作为 exact `PhysicalUserMessageId`，只有它仍与该 SessionId 的 current lease 匹配时才删除 occurrence。assistant error 直接 terminal；正常路径要求 `time.completed` 且 `finish != "tool-calls"`，因为 `tool-calls` 只是同一 physical execution 的中间 provider step。该 observation 是 model-routing 的物理资源边界，不会转成业务 `HostSignal`。这样 A 的 terminal 即使在 B 已经 admission 后才到，也只能尝试释放 A，不能误删 B。

`PluginTransforms.applyContinuationOutcome` 的 `StopPhysicalRun` 从 wire transcript 取 exact trailing `PhysicalUserMessageId`，启动 Host abort 后立即返回，避免 transform callback await 自己的 abort；abort `Ok` 才调用 `ModelRouting.releasePhysicalExecution` 精确清 lease。`SessionIdle` / typed attempt abort 只有 SessionId，继续服务 Reconciler、quiescence、loop/abort 语义，但不直接操作 active model occupancy。session delete / scope cleanup / plugin shutdown 因为整个 owner 已被销毁，可以按 SessionId 强制释放 current lease 并取消 pending。新 PhysicalUserMessageId admission 仍在同一个串行临界区 supersede 旧 lease/pending。业务 completed/retired/join/finality 不直接操作 occupancy；ProviderRetry/ProviderFailure 若仍引用同一 physical user material 则继续复用。

因此同一 SessionId reopen/reuse 不依赖“是否准确监听到 session end”：新 `chat.message` 的 physical identity 足以切断旧槽。

### Optional demand

Strength 这类可丢弃优化应走 nonwaiting reservation：调用一次 scheduler，并同样传该 Session 已知的 `previous`（通常新 replica 为 `null`）；`null` 立即 K0，不进入 required pending queue。若获得 target，先以该 replica SessionId 保存 `PhysicalUserMessageId=None` 的 capacity reservation；reservation 本身不更新 `lastPhysicalTarget`，只有之后真实 `chat.message` 以相同 agent 原位 adopt 成 exact physical execution 时才更新。reservation/physical execution 始终只占一个 occurrence；失败/销毁时按 SessionId 清理。

## 6. Host config 与 managed prompt 路径

旧路径：

```text
opencode.json agent.model
  → ManagedAgentConfig inventory
  → SessionExecutionBinding/OpenCodePort fallback
```

新路径：

```text
plugin/tool dispatch
  → PromptKey + EffectiveAgent + Model=None
  → Host prompt_async enqueue（fork 不等 slot / provider run）
  → physical chat.message(id = PhysicalUserMessageId)
  → ModelRoutingRuntime.acquire(session, physicalUserMessageId, effectiveAgent)
  → mutable Host message.model = explicit lease target
  → Host provider request
```

`OpenCodePort` 不再根据 `Agent` 反查 `ManagedAgentConfig.tryBoundModel`，也不接收 send-time model lease。model routing 只发生在物理 `chat.message` admission；`chat.params` 随后只做 observed-provider validation。

`ManagedAgentConfig` 退回 Host projection/guard owner：把 22 个 managed catalog 名投影到 live Host config（缺则创建），并写入 mode/permission/prompt 等 Wanxiangshu-owned 字段，不再把 Host-final `agent.model` 收进 inventory，也不再要求用户在 `opencode.json` 手写 agent map。若 Host schema 强制要求静态 model，应由 Host adapter 选择一个不具 authority 的合法占位/guardrail，真实发送必须被 explicit lease 覆盖；当前 Host `AgentConfig.model` 可选，因此投影不写 model。该能力需 canary 证明。

## 7. root/user-facing 路径

`chat.message` 是 managed execution 的唯一 required acquisition hook：真实用户 message 先解析用户选中的 EffectiveAgent；plugin synthetic message 则从 PromptKey claim/profile 解析 EffectiveAgent；两者同时必须有 PhysicalUserMessageId。该 hook 先建立 current exact binding，再把输出 message/request 的 model/reasoning 改成 lease target。Host 随后的 `chat.params` 因发生在 messages transform 之前，只验证 chat.message 已记录的 current exact binding；不得把 `input.agent ∈ ManagedAgent.requiredNames` 自身当成 managed admission 证明。CRASH-018 exact disclosure registry 命中的物理 material 从未进入 managed acquisition，故 `chat.params` 直接保持非业务分类并跳过 managed validation/temperature policy。其余 managed request 的实际 provider/model 必须从 Host 已 resolve 的 `input.model.providerID + input.model.id` 观察，reasoning variant 从当前 user message 的 `input.message.model.variant` 观察。不得把 provider-facing `Model` 与 persisted `UserMessage.model` 拼成一个 hybrid target，也不得因 SDK 字段名差异退化成只比较 message model。provider-facing transform 再用 trailing PhysicalUserMessageId（以及 plugin PromptKey）重新证明同一个 execution。

这条 Host mutation 能力必须由 `host-boundary` physical canary 证明，不能只测 DTO 被改了。

外部请求里的 model/reasoning 只可影响非 managed Host 行为；managed agent 一律由 MJS lease 覆盖。

## 8. AABB / Strength

`AttemptExecutionProfile.EffectiveAgent` 保持 provider-attempt-recovery 的唯一 peer/fallback 输出：

```text
A/A → lease(session, physicalUserMessageId_A, SelectedAgent)
B/B → lease(session, physicalUserMessageId_B, PeerAgent)
```

两个 lease target 完全相同合法。model routing runtime 不比较 fast/deep target，也不参与 FallbackCursor。

Strength 不再读取静态 fast/deep model string。它对 `fast-<owner-role>` 做一次 optional scheduler probe；`null` → K0，不阻塞 owner；target 可用才创建/运行 replica。是否仍有成本收益继续归 speculative-investigation，而不是 model router。

## 9. physical execution lifecycle 与进程边界

managed execution lease 的普通释放由 final assistant exact `parentID = PhysicalUserMessageId` 驱动；同 SessionId 新 PhysicalUserMessageId 也会在 admission 内 supersede 旧 execution，因此 correctness 不依赖 end signal 必达。`SessionIdle` / typed abort 只有 SessionId，只能 wake/quiescence/observe，不能删除 active exact lease；session delete / scope cleanup / plugin shutdown 才能按 owner 强制清理。业务 completion/retire/join/finality 不直接释放。进程退出自然丢失 process-local lease registry。

process restart 重新 import MJS 并开启新 routing epoch。本包当前不把 model execution lease 写入 durable event history；跨进程只保留业务 identity/authority，不承诺历史 session reuse 时继续使用上一进程同一个 physical target。

## 10. 主要实现影响面

- 新增 MJS loader + process-shared `ModelRoutingRuntime`；不新增 lane/config schema。
- `ManagedAgentConfig.fs`：删除 Host model inventory authority 与 duplicate-pair-model validation；保留 owned Host fields 投影。
- `SessionExecutionBinding.fs`：拥有 base/override EffectiveAgent + current exact PhysicalUserMessageId binding 的 Host observation validation；synthetic send 保持 model-free，execution lease 由 `chat.message` 获取。
- `OpenCodePort.fs`：删除 `Agent → ManagedAgentConfig.tryBoundModel` fallback。
- `ChatParamsHook.fs` + request mutation hook：managed model/reasoning route/validate。
- `PluginSessionWiring.fs` / Strength：删除静态 inventory model 依赖，optional replica 走 scheduler probe。
- final assistant 的 exact physical terminal 释放匹配 lease；session delete / scope cleanup / plugin dispose 才按 owner 强制清理；Host idle / attempt abort 只提供 wake/quiescence/abort observation，业务 lifecycle 不直接参与 capacity bookkeeping。

## DEPENDS ON

- `participant-identity`：提供 CanonicalRole / Fast·Deep / EffectiveAgent / peer 本体。
- `managed-session-lifecycle`：提供 managed session retire/delete 生命周期边界。
- `host-boundary`：提供 plugin load、root/worktree 多实例与 Host prompt/config 适配边界。

## DOES NOT OWN

- AABB/失败预算/何时切 peer → `provider-attempt-recovery`。
- Role/Persona/权限 → `participant-identity` / `office-capability` / `capability-enforcement`。
- MJS 内具体模型池、容量、成本/能力分类 → 用户调度策略。
- Host SDK/hook 是否真的允许 managed request model/reasoning mutation → `host-boundary` canary。
- provider 本身的远端限流/计费 → 外部 provider；本包只维护当前进程的本地 lease multiset。

## 验证与测试落点

运行命令：包内测试用 `node --test requirements/execution-model-routing/tests/*.test.mjs`；真实 Host provider-wire canary 用 `node requirements/verification-system/tests/e2e/support/managed-model-routing-canary.mjs`。

| 命题 | 落点测试（文件 + 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| EMR-001 sole MJS authority + bootstrap | `tests/scheduler-module-config.test.mjs`：`EMR_001_missing_scheduler_is_created_once_then_loaded_from_disk`（缺失时原子创建 + 立即 import 实际落盘文件）、`EMR_001_existing_scheduler_is_never_overwritten`（已有文件永不自动改写）、`EMR_001_concurrent_bootstrap_keeps_one_atomic_winner_without_merge`（并发 bootstrap 原子 winner，不覆盖不 merge）、`EMR_002_scheduler_program_errors_fail_closed`（invalid module fail closed）；`tests/recommended-template.test.mjs`：`EMR_001_recommended_resource_is_directly_executable_and_uses_full_model_selectors`（发布 resource 可直接执行且全部使用完整 `provider/model`） | NEW | `node --test requirements/execution-model-routing/tests/scheduler-module-config.test.mjs requirements/execution-model-routing/tests/recommended-template.test.mjs` |
| EMR-002 scheduler ABI | `tests/scheduler-module-config.test.mjs`：`EMR_002_scheduler_preserves_running_duplicates_null_and_previous`（`role + running + previous → target|null`、重复项保留、新对话 `previous=null`、接续 target 精确传递）、`EMR_002_scheduler_program_errors_fail_closed`（Promise/throw/非法 target fail closed）；`tests/model-routing-runtime.test.mjs`：`EMR_002_scheduler_program_error_poisons_pending_and_future_demands`（scheduler program error poison pending + future demand） | NEW | `node --test requirements/execution-model-routing/tests/scheduler-module-config.test.mjs requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` |
| EMR-003 active execution multiset / process sharing | `tests/model-routing-runtime.test.mjs`：`EMR_003_each_active_physical_execution_contributes_one_running_occurrence`（不同 physical execution 按 occurrence 计数；同 SessionId 同时只有 current 一个）；`tests/process-shared-routing.test.mjs`：`EMR_003_two_plugin_instances_share_one_process_running_multiset`（两个真实 plugin instance 在同一 Node/OpenCode process 共享 `running`，第二实例看见第一实例 occupancy） | NEW | `node --test requirements/execution-model-routing/tests/model-routing-runtime.test.mjs requirements/execution-model-routing/tests/process-shared-routing.test.mjs` |
| EMR-004 execution-only demand / event-driven null | `tests/model-routing-runtime.test.mjs`：`EMR_004_required_null_waits_for_an_occupancy_event_then_retries`、`EMR_004_newer_physical_message_cancels_superseded_pending_demand`（typed `Superseded`，不 reject）、`EMR_004_an_earlier_null_waiter_does_not_head_of_line_block_another_role`、`EMR_004_optional_null_is_k0_not_a_pending_demand`、`EMR_004_strength_reservation_is_adopted_by_chat_message_without_double_counting`；`tests/process-shared-routing.test.mjs`：`EMR_004_superseded_pending_chat_message_resolves_without_fatal`（真实 plugin hook 穿过 fatal membrane）；`tests/open-code-port-routing.test.mjs`：`EMR_004_sdk_prompt_async_enqueue_never_waits_for_the_host_run_promise`；`requirements/delegation/tests/fork-tool.test.mjs`：`FORK_deep_devops_returns_after_enqueue_even_when_host_prompt_promise_never_settles` | NEW + REUSE | `node --test requirements/execution-model-routing/tests/model-routing-runtime.test.mjs requirements/execution-model-routing/tests/process-shared-routing.test.mjs requirements/execution-model-routing/tests/open-code-port-routing.test.mjs requirements/delegation/tests/fork-tool.test.mjs` |
| EMR-005 MJS owns policy | `tests/routing-authority-boundary.test.mjs`：`EMR_005_runtime_contains_no_product_lane_or_max_sessions_policy`（production 无 `ExecutionLane` / `max_sessions` / first-free 内建策略）；`tests/recommended-template.test.mjs`：`EMR_005_recommended_resource_is_only_a_policy_template`（七组只是可编辑推荐 policy）、`EMR_005_recommended_template_counts_capacity_by_provider_across_models`（同 provider 跨 model/reasoning 合并计数） | NEW | `node --test requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs requirements/execution-model-routing/tests/recommended-template.test.mjs` |
| EMR-006 lease stable only inside exact physical user material | `tests/model-routing-runtime.test.mjs`：`EMR_006_same_physical_message_retry_reuses_target_without_scheduler_rerun`、`EMR_006_new_physical_message_supersedes_old_A_B_occupancy_without_idle`、`EMR_006_same_physical_message_cannot_change_effective_agent`、`EMR_006_lease_is_stable_only_for_one_physical_user_material`、`EMR_006_continuation_passes_previous_target_but_new_session_passes_null`（exact terminal 后 continuation 仍拿到 previous；新 Session 为 null）；`tests/recommended-template.test.mjs`：`EMR_006_recommended_template_prefers_previous_candidate_when_provider_has_capacity`（原 target 仍属于候选且 provider 有容量时优先延续，满载后回到普通 fallback）；`requirements/host-boundary/tests/host-hooks.test.mjs`：`CHAT_MESSAGE_new_physical_material_supersedes_old_capacity_without_idle`（真实 plugin hooks，两个 chat.message 中间无 idle，第二次 scheduler `running=[]`）；`requirements/participant-identity/tests/session-execution-binding.test.mjs`：send model-free，exact physical execution admission 才获取 base/peer lease | NEW + REUSE | `node --test requirements/execution-model-routing/tests/model-routing-runtime.test.mjs requirements/execution-model-routing/tests/recommended-template.test.mjs requirements/participant-identity/tests/session-execution-binding.test.mjs` |
| EMR-007 physical execution supersession/release drives retry | `tests/model-routing-runtime.test.mjs`：`EMR_007_execution_release_is_idempotent_and_wakes_waiters_once` + `EMR_007_late_terminal_for_superseded_physical_execution_cannot_release_current_lease`（exact old terminal 不能误删 reused SessionId 的 current lease）+ EMR-006/004 supersession cases；`requirements/host-boundary/tests/host001-fragment-events.test.mjs`：terminal `message.updated.info.parentID` 提供 exact capacity identity、但仍不成为业务 HostSignal；`requirements/host-boundary/tests/session-execution-binding.test.mjs`：stale old terminal 后 current `chat.params` live lease validation 仍成立；`requirements/host-boundary/tests/host-hooks.test.mjs`：`CHAT_MESSAGE_new_physical_material_supersedes_old_capacity_without_idle`；`requirements/crash-reconciliation/tests/quiescence-surface.test.mjs`：`Q05_new_physical_user_material_revokes_the_previous_idle_before_transform` 证明新 physical admission 在 transform 前关闭上一 terminal 的 idle-send window，防旧 continuation 抢占刚建立的 lease；`tests/routing-authority-boundary.test.mjs`：`EMR_007_chat_message_closes_the_old_idle_window_before_model_admission` 锁定 ingress barrier 在 routing/acquire 前、coarse idle/abort 不拥有 occupancy，exact terminal observer 接 `releasePhysicalExecution`；`SessionExecutionBinding.drop` / plugin dispose 交叉证明 delete/shutdown force cleanup | NEW + REUSE | `node --test requirements/execution-model-routing/tests/model-routing-runtime.test.mjs requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs requirements/crash-reconciliation/tests/quiescence-surface.test.mjs requirements/host-boundary/tests/host001-fragment-events.test.mjs requirements/host-boundary/tests/session-execution-binding.test.mjs requirements/host-boundary/tests/host-hooks.test.mjs` |
| EMR-008 opencode model non-authority | `tests/routing-authority-boundary.test.mjs`：`EMR_008_host_inventory_no_longer_exposes_model_binding_authority`（无旧 model inventory / `tryBoundModel` / duplicate-pair authority）+ `SPEC_INV_fast_and_deep_physical_model_equality_is_not_an_eligibility_gate`（不校验 fast/deep model 互异）；`tests/open-code-port-routing.test.mjs`：`EMR_008_sdk_prompt_never_recovers_a_model_from_agent_or_host_inventory`；`requirements/capability-enforcement/tests/managed-agent-config.test.mjs`：model 可缺失/相同/任意且 owned-field projection 不触碰 model | NEW + REUSE | `node --test requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs requirements/execution-model-routing/tests/open-code-port-routing.test.mjs requirements/capability-enforcement/tests/managed-agent-config.test.mjs` |
| EMR-009 chat.message single model owner | `tests/routing-authority-boundary.test.mjs`：`EMR_009_chat_message_is_the_single_managed_execution_admission_owner`（HostSignalBootstrap required-acquire，Sessions send 栈无 acquire）；`requirements/interaction-authority/tests/chat-params-hook.test.mjs`：`CHAT_PARAMS_uses_the_resolved_provider_model_id_not_the_mutated_user_message_model` / `CHAT_PARAMS_accepts_the_real_provider_model_shape_with_message_variant` 锁住 provider-facing `Model.id` + user-message variant 的真实 observation shape，拒绝 hybrid target；`requirements/participant-identity/tests/session-execution-binding.test.mjs` + `requirements/host-boundary/tests/session-execution-binding.test.mjs`：synthetic enqueue `Model=None`、chat.message 记录 exact physical binding、transform 用 trailing physical id + PromptKey 复核，且 stale old terminal 不会让 current chat.params 丢 lease；`tests/open-code-port-routing.test.mjs`：raw SDK adapter 正确编码显式 target；`requirements/host-boundary/tests/host-hooks.test.mjs` + `requirements/interaction-authority/tests/chat-params-hook.test.mjs`：chat.message acquire+rewrite、chat.params 只 validation、无 idle supersession；真实 Host canary `requirements/verification-system/tests/e2e/support/managed-model-routing-canary.mjs` 证明 provider wire 使用 MJS lease | NEW + REUSE + PHYSICAL | `node --test requirements/execution-model-routing/tests/open-code-port-routing.test.mjs requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs requirements/interaction-authority/tests/chat-params-hook.test.mjs requirements/participant-identity/tests/session-execution-binding.test.mjs requirements/host-boundary/tests/session-execution-binding.test.mjs requirements/host-boundary/tests/host-hooks.test.mjs requirements/interaction-authority/tests/chat-params-hook.test.mjs`；`node requirements/verification-system/tests/e2e/support/managed-model-routing-canary.mjs` |

### GAP

- **GAP-016**（auto-bootstrap sole MJS scheduler / event-driven process-shared physical-execution lease / exact PhysicalUserMessageId binding / managed provider routing）— CLOSED. 关闭证据：production `a0886281`；上述 EMR-001..009 独立 oracle；真实 Host canary 已证明外部 placeholder model 被 MJS lease 覆盖并进入实际 provider wire。
