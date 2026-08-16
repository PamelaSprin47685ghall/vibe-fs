# WHAT — managed-session-lifecycle（唯一 normative 合同）

命题前缀：`MANAGED-SESSION-`。全部命题描述**当前世界必须同时成立**的事实。
来源：旧 host/execution/companion 条款（HOST-008/009/015、EXEC-006/009/014/017/022/026/028/031，
2026-08-14 归档）、历史 change（universal §11/13/17、cache §17）。落点见 `PROOF.md`。

---

## MANAGED-SESSION-001：Attached 会话唯一 lifecycle owner

**规范**：`AttachedSessionRuntime` 是 Attached 会话的唯一创建、恢复、注册、级联取消与 retire
owner；各 AttachmentKind 只提供 payload/terminal 策略，不得复制所有权框架（HOST-008 shape；
历史 how/host 条款 HOST-009）。

**含义/动机**：所有权事实单一 owner；否则崩溃恢复与级联取消分叉（WHY §1）。

**证据**：`Session/AttachedSessionRuntime.fs`（`GetOrCreate/TryFind/Remove/RemoveByDelegateSession`）；
→ PROOF.md `MANAGED-SESSION-001`。

## MANAGED-SESSION-002：创建协议 = 先写关联，再发首个 prompt

**规范**：登记顺序：先写入 `SessionAssociation`（`ExecutionClass` + `Ownership`），再发送首个
prompt（HOST-009）；反向关联必须在首个 prompt 前存在，transform 才能证明该 child 是 leaf
（`SatelliteRuntime` 注释）。

**含义/动机**：prompt 发出后 transform 即可查询关联；晚写关联会让首轮 transform 把 leaf 当普通
Work 处理。

**证据**：`SatelliteRuntime.start`（`spec.Link` 先于返回 lease）；→ PROOF.md `MANAGED-SESSION-002`
（REUSE `satellite-runtime.test.mjs` 的 create 路径）。

## MANAGED-SESSION-003：restart 恢复判据 — 匹配则复用，无关联新建，冲突 fail closed

**规范**：restart 恢复 Attached：query family root children（owner ≠ root 时并查 owner children）→ journal 关联（`RestoredSessionId`）且 id+agent+title 恰好 1 个匹配 → 复用；journal id 不存在 → Replacement（新建，物理挂 root）；无 journal 关联 → 不复用任何候选、直接新建；id 匹配但 agent/title 冲突、多个 id 匹配或查询失败 → fail closed（HOST-009/015；历史 how/host 条款）。

Replacement **不是 direct repoint**：当 durable owner 仍链接旧 child 时，必须先建立新的 physical child，再 durable `Close` 旧 attachment，最后 `Link` 新 child。禁止在旧关联仍存在时直接 append `CompanionBloggerLinked(new)`；该事实按 COMPANION-002 必须 semantic reject。Close 或 Link 任一步失败都 abort 本次 fresh replacement；不得把失败 flight 永久 memoize，下一次显式 material/ensure 必须重新观察 Host + durable truth。

**含义/动机**：恢复必须可证明绑对；猜测 = 收养别人的 child（HOST-015）。REVIEW-019：仅 proven
loss 后替换，不确定 fail closed。

**证据**：`SatelliteRuntime.start`（`Reused | Replacement | Created` + 冲突错误）；`HostForkRestart`；
→ PROOF.md `MANAGED-SESSION-003`（REUSE `satellite-runtime` + `host-fork-restart`）。

## MANAGED-SESSION-004：Reusable 与 OneShot 是两条互斥生命周期

**规范**：SyncDelegate（dedicated `inspect` / `establish-behavior` / `repair-behavior`）走
reusable：completion 后不 retire / 不 dispose，同 scope 续问复用同一 Session；Residual OneShot
（若有）每次调用新建 child、成功完成后 abort/dispose，不跨调用复用（EXEC-028）。**不得**混用。

**含义/动机**：把 dispose-after 套在 reusable 上 = 每轮丢 context；把 reusable 当 one-shot =
永远在重建。

**证据**：`HostForkRuntime.Reuse`（reuse 不 spawn）；→ PROOF.md `MANAGED-SESSION-004`
（MOVE `host-fork-agent.test.mjs`；REUSE `sync-delegate-runtime.test.mjs`）。

## MANAGED-SESSION-005：ReuseScope 是 Dedicated 绑定的生命周期 key

**规范**：dedicated Session 绑定 key = `(OwnerReuseScopeId, SyncDelegateRole)`；同一 scope 至多
一个 live dedicated Session；同 scope 兼容续问复用、不同 scope 不共享；owner effective tier →
deterministic delegate tier，复用既有 child 时沿用已绑定 managed agent（EXEC-026/028 §B）。

**含义/动机**：key 不是 `(owner SessionId, role)`——同一语义工作上下文（Logical Run / 可复用
scope）跨多个 owner session 仍复用同一 dedicated Session（universal.md §11）。

