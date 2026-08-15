# HOW — host-boundary（实现模型与约束；非 normative）

## 实现模型

### 信号边界（`Infrastructure/OpenCode/Codec/` + `Signals/`）

```fsharp
type HostSignal =                    // Infrastructure/OpenCode/Signals/HostSignal.fs
    | SessionIdle of SessionId
    | ProviderRetry of RetrySignal   // { SessionId; Attempt: string; Reason: string } 诊断 only
    | ProviderFailure of sessionId: SessionId * reason: string
    | SessionDeleted of sessionId: SessionId * parentSessionId: SessionId option
    | AttemptAborted of SessionId    // typed 物理 wake；非 ProviderFailure
```

`HostEventCodec.isHostSignalEvent / tryDecode` 在最早边界丢弃 fragment
（`message.updated` / `part.delta` / `session.updated` / `chat.message` 非 terminal）；
`session.status(status.type="idle")` 与 OpenCode 1.18 的独立 `session.idle` 都归一为 `SessionIdle`
wake，重复编码只触发一次新的 bounded reconcile pass，terminal 仍只由完整 snapshot 判定。
`HostSignalSubscribe`（one-shot silence deadline，无事件不触发）。`AttemptAborted` 撤销
idle-derived continuation 能力（QuiescenceGate 属 causal-wait，本包只拥有信号 admission）。

### 快照投影（`Infrastructure/OpenCode/Host/SessionSnapshotPort.fs`）

`projectMessages` 保持 Parts/ToolParts 一致投影；`locateToolCall` 按 callID 唯一解析
ToolPart/assistant/run/ordinal（0/≥2 → `Ambiguous` fail-closed）。`HostSessionContext.read`
从 raw event 提取 `(sessionId, agent)`；`roleOf` 经 `AgentRoleIdentity`。

### Reconciler（`Composition/Turn/Scheduler.fs`）

`Scheduler` 持有 queued/active/generation/wake：同 session 信号合并、最多一个 drain、
generation 隔离（HOST-004）。`maxCausalRereads = 3`；`maxConsecutiveErrors = 5`。
无 wake 记录默认 `RetryWake`（无 idle rights，安全侧）。业务决策在 `ReconcileProgram`（纯）。

### compaction gate（`Domain/HostCompactionPolicy.fs` + `HostCompactionGate/Observer`）

- prevention keys：`compaction.auto`（含 overflow）、`compaction.prune`（COMPANION-009：
  物理删行不可收容）、`compaction.autocontinue`；`autoContinueEnabled = false`。
- `judgeFirstTurn`：setting unavailable → `SettingUnavailable`；首轮 pseudo-run > 0 →
  `CompactedDespiteSettings`；否则 `Satisfied`。失败 → `HostContractUnsupported` 启动失败。
- containment：`isContainableCompaction`（折叠后的 bool，无来源区分）；`nextReanchor` 只返回
  最新未处理 run（一次重锚即 epoch+1 / coverage 归零）。

### 多实例（`Infrastructure/OpenCode/Host/SharedState.fs` + `PluginRuntimeScope.fs`）

模块级单例：`SessionParents` / `VerdictSessions` / `SessionDirectories`（身份注册表）；
每实例：`AgentJournal` / Companions 缓存 / `OwnedSessions` / `UserMessageBindings` / hook 订阅。
共享表操作不跨 `await`（单一 event loop 所有权）。

### 事件 port（`Infrastructure/OpenCode/Host/Events.js`）

`Events_HostEventPort`：per-provider-run Completed dedupe、非 Completed 不 dedupe、late
subscriber sticky replay、listener disposal。

### 其它

- `HostMessageProjection.sanitizeMessages`（HOST-016）在 PairProgrammingThought 之后、
  ReviewSeal 之前执行（历史 how/host 链序）。
- `HostDigest.sha256Hex`：全仓唯一 sha256（durable digest 单点定义）。
- `NeedHelpSensor`（`Host/NeedHelpSensor.fs`）：rolling suffix + reasoning PartId 集 + armed
  identity（SessionId × ProviderRun）；`NeedHelpEventCodec` 先登记 `part.type=reasoning` 再适配
  `field=text` delta；legacy direct reasoning-field 仅 codec 兼容。
- `Tools/ToolContext.fs`：`{ SessionId; Workspace; Cancellation }`（execute 双半边身份经
  `ToolHostCodec` 组装；before/after 只见 sessionID+callID）。

## Plugin Load / Activation 分界（HOST-BOUNDARY-021）

`server(input)` 返回 hooks 之前只组装 capability。该路径不得访问 Host session API，不做 durable semantic recovery，不修改 workspace/Git，不产生业务 durable fact。

Load Phase 可以检查模块、静态资源、配置与 durable bytes 的结构可读性；结构合法但业务语义无法解释时，最多让对应 capability 在使用时失败。普通 hook/tool 也不得承担“上一进程工具恢复”：Fission/Assistance/js-* 等未完成执行保持坏记录。未来 session resume 必须由显式 `/continue` 进入并把 restart/broken-tool 事实公开给 LLM。

## 历史与弃权

- **碎片积分被拒**（why/host.md §4）：流式碎片顺序/形状随 Host 版本漂移 → 选「粗粒度唤醒 +
  完整 snapshot」。
- **busy/running 进业务信号被拒**（cache.md §16）：transport 状态机不搬进 Domain →
  process-local QuiescenceGate（归 causal-wait）。
- **重复读 snapshot 证明 idle 被拒**（cache.md §15）：观测稳定 ≠ 静止资格 → permit 随 wake 携带、
  发送前再 TryConsume（归 causal-wait）。
- **无界退避轮询被拒**（reconciler-event-driven-de-polling.md）：30s 墙钟预算 = 以时间推进做
  A 类探测 → 有界因果重读（≤3）+ 事件驱动。
- **canary 不可弯曲**（canary-unbend.md）：canary 是生产前置证明，不得为绿而弯曲（归
  verification-system 纪律；本包消费其结果）。
- **HOST-013 全部**：归 prefix-stability 等（见 WHAT 弃权）；本包不复制。
- **Magic Todo membrane canaries**：A..R 清单（见 PROOF.md）中本包只拥有 H（唯一
  定位）、A/C（before 时序/原地 mutation）的 Host 观察面；canonical 语义归各 feature owner。
- **`external_directory`**：AGENT-019 唯一 enforcement 写点归 capability-enforcement；Host 路径
  边界机制是 host-boundary 交叉（本包只记录观察面）。
