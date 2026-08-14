# WHAT — host-boundary（唯一 normative 合同）

命题前缀：`HOST-BOUNDARY-`。全部命题描述**当前世界必须同时成立**的事实。
来源：旧 host/architecture 条款（HOST-001..027、ARCH-002/003/012，2026-08-14 归档）与
历史 completed changes（cache / reconciler-event-driven-de-polling）。落点见 `PROOF.md`。

---

## HOST-BOUNDARY-001：业务层不消费流式碎片（事件分层）

**规范**：业务层不得消费流式碎片。合法路径只有：`碎片事件 → 最早边界丢弃 → 粗粒度信号
（idle / retry / deleted）→ single-flight → SDK 完整消息 → 纯策略`（HOST-001；ARCH-002）。
禁止处理 `message.updated` / `part.delta` / `session.updated` / `session.diff` 作为业务输入；
禁止从 idle payload 推断 terminal/完成/失败；禁止依赖事件先后顺序推导因果。

**含义/动机**：碎片顺序/形状随 Host 版本漂移；把因果绑在传输噪声上（why/host.md §4）。

**证据**：`HostEventCodec`（`isHostSignalEvent` / `tryDecode` 在 codec 边界丢弃 fragment）；
→ PROOF.md `HOST-BOUNDARY-001`（MOVE `host001-fragment-events.test.mjs`）。

## HOST-BOUNDARY-002：允许进入业务层的信号是闭集，且分型正确

**规范**：仅 `session.status = idle`、`session.status = retry`、`session.error =
MessageAbortedError | AbortError`、`session.deleted` 可进入 Session 生命周期与 Reconciler。
abort error 必须解码为 typed `AttemptAborted`（撤销当前 attempt 的 idle-derived continuation
能力；**不是** `ProviderFailure`，不得推进 fallback）（HOST-002）。

**含义/动机**：把 ProviderError 当 AttemptAborted（或反之）会让 fallback 预算被 assistance/用户
取消污染；分型是 fallback 正确性的前提（why/host.md §1）。

**证据**：`HostSignalAdapter` + `HostEventCodec`；→ PROOF.md `HOST-BOUNDARY-002`
（MOVE `host001-fragment-events.test.mjs` `HOST_001_only_coarse_session_lifecycle_signals_cross_the_boundary`；
REUSE `codec/signals.test.mjs`）。

## HOST-BOUNDARY-003：Transport ≠ Domain；typed HostSignal；信号是 wake 不是事实载体

**规范**：Domain 合同永远是 typed `HostSignal`（`SessionIdle | ProviderRetry | ProviderFailure |
SessionDeleted | AttemptAborted`）；业务层不得观察 raw payload。HostSignal 任何 case 不携带
message id（FALLBACK-003）；`ProviderRetry.Attempt` 只用于诊断与唤醒，不是 Fallback 领域计数
（FALLBACK-010）。业务事实只从完整 snapshot 读取（ARCH-002）。

**含义/动机**：从事件字段推导领域事实 = 把传输层偶然参数当权威（`HostSignal.fs` 头注释：
retry 事件的 messageID 曾被当成失败 assistant 写进 cursor）。

**证据**：`Infrastructure/OpenCode/Signals/HostSignal.fs`；
→ PROOF.md `HOST-BOUNDARY-003`（NEW `host-capability-observation`；MOVE `host001-fragment-events`）。

## HOST-BOUNDARY-004：TurnUnknown 是 reconciliation 私有观测，不是 TurnOutcome

**规范**：`finish=None` 的稳定 snapshot 分类为 reconciliation 私有 `SnapshotObservation.TurnUnknown`，
**不是** 可 publish 的 `TurnOutcome` case；`TurnUnknown` 不得穿过稳定业务 turn 边界
（HOST-002/004；`ReconcileProgram` 类型注释）。

**含义/动机**：把「观测不到」当「业务结局」会制造假 terminal / 假 missing-final-report。

