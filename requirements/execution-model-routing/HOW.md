# execution-model-routing — HOW

本文件描述当前推荐实现形状，不拥有产品语义；若实现与 WHAT 冲突，以 WHAT 为准。

## 1. 配置形状

保持 TOML 只描述资源，不把 22 个 agent→lane 映射复制进用户配置：

```toml
[[lanes.fastest.models]]
model = "provider/gemma4:31b"
variant = "none"
max_sessions = 16

[[lanes.fastest.models]]
model = "provider/deepseek-v4-flash"
variant = "none"
max_sessions = 16

[[lanes.fastest_ii.models]]
model = "provider/deepseek-v4-flash"
variant = "none"
max_sessions = 16

[[lanes.faster.models]]
model = "provider/step-3.5-flash-2603"
max_sessions = 8

[[lanes.medium.models]]
model = "provider/deepseek-v4-flash"
variant = "low"
max_sessions = 8

[[lanes.higher.models]]
model = "provider/cursor-grok-4.6-high"
variant = "high"
max_sessions = 4

[[lanes.fast_browser.models]]
model = "provider/minimax-m3"
variant = "none"
max_sessions = 8

[[lanes.deep_browser.models]]
model = "provider/minimax-m3"
variant = "none"
max_sessions = 4
```

示例模型只说明 intended cost/capability class，不是 normative default。`model` 推荐强制完整 `provider/model`，避免重新引入“从 Host 当前 provider 猜 providerID”的第二 authority。

若同一物理模型跨 lane 重复，loader 汇总 `(providerID, modelID) → max_sessions`，要求所有声明相同；variant 留在 lane candidate 上。

## 2. 类型形状

推荐把配置与运行时分开：

```text
ExecutionLane = Fastest | FastestII | Faster | Medium | Higher | FastBrowser | DeepBrowser
PhysicalModelId = providerID × modelID
ModelTarget = PhysicalModelId × variant option
ModelCandidate = ModelTarget × MaxSessions
ModelLaneConfig = ExecutionLane → non-empty ordered ModelCandidate list
ModelLeaseKey = SessionId × EffectiveAgent
```

`laneOf EffectiveAgent` 是纯函数，和 `ManagedAgentCatalog.peerNameOf` 一样由源码固定；TOML 不含 agent 名。

## 3. allocator

推荐一个 Fable 模块级 process-shared owner，而不是 `PluginRuntimeScope` 字段：现有 Host 会按 directory/worktree 产生多个 plugin instance，`OpenCode.Host.SharedState` 已证明 module-level singleton 是 root/worktree 跨实例共享边界。

registry 至少维护：

```text
leases: (SessionId, EffectiveAgent) → ModelTarget
physicalOccupants: PhysicalModelId → Set<SessionId>
waiters: lane → FIFO(request + cancellation bookkeeping)
```

分配在单一临界区内完成：

1. 已有 lease → 直接返回，不重复计数；
2. 取 agent 的 lane；
3. 按候选顺序找 `occupants.Count < max_sessions`；
4. 找到后同时写 lease + occupant；
5. 全满则挂起 waiter；容量变化后重新从候选 1 扫描。

同一 SessionId 的第二个 agent 若落到同一 PhysicalModelId，只增加 lease，不增加 occupant。释放 session 时先删除全部 `(session, *)` lease，再从涉及的 physical occupant set 删除一次 SessionId。

waiter 不应 busy-loop。每个 lane 维护 FIFO resolver queue；释放容量只唤醒最早仍有效 waiter，由其重新执行完整 first-free 扫描。可以用 Promise resolver/SerialQueue 组合；不需要把资源等待建成 durable business event。plugin shutdown、session retire、request cancellation 都要撤销 waiter。

## 4. Host config 与 prompt 路径

旧路径：

```text
opencode.json agent.model
  → ManagedAgentConfig inventory
  → SessionExecutionBinding/OpenCodePort fallback
```

新路径：

```text
wanxiangshu.toml
  → ModelLaneConfig
  → ModelLaneRuntime.acquire(session, effectiveAgent)
  → OpenCodePromptOptions.Model = explicit leased target
  → Host provider request
```

`OpenCodePort` 不再根据 `Agent` 反查 `ManagedAgentConfig.tryBoundModel`；发送边界只接受上游已经解析好的 explicit model。

`ManagedAgentConfig` 退回 Host projection/guard owner：确保 managed agent 的 mode/permission/prompt 等 Wanxiangshu-owned 字段存在，不再把 Host-final `agent.model` 收进 inventory。若 Host schema 仍要求 agent 有静态 model，可投影 lane 的首选 ModelTarget 作为 guardrail，但它不是 runtime routing authority；真实发送仍显式携带 lease。

## 5. root/user-facing 路径

`chat.message`/等价可变 request hook 应先解析真实用户选中的 managed agent，再 `acquire(session, agent)` 并把输出 message/request model 改成 lease。`chat.params` 只做 observed-provider validation：实际 provider model 必须等于 lease。

这条 Host mutation 能力必须由 `host-boundary` contract canary 证明，不能只测 DTO 被改了。

外部请求里的 model 只可作为非 managed Host 行为；对 managed agent 必须被 Wanxiangshu 选择覆盖。

## 6. AABB / Strength

`AttemptExecutionProfile.EffectiveAgent` 保持 provider-attempt-recovery 的唯一 peer/fallback 输出。发送时用该 EffectiveAgent 查询 `(session, agent)` lease。因此：

```text
A/A → lease(session, SelectedAgent)
B/B → lease(session, PeerAgent)
```

两者 model 相同完全合法。

Strength 的 eligibility 不再读取静态 fast/deep model string 是否互异。它应基于真实 resolved execution target + 成本模型判断 replica 是否仍有优化价值；没有收益则 K0，但这不是全局配置错误。

## 7. title / compaction

Host title/compaction 没有 Wanxiangshu SessionId，不能自然进入 session-capacity allocator。当前最小实现把 `fastest` 首个 target 投影给 Host title/compaction agent。若未来要给这两类系统调用也做并发 admission，应另立 invocation-capacity 语义，不能伪造 SessionId 混入 EMR-005。

## 8. lifecycle 与进程边界

capacity registry 是当前 OpenCode OS 进程内共享状态：root/worktree plugin instance 共用；不同进程互不协调。session retire/delete 是唯一正常释放边界，plugin shutdown 最终清空本进程剩余 waiter/lease。

进程重启开启新的 allocator epoch；本包不把临时容量 lease 写入 durable event history。若未来要求跨重启保持某个历史 session 的物理模型不变，应另立 durable binding requirement，而不是把临时 provider resource occupancy 混进 mission truth。

## 9. 主要实现影响面

- 新增 `ExecutionLane` / TOML loader / process-shared `ModelLaneRuntime`。
- `ManagedAgentConfig.fs`：删除 Host model inventory authority 与 duplicate-pair-model validation；保留/强化 owned Host fields 投影。
- `SessionExecutionBinding.fs`：binding 从静态 agent→model 改为 session×EffectiveAgent lease；root model 不再由外部 message 绑定。
- `OpenCodePort.fs`：删除 `Agent → ManagedAgentConfig.tryBoundModel` fallback。
- `ChatParamsHook.fs` + request mutation hook：managed model route/validate。
- `PluginSessionWiring.fs` / Strength：删除静态 inventory model 依赖。
- session dispose/drop/fission cleanup：统一调用 allocator release。
- package runtime dependency：若使用现有 `smol-toml`，从 devDependency 移入 runtime dependency。
