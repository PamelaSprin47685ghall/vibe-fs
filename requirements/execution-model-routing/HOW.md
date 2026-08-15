# execution-model-routing — HOW

本文件描述当前推荐实现形状，不拥有产品语义；若实现与 WHAT 冲突，以 WHAT 为准。

## 1. 首次生成的推荐 MJS

`~/.config/opencode/wanxiangshu.mjs` 是普通 ESM，default export 一个同步函数。文件不存在时，当前版本 bootstrap resource 的正文就是下面这份推荐模板（实现只允许无语义的 EOF newline 差异）；bootstrap 只负责原子写出该 resource，避免 loader 再维护另一份隐藏默认。

```js
// Wanxiangshu model scheduler. Edit this file freely.
// `running` is a multiset of active { model, reasoning } leases.
// Return a target to acquire it, or null to wait for an occupancy change.

const count = (running, model, reasoning) =>
  running.filter(x => x.model === model && x.reasoning === reasoning).length

const pick = (running, candidates) => {
  for (const [model, reasoning, limit] of candidates) {
    if (count(running, model, reasoning) < limit) return { model, reasoning }
  }
  return null
}

const FASTEST = [
  ["gemma4:31b", "none", 16],
  ["deepseek-v4-flash", "none", 16],
]

const FASTEST_II = [
  ["deepseek-v4-flash", "none", 16],
]

const FASTER = [
  ["step-3.5-flash-2603", "none", 8],
]

const MEDIUM = [
  ["deepseek-v4-flash", "low", 8],
]

const HIGHER = [
  ["cursor-grok-4.6-high", "high", 4],
  ["glm-5.2", "high", 4],
]

const FAST_BROWSER = [
  ["minimax-m3", "none", 8],
]

const DEEP_BROWSER = [
  ["minimax-m3", "none", 4],
]

const pools = new Map([
  ["title", FASTEST],
  ["compaction", FASTEST],
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

export default function route(role, running) {
  const candidates = pools.get(role)
  if (!candidates) throw new Error(`unknown model-routing role: ${role}`)
  return pick(running, candidates)
}
```

这就是推荐的 MJS 风格：**小纯函数 + 明确常量 + 显式 role 表 + `running` 计数 + 顺序 `pick` + 满载 `null`**。不依赖闭包外可变状态，不读时钟/网络/Host，不把模型策略藏进 runtime。

当前推荐模型对应此前七档意图：最快非推理 `gemma4:31b` / `deepseek-v4-flash`；快速只读 `step-3.5-flash-2603`；常规中档 `deepseek-v4-flash` low；高档 `cursor-grok-4.6-high` / `glm-5.2` high；Browser 使用廉价非推理视觉模型 `minimax-m3`，fast/deep 两个 role 可保留不同 admission limit。

这些模型名/limit 是**首次生成模板的当前推荐值**，不是 runtime schema。用户可以直接编辑整个 MJS：删掉七档、换模型、换计数方法、让 fast/deep 共池都合法。已有文件在升级时绝不被新模板覆盖。

`model` 是非空 OpenCode model selector/name；需要 provider 消歧时用户可写完整 `provider/model`。`reasoning` 直接映射 OpenCode 的 reasoning/variant 档位，非推理显式写 `none`。返回值结构之外的策略信息不穿过 ABI。

## 2. bootstrap + loader

进程启动时解析固定路径：

```text
homedir()/.config/opencode/wanxiangshu.mjs
```

若文件不存在：

1. `mkdir ~/.config/opencode`（recursive/idempotent）；
2. 用 exclusive create（Node `wx` 等价语义）写出随包 resource 的推荐模板；
3. 若 exclusive create 因 `EEXIST` 失败，视为并发 bootstrap 已有 winner，禁止覆盖；
4. 无论自己创建还是别人创建，最后都从磁盘 import 该文件。

这样“推荐默认”只存在于可见、可编辑、持久化的 MJS 文件中；runtime 内存里没有另一套 fallback。目录/写入的其它错误 fail closed。

用 Node ESM dynamic import / file URL 加载；只接受同步 default function。import 成功后保留函数引用到当前 process epoch 结束，不做 file watcher/hot reload。修改 MJS 后重启 OpenCode 获得新策略。

Load Phase 允许 mkdir/create/import 这个配置载体，但不调用 Host session API、不执行任何 model demand。scheduler 本身第一次被调用发生在真实 demand 上。

不再需要 TOML parser，也不需要把 `smol-toml` 因本 feature 移入 runtime dependency。

## 3. 运行时最小类型

推荐只保留这些概念：

```text
ModelTarget = { Model: string OpenCode model selector; Reasoning: string }
ModelLeaseKey = SessionId × EffectiveAgent
Running = ModelTarget list                 // multiset，重复保留
Scheduler = role:string × Running -> ModelTarget option
PendingDemand = Required | Optional owner metadata + cancellation
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
managedLeases: (SessionId, EffectiveAgent) → ModelTarget
ephemeralAllocations: SystemInvocationId → ModelTarget
pending: arrival-ordered demands
```

`running` 每次调用临时由两张 allocation 表的 values 拼成新数组。不要缓存第二份计数 truth；MJS 自己从 multiset 计数。

同一 SessionId 的两个 EffectiveAgent 若都指向同一 target，values 中自然出现两个重复元素。

## 5. 串行事件驱动 acquire/release