**证据**：`ReconcileProgram.SnapshotObservation` / `TurnOutcome`；→ PROOF.md `HOST-BOUNDARY-004`
（REUSE `domain/reconcile-program.test.mjs` + `codec/signals.test.mjs`）。

## HOST-BOUNDARY-005：Reconciler 快照观测 machinery（single-flight / dirty / 有界因果重读）

**规范**：Reconciler 每 session 同时最多一次 reconcile（single-flight）；idle 到达设 dirty；
每次 idle 至多 3 次因果重读；无新信号时不产生 `setTimeout` / `GetMessages`
（HOST-004；`reconciler-event-driven-de-polling.md` 有界重读取代无界退避轮询）。

**含义/动机**：轮询把时间推进当业务状态探测（A 类病态）；事件驱动只对真实信号反应。

**证据**：`Application/Reconciliation/Reconciler.fs`（Scheduler：queued/active/generation/
wake）；→ PROOF.md `HOST-BOUNDARY-005`（REUSE `execution/reconcile-idle-early.test.mjs`、
`domain/reconcile-program.test.mjs`）。

## HOST-BOUNDARY-006：同一 raw part 的 Parts/ToolParts 状态投影一致

**规范**：session-shaped `type="tool"` part：`state.status = pending|running` → `Parts =
ToolCall` + `ToolParts = Pending`；`state.status = completed|error` → `Parts = ToolResult` +
`ToolParts = Completed|Failed`。禁止 `ToolParts = Failed` 而 `Parts = ToolCall` 的分叉投影
（HOST-004）。

**含义/动机**：分叉投影会把已失败 execution 重新表示成 in-flight，错误抑制 interaction repair。

**证据**：`SessionSnapshotPort.projectMessages`；→ PROOF.md `HOST-BOUNDARY-006`
（MOVE `session-snapshot-locality.test.mjs` `HOST-004_keeps_failed_session_tool_state_consistent...`）。

## HOST-BOUNDARY-007：compaction 观测 gate — prevention + containment

**规范**：HOST-006 两层必须同时成立：预防 = `compaction.auto`（含 overflow）/ `compaction.prune` /
`compaction.autocontinue` 关闭且**首个 managed session 第一轮请求后 pseudo-run 为零**，否则
`HostContractUnsupported` 启动失败；收容 = 任意观察到的 compaction pseudo-run → 原子重锚
（`ContextReanchored`），不区分 manual `/compact` 与 Host 自触发（CTX-005 同构）。

**含义/动机**：关掉配置单独不算已证明（上游键名漂移）；收容只认 transcript 事实，是主防线
（why/host.md §5）。

**边界**：重锚的 `ContextReanchored` 语义（PrefixEpoch+1 / Snapshot=None）归 `prefix-stability`；
恢复失败/容量信号语义归 `context-compression`。本命题只拥有「观测 gate + fail-closed」。

**证据**：`Domain/HostCompactionPolicy.fs`（requiredSettings / autoContinueEnabled /
judgeFirstTurn / nextReanchor）；`HostCompactionGate/Observer`；→ PROOF.md `HOST-BOUNDARY-007`
（NEW `host-capability-observation`）。

## HOST-BOUNDARY-008：Transform→ProviderRunIdentity 因果读；0/≥2 不写 seal

**规范**：transform 绑定用因果读：`role=assistant`、`time.completed` 未设、`parentID` 匹配最后一条
user、`id` 为 session 内 assistant 最大者 → 命中**恰好一个**才绑定；命中 0 或 ≥2 → 不写 seal。
compaction / summary 路径 → 不写 seal。唯一性前提是单 actor 写 assistant（HOST-010；
历史 how/host 引理 1–4）。

**含义/动机**：same-root 猜测在 Host 重排消息时假绿；宁可放弃 seal（REVIEW-010 只见
PendingIdentity/Rejected），不赌同一身。

**证据**：`ReviewSeal` / `TurnBinding`（消费因果读）；→ PROOF.md `HOST-BOUNDARY-008`
（REUSE `review/*` + `host/` 相关 seal 测试；canary 见本文件 PROOF.md canary 清单）。

