# 等待语义分类与去业务轮询化

未裁决候选。不是当前规范，不得直接据此修改生产代码。

## Current baseline

系统当前存在数类由时间驱动、而非由事件信号驱动的等待，其实现彼此混杂：

1. **Reconciler 轮询（业务状态探测）**：`Reconciler.Scheduler.materializeActive` 收到
   `Provisional` / `Unknown` / `NoTurn` 时，按 `[| 50; 100; 250; 500; 1000; 2000; 3000; 5000 |]`
   ms 退避递归 `delayMs`（`setTimeout`）重读 SDK snapshot，`budget = 30_000` ms。
   现行正式条款 `docs/how/host.md` HOST-004 只要求「最多 3 次因果重读；仍 Unknown
   则保持 Dirty 等下一信号」，且 `tests/unit/execution/reconcile-idle-early.test.mjs`
   把 early-idle（idle 先到、transcript 后 materialize）定为 highest-probability
   completion-loss point——因果重读正是为该窗口存在。
2. **Executor join 忙等待**：`ExecutorSummarize.awaitAgent` 在 `while not done'` 循环中
   join-any，非目标 Agent 的完成结果写入局部 `stash` 字典（`summarizeSpool` 内新建），
   直到撞上目标 Agent 或 600s 墙钟预算。现行 EXEC-023/024 规定：`permit → join`，
   agent mailbox 仅 Pulse，结果从 Journal 读；`ExecutorSummarizeRuntime.JoinWithPermit`
   每次 join 前重新取得 `FamilyRecoveryPermit`（map/reduce 改变 family closure digest）。
3. **SSE 心跳（周期扫描）**：`HostSignalSubscribe.fs` 以 `setInterval(15_000)` 定期扫描
   `Date.now() - state.lastEventMs > 30_000`，并另有指数退避 reconnect 循环。
4. **跨进程锁等待**：`IntegrationGate.fs` 依赖 `proper-lockfile` 的 50 次 100–500 ms 重试。

## 分类：等待不是同一件事

本候选把「等待」显式分为四类，分别适用不同规则，避免用一个统一方案误伤：

| 类别 | 定义 | 判定规则 |
|------|------|----------|
| A. 业务状态探测 | 控制流为获知**业务事实**而反复读取（snapshot / projection） | 必须有界（因果重读上限）；不得以墙钟退避推进 |
| B. 事件等待 | 控制流阻塞于**真实物理信号**（TCS / journal waiter / process signal） | 事件驱动，零轮询；允许注入可取消 timer 做 deadline |
| C. Deadline / watchdog | 距上次因果进展的静默时长判据（VERIFY-004） | 允许墙钟，但须集中、可取消、可注入测试 |
| D. 跨进程互斥等待 | 多进程竞争单一物理资源（publish lock） | 保持 cross-process 合同；另行裁决，不并入本候选 |

本候选只处理 A（收敛到有界因果重读 + B 事件等待）与 B（Executor 定向等待）、C（SSE
周期扫描 → one-shot deadline）。D 移出范围，见 Non-Goals。

## Proposed delta

1. **Reconciler：保留因果重读，废除无界退避轮询**
   - `ReconcileProgram.decideStep` 的 `RereadWithBackoff` 改为有界 `Reread`（携带剩余
     因果重读次数，上限 = HOST-004 的 3 次），不携带时间信息；删除 `pickDelay` /
     `nextBackoffIndex` 的时间语义，删除 `Scheduler` 的 `delays` 数组与
     `budgetMs` 墙钟预算。
   - 重读用尽仍非终态（early-idle 窗口过后仍 `Unknown`）：保持 Dirty latch，本 pass
     结束，等待**同一 signal 之外的下一粗粒度信号**（`idle` / `retry` / `deleted`）
     重新入队——这是 HOST-004 现有语义的落点，不发明新唤醒源。
   - 明确 `StopPass` 语义：`takeWork` 消费 queued 后即释放 active，绝不把 session
     重新放回 runnable queue 立即重跑（那会退化成无 delay 的 busy loop）。
   - 保留 `reconcile-idle-early.test.mjs` 的三个回归（同 Kick 内因果重读达终态、预算
     耗尽后第二信号恢复、连续 snapshot error 后 Ok 重置），只把「预算」改写成
     「因果重读次数」。