模型资源 mutation 通过一个 process-wide serial queue/actor 顺序处理，避免并发 scheduler call 基于同一旧 snapshot 同时提交。

### Acquire managed required

1. `(session, agent)` 已有 lease → 直接返回；
2. 生成当前 `running` snapshot；
3. 调 `route(agent, running)`；
4. target → 校验结构，原子写 lease，再返回；
5. `null` → 把 demand 挂入 pending，不发 provider。

### Occupancy changed

每次成功 acquire 或 release 后，对 pending 按 arrival order 做一轮：

1. 对当前仍有效 demand 构造最新 `running`；
2. 调 scheduler；
3. `null` → 保留该 demand，继续检查后面的 demand，避免不同 role 的 head-of-line blocking；
4. target → 立即提交 occupancy，再处理下一个 demand，使后续调用看到更新后的 multiset。

一轮结束后若仍有 pending，不自行再次循环；等待下一次真实 occupancy event。没有 timer/poll。

request/session cancel、retire、plugin shutdown 只需把对应 pending 标为失效/移除。

### Optional demand

Strength 这类可丢弃优化应走 nonwaiting probe：调用一次 scheduler；`null` 立即 K0，不进入 required pending queue。若获得 target，则先正常 acquire 其 replica session lease，retire 时释放。

## 6. Host config 与 managed prompt 路径

旧路径：

```text
opencode.json agent.model
  → ManagedAgentConfig inventory
  → SessionExecutionBinding/OpenCodePort fallback
```

新路径：

```text
wanxiangshu.mjs default route
          + process-shared running
  → ModelRoutingRuntime.acquire(session, effectiveAgent)
  → OpenCodePromptOptions.Model/Reasoning = explicit lease target
  → Host provider request
```

`OpenCodePort` 不再根据 `Agent` 反查 `ManagedAgentConfig.tryBoundModel`；发送边界只接受上游已经解析好的 explicit target。

`ManagedAgentConfig` 退回 Host projection/guard owner：确保 managed agent 的 mode/permission/prompt 等 Wanxiangshu-owned 字段存在，不再把 Host-final `agent.model` 收进 inventory。若 Host schema 强制要求静态 model，应由 Host adapter 选择一个不具 authority 的合法占位/guardrail，真实发送必须被 explicit lease 覆盖；该能力需 canary 证明。

## 7. root/user-facing 路径

`chat.message`/等价可变 request hook 先解析真实用户选中的 managed EffectiveAgent，再 acquire `(session, agent)` lease，并把输出 message/request 的 model/reasoning 改成 lease target。`chat.params` 只做 observed-provider validation：实际 provider target 必须等于 lease。

这条 Host mutation 能力必须由 `host-boundary` physical canary 证明，不能只测 DTO 被改了。

外部请求里的 model/reasoning 只可影响非 managed Host 行为；managed agent 一律由 MJS lease 覆盖。

## 8. AABB / Strength

`AttemptExecutionProfile.EffectiveAgent` 保持 provider-attempt-recovery 的唯一 peer/fallback 输出：

```text
A/A → lease(session, SelectedAgent)
B/B → lease(session, PeerAgent)
```

两个 lease target 完全相同合法。model routing runtime 不比较 fast/deep target，也不参与 FallbackCursor。

Strength 不再读取静态 fast/deep model string。它对 `fast-<owner-role>` 做一次 optional scheduler probe；`null` → K0，不阻塞 owner；target 可用才创建/运行 replica。是否仍有成本收益继续归 speculative-investigation，而不是 model router。

## 9. title / compaction

这两类 Host system request 用 pseudo-role 进入同一 scheduler：

```text
route("title", running)
route("compaction", running)
```

返回 target 后建立 ephemeral allocation；必须在真实 provider terminal/abort 路径释放。`null` 作为 required Host demand 等待 occupancy event。

这需要 Host 能在 title/compaction 发出前接受显式 model/reasoning override；能力不存在时应由 contract canary 让 GAP-016 保持 OPEN，而不是退回静态 `small_model`/agent model 猜测。

## 10. lifecycle 与进程边界

managed lease 只在 session retire/delete 正常释放；普通 idle/completion 不释放。plugin shutdown 清理当前进程 pending/ephemeral 状态；进程退出自然丢失 process-local lease registry。

process restart 重新 import MJS 并开启新 routing epoch。本包当前不把 model lease 写入 durable event history；若未来要求历史 session 跨进程保持完全相同 physical target，应另立 durable binding requirement。

## 11. 主要实现影响面

- 新增 MJS loader + process-shared `ModelRoutingRuntime`；不新增 lane/config schema。
- `ManagedAgentConfig.fs`：删除 Host model inventory authority 与 duplicate-pair-model validation；保留 owned Host fields 投影。
- `SessionExecutionBinding.fs`：binding 从静态 agent→model 改为 session×EffectiveAgent lease；root model 不再由外部 message 绑定。
- `OpenCodePort.fs`：删除 `Agent → ManagedAgentConfig.tryBoundModel` fallback。
- `ChatParamsHook.fs` + request mutation hook：managed model/reasoning route/validate。
- `PluginSessionWiring.fs` / Strength：删除静态 inventory model 依赖，optional replica 走 scheduler probe。
- session dispose/drop/fission cleanup：统一释放对应 managed lease。
- Host title/compaction adapter：建立/释放 ephemeral allocation，并补 physical canary。