**证据**：`AttachedSessionRuntime`（key = scope + role）；`ReuseScope.ofSession/compatible`；
→ PROOF.md `MANAGED-SESSION-005`（NEW `attached-session-runtime.test.mjs`）。

## MANAGED-SESSION-006：Handle 四态；tombstone 与 abandon 不可回退

**规范**：Handle 生命周期四态：`Active → CompletedAwaitingJoin → Retired` 或
`Active|CompletedAwaitingJoin → Abandoned`；`Retired` 与 `Abandoned` 是 durable terminal，永不复原
为 Active / CompletedAwaitingJoin；已 Retired 的 id 永远回答 Retired，不得降级成「当 agent 名再
fork」（EXEC-009）。

**含义/动机**：tombstone 是防重复投递与防身份回收的物理事实；回退 = 重放历史改变结局。

**证据**：`HandleProjection`（`linkNamed/complete/abandon/retire`）；`HandleController.consume`；
→ PROOF.md `MANAGED-SESSION-006`（REUSE `execution/handle.test.mjs`）。

## MANAGED-SESSION-007：completion cell 单赋值；第一个赢家唯一

**规范**：terminal、send-failure 与 cancel 竞争同一个 completion cell：先到者胜，后来者被拒而非
覆盖（EXEC-004）；`recordCompletion` 只接受 `JoinableCompletion`（Succeeded | Failed finality），
raw Aborted / 裸 kind+body 不能占 cell（P0-RECOVERY-JOIN-001）。

**含义/动机**：覆盖写 = 同一 handle 两个结局；join 必须只读到稳定唯一的完成事实。

**证据**：`HandleProjection.complete` 拒绝 `AlreadyCompleted`；`HandleController.recordCompletion`；
→ PROOF.md `MANAGED-SESSION-007`（REUSE `execution/handle.test.mjs`）。

## MANAGED-SESSION-008：retire 是 consume 的唯一写口

**规范**：`join` 消费 completion 后写 `HandleRetired`（`HandleController.consume`）；`CommitUnknown`
不得交出 payload（否则 caller 视为已消费而 restart 仍可 join，重复投递）。retirement 使已消费
completion 不可再返回。

**含义/动机**：唯一 consume 路径 + tombstone = 投递 exactly-once（restart 视角）。

**证据**：`HandleController.consume`（`AlreadyRetired` / `AppendFailed`）；→ PROOF.md
`MANAGED-SESSION-008`（REUSE `execution/handle.test.mjs` `EXEC_004_join_may_only_retire...`）。

## MANAGED-SESSION-009：abandon 是 durable terminal；parent cancel 逐 child 写

**规范**：`Abandoned`（含 `ParentCancelled` 等 reason）不可 join、不可回退；parent cancel 对每个
owned agent 逐个写 `HandleAbandoned`，无批量 fact（EXEC-009）；operator abort → `TurnAborted`
cleanup 必须取消父全部仍运行的 sub-session（EXEC-017 cascade cancel）。任何随后会释放
`AgentJournal` / EventStore / workspace 的 owner teardown，必须等待这次 parent cancel 完成 durable
`HandleAbandoned` 与 physical child teardown 后才能返回；不得用 detached `Async.StartImmediate` 把
cancel 留到 store/repository 已释放之后继续执行。

**含义/动机**：父取消 = 子全部止损；逐 child 使恢复与审计逐条可定位。

**证据**：`HandleController.cancelChildren` / `recordAbandon`；→ PROOF.md `MANAGED-SESSION-009`
（REUSE `execution/handle-abandoned.test.mjs` + `host-fork-agent` 的 abandoned 拒绝）。

## MANAGED-SESSION-010：HostOwnedHidden handle 对父不可见

**规范**：`HandleOwnership.HostOwnedHidden`（EXEC-014 Distiller child、GLORY-002 hidden Reviewer）
对父的 `list` / `join` / `horizon` / EXEC-016 background guard / 父恢复（RestoreHandles）全部不可见；
记录仍持久，仅供 Host-owned workflow 审计与自身恢复。

**含义/动机**：`run` 同步掌控 Distiller 生命周期；若 hidden 泄漏进 `listable`，会阻塞 caller 的
suicide（`distiller-ownership.test.mjs` 头注释回归）。

**证据**：`HandleProjection.parentVisible` + 视图过滤；→ PROOF.md `MANAGED-SESSION-010`
（MOVE `distiller-ownership.test.mjs`）。

## MANAGED-SESSION-011：proven permanent loss 才有 replacement 资格；replacement 必须显式迁移 durable association

**规范**：journal 关联的 Host child 永久消失（proven）→ 按该 AttachmentKind 恢复合同 Replacement；lookup failure / ownership conflict / 重复候选 → fail closed（HOST-009/015；REVIEW-019）。不确定时宁可不恢复，不得猜。