2. **Executor：targeted、permit-gated、Journal-authoritative await**
   - `IExecutorRuntime` 增加
     `AwaitAgentWithPermit(agentId, timeoutMs): Task<Result<RunCompletion, ForkError>>`：
     fresh `FamilyRecoveryPermit`（复用现有 `requirePermit` 路径）→ 定向等待指定
     agent → 完成后按 EXEC-024 从 Journal / 既有 completion 投影读结果。
   - 删除 `ExecutorSummarize.awaitAgent` 的 `while` 循环与局部 `stash`；`summarizeSpool`
     的并行扇出改为每 chunk 一个定向 `AwaitAgentWithPermit`。
   - 不新建第二份 `RunCompletion` 真理源：TCS / Pulse 仅作唤醒机制；completion 的
     权威仍在 Journal + `validatePermit`（`HostForkRuntime`）。`ForkRuntime.AwaitAgent`
     与 `HostForkRuntime.AwaitAgent` 已存在，本候选在其上补 permit 门，不另造 cell。
3. **SSE：周期扫描 → one-shot silence deadline**
   - 删除 `setInterval` 周期扫描；每次 `onEvent` 时 `clearTimeout` + 重置单次
     `setTimeout`（silence deadline），无事件时不触发回调。
   - reconnect 的指数退避保留（它是传输层事件等待，不是业务轮询），但必须使用
     本候选的 `ITimerPort` 并支持取消。
4. **`ITimerPort`（测试/资源生命周期抽象，不消灭墙钟）**
   - 合同至少包含：`delay(ms)`、`cancel/dispose`、ref/unref policy（继承
     `PtyTiming` 长 timer `.unref()` 的 event-loop 生命周期语义：长预算不得持住
     干净进程）。
   - 生产实现 = Node timer；测试实现 = 虚拟时钟注入，满足 VERIFY-004 的
     「watchdog / deadline 允许墙钟，但须集中、可取消、可注入」。

### Non-goals

- 不删除 Reconciler 的因果重读（early-idle 窗口保护），不发明「terminal 可见后必有
  第二 signal」的更强顺序保证。
- 不把 Executor 完成改为纯内存 cell 真理源（EXEC-023/024 不变）。
- 不重写 `IntegrationGate` / `proper-lockfile` 的 cross-process 语义；如确需，另立
  候选，并先定义 owner crash 回收、stale lock、多实例单一 publish gate 合同。
- 不宣称「消灭全部墙钟」：VERIFY-004 明确允许 wall-clock backstop，以「距上次因果
  进展的静默时长」为判据。

## Impact map

- what：`docs/what/host.md`（HOST-001/002 信号分层不变；如需在 what 层写 Reconciler
  行为约束，从 how 提升，不暗示 what 已禁止 reread）。
- how：`docs/how/host.md`（HOST-004：3 次因果重读 → 明确「用尽即保持 Dirty 等下一
  信号」，删时间预算表述）；`docs/how/dsl-structured-program.md`（等待语义分类）。
- shape：`docs/shape/execution.md`（EXEC-023/024：补 targeted await 的 permit 门表述）。
- proof：`docs/proof/host.md`（Reconciler 零无界轮询）；`docs/proof/execution.md`
  （Executor targeted await 契约测试）；`docs/proof/verify.md`（ITimerPort 注入面）。
- code：
  - `src/Wanxiangshu/Domain/ReconcileProgram.fs`（`RereadWithBackoff` → 有界 `Reread`；
    删 `pickDelay` / `nextBackoffIndex` 时间语义）
  - `src/Wanxiangshu/Application/Reconciliation/Reconciler.fs`（删 `delays` / `budget` /
    `delayMs`；`StopPass` 语义落点）
  - `src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorSummarize.fs`（删 `while` /
    `stash`）
  - `src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorSummarizeRuntime.fs`（新增
    `AwaitAgentWithPermit`）
  - `src/Wanxiangshu/Session/ForkRuntime.fs`、`src/Wanxiangshu/Session/HostForkRuntime.fs`
    （permit-gated targeted await 落点）
  - `src/Wanxiangshu/Infrastructure/OpenCode/Signals/HostSignalSubscribe.fs`（one-shot
    deadline）
  - `src/Wanxiangshu/Process/PtyTiming.fs` → `ITimerPort`（含 ref/unref policy）