## HOST-BOUNDARY-009：Tool 身份两个半边；缺一 fail closed

**规范**：`ToolContext`（execute）同时有 message id 与 call id；`tool.execute.before/after` 只有
call id。`ProviderRunIdentity` + `ToolCallId` 只能同时从 `ToolContext` 取得；任一缺失 → fail
closed。禁止用 after 的 callID 与别处 messageID 猜测配对；禁止使用 SDK/Host 不存在的字段
（如 `userMessageID`）冒充物理用户消息身份（HOST-011）。

**含义/动机**：身份半边是 hook 面的物理现实；猜配对 = 假绿（历史 shape/host HOST-011）。

**证据**：`Tools/ToolContext.fs`、`ToolHostCodec`；→ PROOF.md `HOST-BOUNDARY-009`
（REUSE `plugin/tool-host-codec.test.mjs` `HOST-011`）。

## HOST-BOUNDARY-010：多实例边界 — 共享身份注册表，每实例私有状态；不跨 await

**规范**：跨 worktree 实例：`SessionParents` / `VerdictSessions` / `SessionDirectories` 是模块级
共享单例；`AgentJournal` / Companions 缓存 / `OwnedSessions` / `UserMessageBindings` / hook 订阅
每实例独有（`PluginRuntimeScope`）。共享表只由单一 Node.js event loop 访问，单次查改不跨
`await`；跨异步边界先复制不可变快照；禁止「读取 → await → 按旧值回写」RMW（HOST-012 C2）。

**含义/动机**：第二 worktree 实例读不到主实例 verdict 是实测边界（why/host.md §7）；
共享 Journal writer 会折叠写盘。

**证据**：`Infrastructure/OpenCode/Host/SharedState.fs`、`PluginRuntimeScope.fs`；
→ PROOF.md `HOST-BOUNDARY-010`（REUSE `host/shared-state.test.mjs`）。

## HOST-BOUNDARY-011：空 Content 预防（HOST-016）

**规范**：交付上游 provider 前：无 `tool_calls` 的 `assistant` 消息若无 text part 或 text 为空，
以 reasoning/thinking 文本（或默认 `"..."`）填充 text part；`user` 消息 text 为空填充 `"#"`。
禁止向上游发送空 content 消息（上游 400 `messages[i].content cannot be empty`）。

**含义/动机**：依赖外部网关/厂商容错实现不一；在 transform 末尾兜底是唯一可靠位置
（why/host.md §8）。

**证据**：`HostMessageProjection.sanitizeMessage/sanitizeMessages`；→ PROOF.md `HOST-BOUNDARY-011`
（MOVE `host-message-projection.test.mjs`）。

## HOST-BOUNDARY-012：sessionID+callID 定位 canary；不能唯一 fail closed（HOST-025）

**规范**：before/after 仅有 `sessionID + callID`（HOST-011）。必须证明经完整 SDK snapshot 能
**唯一**定位原 ToolPart / assistant message / provider run / ToolPart ordinal / XTrace range；
命中 0 / ≥2（如 callID 出现在多个持久化 ToolPart）→ fail closed。禁止用 callID 到别处猜配
messageID。

**含义/动机**：membrane 与 deferred prepare 的定位基础；不能唯一证明 = 上线即错配。

**证据**：`SessionSnapshotPort.locateToolCall`；→ PROOF.md `HOST-BOUNDARY-012`
（MOVE `session-snapshot-locality.test.mjs` `TODO-004_*` 定位断言）。
## HOST-BOUNDARY-013：HOST-027 reasoning sensor — 只认 reasoning delta，每 run 一次

**规范**：Host 只从 managed Work provider attempt 的 reasoning/thinking delta 识别精确 sentinel
`[NEEDHELP]`；检测器能跨 delta 边界拼出 sentinel，但只保留有限 rolling suffix；visible text、
tool output、synthetic Pair Hint 与历史 transcript 中的同字节**不得**触发；每个
`(SessionId, ProviderRunIdentity)` 至多触发一次（HOST-027）。

