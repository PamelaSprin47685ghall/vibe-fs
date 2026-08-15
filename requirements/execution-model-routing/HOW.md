# execution-model-routing — HOW

本文件描述当前推荐实现形状，不拥有产品语义；若实现与 WHAT 冲突，以 WHAT 为准。

## 1. 首次生成的推荐 MJS

`~/.config/opencode/wanxiangshu.mjs` 是普通 ESM，default export 一个同步函数。文件不存在时，bootstrap 原子写出随包发布的 `resources/wanxiangshu.mjs`；下面展示它的推荐策略与风格，实际生成字节以该 resource 为唯一实现源，避免 loader 再维护另一份隐藏默认。

```js
// Wanxiangshu model scheduler. Edit this file freely.
// `running` is a multiset of active { model, reasoning } leases.
// Return a target to acquire it, or null to wait for an occupancy change.

const count = (running, model, reasoning) =>
  running.filter((item) => item.model === model && item.reasoning === reasoning).length

const pick = (running, candidates) => {
  for (const [model, reasoning, limit] of candidates) {
    if (count(running, model, reasoning) < limit) return { model, reasoning }
  }
  return null
}

const FASTEST = [
  ["ollama-cloud/gemma4:31b", "none", 16],
  ["opencode-go/deepseek-v4-flash", "none", 16],
]

const FASTEST_II = [
  ["opencode-go/deepseek-v4-flash", "none", 16],
]

const FASTER = [
  ["stepfun/step-3.5-flash-2603", "none", 8],
]

const MEDIUM = [
  ["opencode-go/deepseek-v4-flash", "low", 8],
]

const HIGHER = [
  ["cursor/cursor-grok-4.6-xhigh", "xhigh", 4],
  ["neuralwatt/glm-5.2-flex", "high", 4],
]

const FAST_BROWSER = [
  ["ollama-cloud/minimax-m3", "none", 8],
]

const DEEP_BROWSER = [
  ["opencode-go/minimax-m3", "none", 4],
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

export default function route(role, running) {
  const candidates = pools.get(role)
  if (!candidates) throw new Error(`unknown model-routing role: ${role}`)
  return pick(running, candidates)
}
```

这就是推荐的 MJS 风格：**小纯函数 + 明确常量 + 显式 role 表 + `running` 计数 + 顺序 `pick` + 满载 `null`**。不依赖闭包外可变状态，不读时钟/网络/Host，不把模型策略藏进 runtime。

当前推荐模型对应此前七档意图：最快非推理 `ollama-cloud/gemma4:31b` / `opencode-go/deepseek-v4-flash`；快速只读 `stepfun/step-3.5-flash-2603`；常规中档 `opencode-go/deepseek-v4-flash` low；高档 `cursor/cursor-grok-4.6-xhigh` xhigh / `neuralwatt/glm-5.2-flex` high；Browser 分别使用 `ollama-cloud/minimax-m3` 与 `opencode-go/minimax-m3` 的非推理档，fast/deep 两个 role 可保留不同 admission limit。

这些模型名/limit 是**首次生成模板的当前推荐值**，不是 runtime schema。用户可以直接编辑整个 MJS：删掉七档、换模型、换计数方法、让 fast/deep 共池都合法。已有文件在升级时绝不被新模板覆盖。

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
pending: arrival-ordered demands
```

`running` 每次调用临时由 managed lease 表的 values 生成新数组。不要缓存第二份计数 truth；MJS 自己从 multiset 计数。

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

## 9. lifecycle 与进程边界

managed lease 只在 session retire/delete 正常释放；普通 idle/completion 不释放。plugin shutdown 清理当前进程 pending 状态；进程退出自然丢失 process-local lease registry。

process restart 重新 import MJS 并开启新 routing epoch。本包当前不把 model lease 写入 durable event history；若未来要求历史 session 跨进程保持完全相同 physical target，应另立 durable binding requirement。

## 10. 主要实现影响面

- 新增 MJS loader + process-shared `ModelRoutingRuntime`；不新增 lane/config schema。
- `ManagedAgentConfig.fs`：删除 Host model inventory authority 与 duplicate-pair-model validation；保留 owned Host fields 投影。
- `SessionExecutionBinding.fs`：binding 从静态 agent→model 改为 session×EffectiveAgent lease；root model 不再由外部 message 绑定。
- `OpenCodePort.fs`：删除 `Agent → ManagedAgentConfig.tryBoundModel` fallback。
- `ChatParamsHook.fs` + request mutation hook：managed model/reasoning route/validate。
- `PluginSessionWiring.fs` / Strength：删除静态 inventory model 依赖，optional replica 走 scheduler probe。
- session dispose/drop/fission cleanup：统一释放对应 managed lease。