- tests：`tests/unit/execution/reconcile-idle-early.test.mjs`（预算 → 因果重读次数）、
  `tests/unit/execution/join-v2-*.test.mjs`、`tests/unit/execution/handle.test.mjs`
  （Executor targeted await）、新增 ITimerPort 注入测试。

## Alternatives

1. **完全删除 reread，纯等下一信号**：拒绝。early-idle 竞态无第二信号保证，会
   重新引入 completion-loss（`reconcile-idle-early.test.mjs` 现证）。
2. **维持无界退避轮询**：拒绝。30s 墙钟预算 = A 类业务状态探测以时间推进，违背
   HOST-004 的「3 次因果重读」既有上限。
3. **Executor 直接 await 内存 TCS**：拒绝。绕开 EXEC-023/024 的 permit → join 与
   Journal-authoritative completion，产生第二份 `RunCompletion` 真理源。
4. **统一 ITimerPort「消灭墙钟」**：拒绝。VERIFY-004 允许 wall-clock backstop；
   ITimerPort 定位是集中、可取消、可注入，不是消除时间。
5. **本次顺带重写 IntegrationGate**：拒绝。cross-process 锁等待需先定义 crash 回收
   / stale lock / 单一 gate 合同，属 D 类，另行裁决。

## Migration / cutover

1. Reconciler：`RereadWithBackoff` → 有界 `Reread`；删 `delays` / `budgetMs` /
   `delayMs`；`StopPass` 后 active 释放、queued 已消费，等下一信号。
2. Executor：`IExecutorRuntime` 增 `AwaitAgentWithPermit`；`summarizeSpool` 改定向
   await；删 `stash` / `while`。
3. SSE：`setInterval` → one-shot silence deadline（`clearTimeout` + 重置）；reconnect
   退避迁移到 `ITimerPort`。
4. `ITimerPort`：先落地 `PtyTiming` 之上的接口与虚拟时钟测试实现，再逐调用点替换。
5. Clean break：不保留 `RereadWithBackoff` / `stash` / `setInterval` 心跳兼容分支。

## Compatibility disposition

CleanBreak（对 A/B/C 三类）；D（IntegrationGate）不在本候选，保持现状。

## Proof plan

1. **early-idle / 无第二信号**：单 Kick 内因果重读（≤3 次）内 transcript materialize
   → 同一 Kick 发布 TurnCompleted；重读用尽仍 Unknown → 保持 Dirty 零轮询，注入
   第二 `SessionIdle` → 恢复并发布。两个用例都不得依赖墙钟推进。
2. **Reconciler 零无界轮询**：无新信号时，Kick 后除有界因果重读外无任何
   `setTimeout` / `GetMessages`；故意恢复旧退避分支时该测试必须红。
3. **Executor permit-gated targeted await**：乱序完成 N 个 agent，`AwaitAgentWithPermit`
   只返回目标 agent；每 join 重新 `requirePermit`；`FamilyBlocked` 硬失败、
   `FamilyWaiting` 等待不误报；completion 从 Journal 投影读，不构造内存真理源。
4. **SSE one-shot deadline**：连续推包零超时触发；停止推包至阈值恰好触发一次；
   timer 不持有事件循环（干净进程自然退出）。
5. **ITimerPort 注入**：虚拟时钟推进下 watchdog / deadline 判定与真实 timer 等价；
   cancel/dispose 后回调零触发。
6. **门禁**：`npm run lint` 全绿；相关 unit / integration 通过；不调整任何静默窗口
   或超时常量。

## Decision owner

Wanxiangshu 项目 Owner。

## Admission blockers

- Owner 需确认 HOST-004「3 次因果重读」上限与 early-idle 窗口保护的保留（本候选不
  寻求任何更强的 Host happens-after 顺序保证）。