**含义/动机**：sensor 是 assistance 的物理入口；把 visible text 扫描伪装成等价实现 =
把用户正文当求助信号。

**边界**：assistance abort 的 authority 语义（不推进 fallback）归 `interaction-authority`；
abort cause 分离归 `degeneration-guard`；consultation child 归 `delegation`。本命题只拥有
「reasoning sensor 的识别/armed 边界」。

**证据**：`NeedHelpEventCodec` + `NeedHelpSensor`（rolling suffix / armed identity / tryTake）；
→ PROOF.md `HOST-BOUNDARY-013`（MOVE `needhelp-sensor.test.mjs`）。

## HOST-BOUNDARY-014：不修改 OpenCode 本体；只用现有 Hook/SDK（ARCH-003）

**规范**：仅使用现有 Hook/SDK：`chat.message`、`experimental.chat.messages.transform`、
`tool.definition / tool.execute.before / tool.execute.after`、
`experimental.session.compacting / experimental.compaction.autocontinue`、`event`、
`client.session.* / prompt_async / session.messages`。禁止要求新 Hook、改 Host 源码、依赖未公开
API（ARCH-003）。

**含义/动机**：修改 Host core = 每次升级维护 fork；依赖未公开 API = 无声漂移。

**证据**：`Infrastructure/OpenCode/Plugin/PluginTransforms.fs`（hook 顺序收敛）、
`OpenCodePort`；→ PROOF.md `HOST-BOUNDARY-014`（REUSE `plugin/host-hooks.test.mjs`）。

## HOST-BOUNDARY-015：tool 文本结果有界（ARCH-012）

**规范**：自定义 tool 文本结果在 Host 默认 head truncation 之前完成确定性留尾截断：≤2000 行且
UTF-8 ≤51200 字节时逐字返回；超限时输出固定 marker + 确定性尾部（优先最新完整行；最后一行自身
超限按 UTF-8 scalar 安全保留后缀）。计量只认 UTF-8 字节与换行（ARCH-012）。

**含义/动机**：结果 wire 有界是 Host 稳定契约；截断只影响返回 wire，不改内部完整事实来源。

**证据**：`Process/LargeGate.fs` / `Domain/ToolResultBound.fs`；→ PROOF.md `HOST-BOUNDARY-015`
（REUSE `context/tool-result-bound.test.mjs`）。

## HOST-BOUNDARY-016：HostEventPort 按 provider run 去重 + sticky replay（观察可靠性）

**规范**：同一 provider run 的 Completed outcome 重复通知被吸收（每 run 恰好一次送达）；无
provider run 的 Completed 与 failed/aborted outcome 不去重；late subscriber 收到每 session 最后
一个 sticky outcome；disposed listener 停止投递（HOST-012 观察面；Events.js）。

**含义/动机**：root 与 worktree 实例都 reconcile 同一 child；第二次 Completed 不得用旧 run 的
outcome 完成新 run。

**证据**：`Infrastructure/OpenCode/Host/Events.js`（`Events_HostEventPort`）；
→ PROOF.md `HOST-BOUNDARY-016`（MOVE `events-port.test.mjs`）。

## HOST-BOUNDARY-017：Host 身份/配置观察适配（HostSessionContext / ManagedAgentConfig）

**规范**：Host 侧只做观察适配：raw event → `(sessionId, agent)` 提取（`properties.sessionID`
优先，`event.sessionID` 兜底；agent 只从 `properties.info` 取）；agent 名 → Role 解析（
`fast-coder` → Coder；`build`/`plan` 等 alias 拒绝）经 `AgentRoleIdentity`；`ManagedAgentConfig`
校验 managed agent inventory 与 `external_directory` 归属字段。Host 适配不创建任何业务 authority。

**含义/动机**：身份观察是 Host 边界能力；alias 拒绝防止把非 managed 名当真实角色。