对允许 Replacement 的 attachment，状态迁移必须是 `old durable link → Close(old) → Link(new)`；不能把 `Link(new)` 当作覆盖赋值。只有新 child 创建成功且 old association 被合法关闭后，新关联才能提交。失败 ensure 的 single-flight cache 必须失效，避免一次冲突把未来所有 ensure 永久钉在同一个 rejected Task 上。

**证据**：`SatelliteRuntime.start/linkLease`（journal-linked child 不在 merged candidates → Replacement；Close→Link；冲突 → Error）；→ PROOF.md `MANAGED-SESSION-011`（REUSE `satellite-runtime`）。
## MANAGED-SESSION-012：Child Run 生命周期与父背景记录分离

**规范**：Child Run 生命周期（active / cancel / completion cell 单赋值 / 物理状态投影
`Busy|Idle|Interrupted|Closed`）与父背景记录分离（EXEC-006）；父背景记录不冒充 child completion
（EXEC-008）。

**含义/动机**：子运行状态是物理事实；父的「他还在跑/已结束」不得与父自己的工作记录互相污染。

**证据**：`Session/ChildRun.fs` + `ChildRunProjection.fs`（`status`/`toRecord`）；
→ PROOF.md `MANAGED-SESSION-012`（MOVE `child-run-projection.test.mjs`）。

## MANAGED-SESSION-013：restart 按 durable handle 投影 re-enlist child

**规范**：`HostForkRestart.restoreLinkedChildren`（及 journal-only `restoreLinkedChildrenWithoutRuntime`）
按 durable handle 投影（HandleLinked 事实 + completion blobs）恢复每个 child 的合法 lifecycle：
active → RecoveredActive、terminal → re-enlist、abandoned/retired → 原样恢复；HostOwnedHidden
过滤；legacy 假 abort / 无效 blob / 恢复 commit 失败 → 等待 / 阻塞（EXEC-009/GREEN-4）。

**含义/动机**：restart 后 child 的 lifecycle 只能从 durable facts 重建；内存/猜测不得参与。

**边界**：generic 恢复协议与 permit 线性序归 `crash-reconciliation`（EXEC-023）；假 abort 的
outcome 分型归 `effect-accounting`（EXEC-022）——本命题只拥有「handle 投影恢复出的合法结果」。

**证据**：`Session/HostForkRestart.fs`；→ PROOF.md `MANAGED-SESSION-013`（REUSE
`host-fork-restart.test.mjs`）。

## MANAGED-SESSION-014：Dedicated Session 生命周期 = OwnerReuseScope 生命周期

**规范**：Dedicated Session lifetime = OwnerReuseScope lifetime；graceful ReuseScope close 才
retire/release（universal.md §17；EXEC-026 §B 不变量 6）；「owner Session 进入最终 retire/dispose」
不等同 ReuseScope 终结。

**含义/动机**：同一 scope 跨多个 owner session 复用；scope 未关而 retire = 丢 hot knowledge。

**证据**：`SyncDelegateRuntime`（G6 删除 child 时 retire live binding 但为 owner scope close
保留）→ PROOF.md `MANAGED-SESSION-014`（REUSE `sync-delegate-runtime.test.mjs`）。

## MANAGED-SESSION-015：handle 是 agent id；同一 handle restart 后绑同一 child

**规范**：agent child 的 handle IS 其 runtime agent id（`HandleController.agentHandle`）；EXEC-009
要求 restart 后同一 handle id 绑同一 child session；`ChildSessionId` 只由 Host 签发，禁止从 handle
id 派生虚构 session（`HandleController.linkNamed` 注释）。

**含义/动机**：第二身份 = 一张需要持续同步的映射表；虚构 session = 每次操作静默 no-op 的幽灵资源。

**证据**：`HandleController.agentHandle/linkNamed`；→ PROOF.md `MANAGED-SESSION-015`
（REUSE `execution/handle.test.mjs` `EXEC_009_a_linked_handle_records_the_child_session_it_drives`）。

## GARBAGE / 弃权（不进入 WHAT）

- `EXEC-021/022` 假 completion 补偿的 outcome 分型本体 → `effect-accounting`；本包只拥有 handle
  状态机对补偿事实的拒绝（`rejectFalseCompletion`），落点仍 REUSE 本包 handle 测试。
- `EXEC-023` permit→join 线性序、`EXEC-024` Mailbox 双通道 → `crash-reconciliation` /
  `process-execution`；本包不复制恢复协议。
- `EXEC-005` horizon 在场名册 / `EXEC-016` JoinGuard continuation 语义 → `delegation` /
  `interaction-authority`；本包只消费其 outstanding-background 判据（listable handles）。
- `EXEC-014` 的 Distiller office 语义（map/reduce、机器 Assignment 不进工具面）→
  `output-distillation` / `participant-horizon`；本包只拥有 hidden handle 的可见性。
- 历史 `Student/Teacher` 生命周期（`StudentRun`/`teacherCalls` 等）→ GARBAGE（G3 删除；
  历史 shape/execution EXEC-027 空缺）。