- Owner 需确认 Executor targeted await 的 permit 获取频率（每次 join 重新
  `requirePermit`）与 map/reduce 并发扇出的成本权衡。
- Owner 需确认 `ITimerPort` 的 ref/unref policy 边界（长预算不持住进程 vs 短预算
  保持事件循环，`PtyTiming` 现有阈值语义的迁移）。

## Active work

启动：Owner 已裁决实施。范围限定本提案（A/B/C 三类，D 移出）。
Reconciler 语义迁移已完成：`RereadWithBackoff`/`pickDelay`/`nextBackoffIndex`/`delays`/`budget`/`delayMs` 已删除，改为 `maxCausalRereads` 有界因果重读；`decideStep (rereadsRemaining) (evidence)`。
剩余：Executor targeted await（AwaitAgentWithPermit + 删 while/stash）、SSE one-shot deadline、ITimerPort、proof 闭环、归档。

## Final outcome

- **完成范围**：A/B/C 三类（Reconciler 有界因果重读、Executor targeted permit await、SSE one-shot deadline）；D 类（IntegrationGate）按 Non-Goal 未处理。
- **Reconciler**：`RereadWithBackoff`/`pickDelay`/`nextBackoffIndex`/`delays`/`budget`/`delayMs` 已删除，改为 `maxCausalRereads` 有界因果重读（`decideStep (rereadsRemaining) (evidence)`）；`StopPass` 后保持 Dirty 等下一信号，不立即重入 runnable queue；另补 `maxConsecutiveErrors`（默认 5）连续 SnapshotError 上限，消除持续报错时的无限递归（改造删除时间预算后 Error 分支失去终止条件的回归修复）。
- **Executor**：`IExecutorRuntime` 增 `AwaitAgentWithPermit(agentId, timeoutMs)`，`asExecutorRuntime` 经 `requirePermit` 门（RECOVERY_WAITING→TimedOut），`HostForkRuntime.AwaitAgentWithPermit` 先 `validatePermit` 再定向 await；`ExecutorSummarize.awaitAgent` 的 while 忙等待与 stash 字典删除，`summarizeSpool` 改每 chunk 定向 await；completion 权威仍在 Journal + validatePermit，无第二份 RunCompletion 真理源。门禁 `p0-recovery-join.mjs` 的 `executor-summarize-join-with-permit` 规则 pattern 扩展为 `JoinWithPermit|AwaitAgentWithPermit`。
- **SSE**：`HostSignalSubscribe.fs` 删除 `setInterval` 周期扫描，改 one-shot silence deadline（每次 onEvent `clearTimeout` + 重置单次 `setTimeout`），`sse-heartbeat-timeout` 致命语义保留；reconnect 指数退避保留。
- **ITimerPort**：`PtyTiming.fs` 定义 `ITimerHandle`/`ITimerPort` + 生产 `nodeTimerPort`（`ms>=1000` 时 `.unref()`）+ 虚拟时钟 `createVirtualTimerPort`；5 处 `let mutable` 补 `// DSL-MUTABLE:` 声明；`TrySetResult`→`AsyncSupport.trySetResult`（Fable 兼容）。SSE 心跳与 reconnect 经 ITimerPort 注入（生产=nodeTimerPort，测试=virtualTimerPort）：心跳 one-shot silence deadline 用 port.Delay + state.heartbeatHandle + .Cancel，reconnect 退避用 port.Delay；dispose 取消 handle + port.Dispose。
- **门禁修复**：`dsl-ownership-ratchet.mjs` 的 `scanRoot` 路径规范化 bug（`relative(root,file)` 丢失 `/Process/` 段致 DSL-MUTABLE 失效）已修复（双路径策略），未更新 baseline。
- **文档**：`docs/how/dsl-structured-program.md` 等待语义分类（A/B/C/D）、`docs/how/host.md` HOST-004 明确用尽即保持 Dirty、`docs/shape/execution.md` EXEC-023/024 补 targeted await permit 门、`docs/proof/{host,execution,verify}.md` 增零无界轮询/targeted await 契约/ITimerPort 注入面条目。
- **测试**：`reconcile-idle-early.test.mjs` 三回归改为因果重读次数；`reconcile-supervisor.test.mjs` 清理 `maxBudgetMs`/`backoffDelaysMs` 残留、1c 改因果重读语义；新增 `EXEC_reconcile_persistent_errors_stop_pass_bounded`（连续错误有界终止）、`timer-port.test.mjs`（ITimerPort 虚拟时钟契约）；`p0-recovery-join-gate.test.mjs` 绿路径 fixture 更新。
- **验证**：`npm run check`（lint/build/test/integration）全绿；unit 1074/0；lint 含 spec-check/architecture/dsl-ownership/dsl-ownership-ratchet/p0-recovery-join 全绿。
- **已知限制**：D 类 IntegrationGate 未处理；无更强 Host happens-after 顺序保证（HOST-004 上限与 early-idle 保护保留）。