**边界**：Role 身份规则本体归 `participant-identity`；`external_directory` 允许语义归
`capability-enforcement`（AGENT-019 交叉）；本命题只拥有「Host 观察适配面」。

**证据**：`HostSessionContext.read/roleOf`、`ManagedAgentConfig.validate/applyOwnedFields`；
→ PROOF.md `HOST-BOUNDARY-017`（MOVE `host-session-context.test.mjs`；REUSE
`managed-agent-config.test.mjs` 的 owned-fields 部分）。

## HOST-BOUNDARY-018：默认不修改 Host 本体；Host fork 另立需求

**规范**：当前产品默认不 fork OpenCode；若未来产品选择 Host fork，应另立独立需求（boundary
card DOES NOT OWN 之外的产品决策）。

**含义/动机**：fork 是产品级决策，不是 adapter 内部优化；它改变所有 capability 的维护契约。

**证据**：ARCH-003（只用现有 Hook/SDK）；→ PROOF.md `HOST-BOUNDARY-018`（REUSE
`plugin/host-hooks.test.mjs`）。

## HOST-BOUNDARY-019：Host capability 缺口必须由 canary/contract proof 证明

**规范**：业务依赖的每条 Host 物理能力（snapshot 定位、hook 时序、compaction 观测、信号边界、
因果读唯一性）必须由可红 proof（canary / contract 测试）证明；不能默默依赖 undocumented API 或
假设上游默认值（HOST-019/024/025 blocking canaries；PROOF.md Magic Todo membrane
canary 清单 A..R）。

**含义/动机**：未验证能力 = 上线首炸；`HostContractUnsupported` 是显式失败而非悄悄降级。

**证据**：本包 proof 表全部 canary + PROOF.md Magic Todo membrane canary 清单；→ PROOF.md
`HOST-BOUNDARY-019`。

## HOST-BOUNDARY-020：观察不足或多解时 fail closed（家族原则）

**规范**：凡观察不足（查询失败、0 命中）或多解（≥2 命中、多个候选、冲突）一律 fail closed，
不猜不收养：HOST-010（0/≥2 不写 seal）、HOST-011（缺半边拒绝）、HOST-025（定位不能唯一拒绝）、
HOST-015 恢复冲突（归 lifecycle 消费）、HOST-013 anchor 缺失不重定位。

**含义/动机**：安全侧失败是 Host 边界的总纪律：宁缺证明，不赌同一身（why/host.md §6）。

**证据**：`session-snapshot-locality`（`Ambiguous`）、`needhelp-sensor`（armed 唯一）、
`host001-fragment-events`（codec 丢弃）；→ PROOF.md `HOST-BOUNDARY-020`。

## GARBAGE / 弃权（不进入 WHAT）

- `HOST-013` Pair Hint 全链（durable anchored pair、Cursor NUL+BOM、parallel wave、tip nudge、
  `SessionStartedAt` wall-clock）→ `prefix-stability`（append-only prefix law + placement）、
  `provider-projection`（renderer）、`cognitive-environment`（正文 craft）、`guidance-delivery`
  （nudge）、`time-capability`（elapsed）；本包只保留其中「Host 编码/entity 物理面」的观察事实
  （归 prefix-stability 侧，不复制）。
- `HOST-017..024` Magic Todo membrane 的 canonical/effect/review/description 语义 → 各 feature
  owner（obligation-ledger / effect-accounting / review-assurance / action-affordance /
  participant-horizon）；本包只拥有 `HOST-019` before 时序 barrier、`HOST-020` 原地 mutation、
  `HOST-025` 定位 canary 三个 Host 观察面（并入 HOST-BOUNDARY-012/019/020，未单列）。
- `HOST-007` 日志纪律 → `crash-reconciliation` / `structured-workflow`。
- `HOST-005` XTrace → `semantic-trace`；`HOST-026` ProviderLanguage → `provider-language`。
- `AGENT-026` stealth-browser MCP 启动判定 → `external-investigation` / HOW（Host adapter 机制）。
- `HOST-014` Student/Teacher Host 行为 → GARBAGE（G3 删除）。