## Final outcome (REVISE 收尾)

- **Executor 契约测试**：`tests/unit/execution/executor-summarize.test.mjs` 新增 `summarizeSpool`/`AwaitAgentWithPermit` 行为契约测试（每 chunk 恰好一次定向 await、乱序完成只返回目标 agent、TimedOut/NotFound 失败收集 + cancelOwned、FamilyWaiting→TimedOut 不误报成功）。Proof plan #3 闭环。乱序用例用短 setTimeout（~30ms）确定性通过，非虚拟时钟但非墙钟敏感。
- **文档一致性修订**：`docs/how/dsl-structured-program.md` 明确 SSE 心跳与 reconnect 属 C 类、经 ITimerPort 注入（生产=nodeTimerPort，测试=virtualTimerPort），cancel/dispose 后回调零触发；`docs/how/host.md` HOST-004 消除「等…之外」措辞歧义（改为「保持 Dirty，等下一粗粒度信号（idle/retry/deleted）重新入队」）；`docs/proof/verify.md` ITimerPort 条注明 SSE 心跳与 reconnect 经 ITimerPort 注入。
- **domain.mjs 注释清理**：`tests/unit/support/domain.mjs` `reconcileSupervisor` 注释从旧墙钟语义（timer-backoff/wall-clock budget）改为 `maxCausalRereads`/`maxConsecutiveErrors`（无墙钟/timer backoff）。
- **已知独立 flaky**：`EXEC_025_three_teacher_calls_...`（student-teacher tool-loop）2500ms 超时为 student-teacher 链路（hanging-return 改造）引入的负载敏感 flaky，与本次改造零因果关联（该测试不经过本改造文件），当前连续多跑全绿；如再 flaky 应在 student-teacher 侧收紧确定性。
- **后续 ITimerPort 真迁移**：后续将 SSE 心跳与 reconnect 真正迁移至 ITimerPort 注入（提交 282824b3），消除早期"Node timer 语义等价"字面偏离；Proof plan #4 行为测试（连续推包零 fatal、静默达阈一次 fatal、dispose 后零回调）在虚拟时钟下闭环。
- **Executor FamilyWaiting 等待语义闭环**：`ExecutorSummarize.awaitAgentWithPermit` 从单次调用改为有界节流重试（`remainingMs` 预算，初始 `AwaitAgentTimeoutMs`）；`ForkError.TimedOut`（映射自 `RECOVERY_WAITING`/FamilyWaiting）在预算内以 `PtyTiming.timerTask`（≤100ms）节流重试直至 Ready，预算耗尽或 `ForkError.NotFound`（FamilyBlocked/真实超时）则硬失败。修正 `ExecutorSummarizeRuntime.fs` 误导注释。补充测试：`EXEC_summarize_spool_family_waiting_then_ready_succeeds`（Waiting→Ready→成功）、`family_waiting_timed_out_not_reported_as_success`（NotFound 硬失败）、`await_timeout_fails_chunk`/`cancel_owned_on_failure` 改 NotFound 触发（避免恒 TimedOut 挂起）。Proof plan #3「FamilyWaiting 等待不误报」完整闭环（等待=有界节流重试，不误报成功也不误报硬失败）。
