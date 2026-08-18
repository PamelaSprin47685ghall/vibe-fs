# HOW — time-capability（实现模型与约束，非 normative）

> 本文件解释实现，不另造 normative owner。WHAT 命题见 `WHAT.md`；测试落点见 `HOW.md`。

## 实现模型总览

```text
Kernel/Temporal.fs        接口合同（IClockPort / ITimerPort / IDeadlineHandle）——纯接口，无 JS 产物
Process/PtyTiming.fs      物理 + 虚拟实现（nodeTimerPort / nodeClockPort / virtual ports / timerTask）
Process/Deadline.fs       typed deadline（ofBudget / remaining / isExpired / nextWaitMs / MaxTimerWaitMs）
Process/ProcessRunner.fs  process 等待消费 deadline（物理层，允许直接采样 UtcNow）
Session/CompletionMailbox.fs  join 等待用注入 IClockPort + ITimerPort + Deadline.nextWaitMs 分段等待
Kernel/CausalWait.fs      WaitEscape.DeadlineAt（causal-wait 引用本包的 typed 时刻）
Execution/Session/SessionStartedAtProjection.fs  HOST-013 session 起点的 bounded projection
Execution/Session/SessionStartedAtLedger.fs      首次 prompt bind-once durable writer
Infrastructure/OpenCode/Plugin/PluginBoot.fs / PluginTransforms.fs  注入 IClockPort；新 occurrence 采 elapsed
```

### 1. `Kernel/Temporal.fs` — 纯接口合同

```fsharp
type IDeadlineHandle =
    abstract Delay: Task<unit>
    abstract Cancel: unit -> unit

type ITimerPort =
    abstract Delay: milliseconds: int -> IDeadlineHandle
    abstract Dispose: unit -> unit

type IClockPort =
    abstract UtcNow: unit -> DateTimeOffset
```

要点：

- `Cancel` 让 `Delay` **永久 pending**（一次可取消，不复活）。
- `ITimerPort.Dispose` 停止整个 port（生产=已用 latch；虚拟=清除 entries）。
- 接口文件无任何 Node / setTimeout / Fable JS —— 纯合同（Fable 对纯接口不产出 JS，`dist/Kernel/Temporal.js` 不存在是正常的）。

### 2. `Process/PtyTiming.fs` — 物理与虚拟实现

| 函数 | 角色 |
|---|---|
| `timerTask ms` | fire-and-forget 延时（无取消面；`raceExit` 用）。`ms ≥ 1000` → `.unref()`（长预算不得持住干净进程）；短预算保持 event loop（`node:test` 并发下 unref 的 timer 可能在 loop 已排空后才触发 → 「Promise resolution is still pending」CI 红）。 |
| `nodeTimerPort()` | 生产 `ITimerPort`：`setTimeout` + `clearTimeout`；`ms ≥ 1000` → unref；`Dispose` latch。 |
| `nodeClockPort()` | 生产 `IClockPort`：`DateTimeOffset.UtcNow`。 |
| `createVirtualTimerPort()` | 虚拟 timer：`Advance` 只触发 `fireAt <= nowMs` 的 handle；`Cancel` 移出 entries；`Dispose` 清空。 |
| `createVirtualClockPort()` | 虚拟时钟：起点 `2000-01-01T00:00:00Z`，`AdvanceMs` / `Set`。 |

`DSL-MUTABLE` 注释标注这些物理可变资源（timer latch、cancelled ref、cursor）——它们是被允许的物理 mutable，不承载业务控制流（`structured-workflow` 交叉）。

### 3. `Process/Deadline.fs` — typed deadline

- `Deadline of expiresAt` **私有构造**：业务无法读出时刻自行解释。
- `ofBudget now budget`：对 `DateTimeOffset.MaxValue` 截断防溢出。
- `remaining clock d`：`expiresAt - clock()`，负值钳 0。
- `isExpired clock d`：`clock() >= expiresAt`。
- `nextWaitMs clock d`：剩余毫秒封顶 `MaxTimerWaitMs = 0x7FFFFFFF`（JS Int32 setTimeout 上限 ≈ 24.8 天）；过期返回 0 → 长预算由调用方分段。
- `MaxTimerWaitMs` 用普通 `let` 而非 `[<Literal>]`：Fable 会把 literal 内联不导出，测试就读不到要断言的界。

### 4. 消费形态（不是本包拥有的业务语义）

- `Process/ProcessRunner.fs`（物理层）：`Deadline.ofBudget (clock ()) budget` + `waitForExit`，超时确定失败路径（EXEC-011，process-execution 拥有）。
- `Session/CompletionMailbox.fs`：join 等待注入 `ITimerPort` + `IClockPort`；budget 是绝对 `Deadline`，每次等待 `ITimerPort.Delay(Deadline.nextWaitMs clock expires)`；到期 → `DeadlineExpired` 中断（EXEC-025 机制，delegation/process 拥有业务意义）。
- `HostForkJoin.fs`：deadline handle 与信号 `Promise.race`，race 后 `Cancel()`。

### 5. HOST-013 SessionStartedAt / elapsed（TIME-007）

- `PluginBoot` 在 composition root 创建一次 `IClockPort`，`PluginTransforms` 只消费 `boot.Clock`；没有 ambient 时间读取。
- 每次 provider transform 入口在任何 `let!` 前同步采样 candidate。`SessionStartedAtLedger.bind` 对 session 的 bounded projection 做 O(1) lookup：已有值直接返回；缺失时 append `HostFact.SessionStartedAtBound(SessionId, StartedAt)`，fold 的 `SessionStartedAtProjection.bind` 永远保留第一值。
- journal append 成功后返回 fold 后的 canonical first value；bind 失败时当前 provider attempt fail closed，不能拿 process-local candidate 继续，因为 restart 后会换原点。
- 新 HOST-013 occurrence 再调用同一 `IClockPort.UtcNow()` 一次，`PairProgrammingCalibration.renderElapsed` 把 `max(0, now-startedAt)` 转成人类尺度；最终字节交给 guidance-delivery / prefix-stability 冻结。
- 不从 OpeningPrompt/XTrace/transcript 反推时间，不把 first marker time 当创建时间；这里“首次 prompt”就是 session 的 provider-facing 创建边界。

### 6. ambient 静态扫描（机制归 structured-workflow）

`scripts/checks/g4r-ce-vocabulary.mjs`：

```js
RAW_TIME_SCAN_LAYERS = ['Domain', 'Application', 'Session']
RAW_TIME_TOKENS = ['DateTimeOffset.UtcNow', 'DateTime.Now', 'DateTime.UtcNow', 'Date.now', 'setTimeout', 'timerTask']
```

- 生产侧红/绿由 `requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs` 证明（RED on synthetic tokens；`G4R_CE_S14_production_is_clean_in_hard_phase`）。
- allowlist 当前为空（物理适配器在 Process/Infrastructure 层，天然不在扫描层内）。
- **本包不重复拥有该 gate**；它证明的是「业务层无 ambient 时间」这条 TIME-004 事实，机制 owner 是 `structured-workflow` / `verification-system`。

## 历史与弃权

| 源 | 裁决 | 理由 / 落点 |
|---|---|---|
| 历史 change（ce-temporal-ownership，时间部分） | 吸收为 WHY 考古 | 五个时序 owner 分工 + `TurnCompletionProgram` 第二运行时教训 → `WHY.md` §失败模式 1；deadline 有界机制 → WHAT TIME-002 |
| 历史 change（reconciler-event-driven-de-polling，ITimerPort 部分） | 吸收为 HOW + WHY | 等待四分类（A/B/C/D）→ `WHY.md` §失败模式 2；C 类 deadline 注入 → TIME-001/003；分类表本身 → `causal-wait` HOW |
| 历史 loop 条款 | GARBAGE（对本包） | 全部 LOOP 条款归 `degeneration-guard`（循环检测/强杀）；无时间能力内容 |
| 历史 what/how orchestrator 条款 | GARBAGE（对本包） | ORCH-001/002/007/008 归 `change-integration` / `delegation`；无时间条款 |
| 历史 EXEC-001..032 中非时间条款 | 不消费 | 归 delegation / process-execution / work-record 等；本包只取 EXEC-004/011/025 的时间机制面 |
| `ProcessEstimate.effectiveDeadline`（min(3×estimate, HardLimit)） | HOW（归 process-execution） | deadline 的应用，非时间能力本身 |
| `ITimerPort` / `IClockPort` 名字、`DevOpsJoinTimeoutMs = 10_000` | HOW | 名字/数值可替换，不进入 WHAT |
| `g4r-ce-vocabulary` RAW_TIME 静态扫描 | 机制归他人，本包消费 | 见上文 §5 |

## 阅读实现代码的入口

```text
src/Wanxiangshu/Kernel/Temporal.fs        # 接口合同（先读）
src/Wanxiangshu/Process/Deadline.fs       # typed deadline（第二读）
src/Wanxiangshu/Process/PtyTiming.fs      # 物理/虚拟实现（第三读）
src/Wanxiangshu/Session/CompletionMailbox.fs  # 消费示例：注入 port + Deadline.nextWaitMs 分段
```

## DEPENDS ON

无。本包不依赖任何其它 package 的 guarantee（`requirements/INDEX.md` 依赖骨架 Phase E 结论）。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（已物理移入本包 `tests/`）/ `REUSE`（留在原处，记录 cutover 拆分）/ `NEW`（本包新写）。
> 运行命令：`node --test <file>` 单跑；`node requirements/verification-system/tests/run.mjs` 全单元（自动包含 `requirements/**/tests/*.test.mjs`）；`node scripts/check.mjs` 全部静态门。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| TIME-001 时钟/timer 显式注入 capability | `tests/timer-port.test.mjs` — `WHAT[TIME-003] VERIFY_004_virtual_timer_*`（Delay/Cancel/Dispose 契约）+ `tests/clock-port-virtual.test.mjs` — `WHAT[TIME-001] TIME_001_virtual_clocks_are_independent_not_ambient`（两个虚拟时钟互不影响） | MOVE + NEW | `node --test requirements/time-capability/tests/timer-port.test.mjs requirements/time-capability/tests/clock-port-virtual.test.mjs` |
| TIME-002 deadline/elapsed typed 表达 | `tests/deadline-typed.test.mjs` — `WHAT[TIME-002] TIME_002_deadline_of_budget_and_remaining_are_pure_clock_functions` / `WHAT[TIME-002] TIME_002_of_budget_clamps_to_datetime_max_no_overflow` / `WHAT[TIME-002] TIME_002_next_wait_ms_caps_at_js_timer_ceiling`（ofBudget/remaining/isExpired/nextWaitMs 全部经注入时钟纯函数消费；`MaxTimerWaitMs` 封顶）+ `tests/devops-join-timeout.test.mjs` — `WHAT[TIME-002] EXEC_025_join_deadline_expired_renders_waiting_ended_natural_language`（EXEC-025 deadline 机制面）+ `tests/process-output-deadline.test.mjs` — `WHAT[TIME-002] EXEC_011_*`（effectiveDeadline=min(estimate, HardLimit)、非有限/非正坍缩、DefaultHardLimit=1h）+ `tests/process-runner-estimate.test.mjs` — `WHAT[TIME-002] EXEC_011_rejects_*_estimate`（无效预算 spawn 前拒绝） | NEW | `node --test requirements/time-capability/tests/deadline-typed.test.mjs requirements/time-capability/tests/devops-join-timeout.test.mjs requirements/time-capability/tests/process-output-deadline.test.mjs requirements/time-capability/tests/process-runner-estimate.test.mjs` |
| TIME-003 虚拟化；测试替换物理时钟 | `tests/timer-port.test.mjs`（`WHAT[TIME-003] VERIFY_004_virtual_timer_*` 虚拟 timer 精确触发/cancel/dispose）+ `tests/clock-port-virtual.test.mjs`（`WHAT[TIME-003] TIME_003_virtual_clock_starts_at_fixed_epoch` / `WHAT[TIME-003] TIME_003_virtual_clock_advance_and_set_are_deterministic` 虚拟时钟推进/设定） | MOVE + NEW | 同上两文件 |
| TIME-004 业务层禁 ambient 时间 | REUSE：`requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs` — `G4R_CE_S0_raw_time_scanner_RED_on_synthetic_tokens`（合成 token 必红）+ `G4R_CE_S14_production_is_clean_in_hard_phase`（生产三层无 raw time）。机制归 structured-workflow，本包消费其 guarantee + NEW：`tests/ambient-time-forbidden.test.mjs` — `WHAT[TIME-004] domain_application_session_contain_no_raw_time_tokens`（本包侧产品事实：三层扫描命中零）+ `WHAT[TIME-004] business_layer_scan_is_not_vacuous_across_a_clean_tree`（消费非盲目：合成业务层文件必红；扫描层=Domain/Application/Session） | REUSE + NEW | `node --test requirements/time-capability/tests/ambient-time-forbidden.test.mjs`；`node --test requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs` |
| TIME-005 时间值不是 authority | `tests/deadline-typed.test.mjs` — `WHAT[TIME-005] TIME_005_verdict_follows_injected_clock_not_value`（同一 deadline 两个注入时钟给出不同判定；`Deadline` 无公开时刻访问器）+ `tests/clock-port-virtual.test.mjs` — `WHAT[TIME-005] TIME_005_deadline_verdict_uses_injected_clock_view` + `tests/temporal-virtual-clock.test.mjs` — `WHAT[TIME-005] TEMPORAL_virtual_clock_*`（harness 虚拟时钟：time is input, never authority） + REUSE：`requirements/verification-system/tests/support/temporal-harness.mjs`（`One World / Pure Time`：Time is input, never authority，供 temporal 定理） | NEW + REUSE | `node --test requirements/time-capability/tests/deadline-typed.test.mjs requirements/time-capability/tests/clock-port-virtual.test.mjs requirements/time-capability/tests/temporal-virtual-clock.test.mjs` |
| TIME-006 deadline 是 causal-wait 的可选 escape | `tests/until-signal-or-deadline.test.mjs` — `WHAT[TIME-006] THEOREM_untilSignalOrDeadline_deadline_without_material_is_WaitTimedOut`（IDeadlineHandle 作为等待 escape；SPLIT@cutover：CausalAwait 词汇归 causal-wait，deadline 语义归本包） | REUSE（SPLIT@cutover） | `node --test requirements/time-capability/tests/until-signal-or-deadline.test.mjs` |
| TIME-007 首次 prompt bind-once SessionStartedAt；新 occurrence fresh elapsed | `tests/pair-session-elapsed.test.mjs` — `WHAT[TIME-007] TIME_007_*` + `WHAT[TIME-007] GD_012_*`（pure + durable bind once / bounded projection no-scan-no-mutable / clamp / 双语 human-readable / occurrence fresh + old MarkerText immutable / composition order） | NEW（FROZEN 2026-08-14） | `node --test requirements/time-capability/tests/pair-session-elapsed.test.mjs` |

### 关联 REUSE 落点（边界消费方，不重复拥有）

| 场景 | 落点 | owner |
|---|---|---|
| EXEC-025 DevOps 10s → `DeadlineExpired` 自然语言 | `requirements/participant-horizon/tests/devops-join-timeout.test.mjs`（`devops_join_deadline_renders_natural_language_not_timed_out_dto`） | 本包（deadline 机制面）+ `delegation`（join 中断面）SPLIT@cutover |
| EXEC-011 process deadline 有界、超时确定失败 | `requirements/process-execution/tests/process-runner.test.mjs`（`EXEC_011_*`）、`requirements/process-execution/tests/process-output.test.mjs`（`effectiveDeadline`） | `process-execution`（本体）+ 本包（deadline 输入） |
| join 分段等待（注入 IClockPort/ITimerPort + nextWaitMs） | `requirements/process-execution/tests/process-wait.test.mjs`、`tests/unit/session/host-fork-*.test.mjs` | `delegation` / `process-execution`（消费） |
| G4R temporal 定理（虚拟时间证明） | `requirements/time-capability/tests/temporal-virtual-clock.test.mjs` 等（harness 虚拟端口） | 各业务 owner；本包只提供虚拟时间能力 |

### 运行与红/绿判读

- 单跑：`node --test requirements/time-capability/tests/<file>`。任一断言失败 → 该命题的当前世界 RED。
- 全单元：`node requirements/verification-system/tests/run.mjs`（自动包含 `requirements/time-capability/tests/**`）。
- 静态门：`node scripts/check.mjs`（含 `causal-wait-boundary.mjs`、`test-boundary.mjs`、`g4r-ce-vocabulary.mjs` 等）。

### Semantic anchor ids

本包在 `scripts/checks/semantic-anchors.mjs` 中**不拥有**任何 semantic ID（该 catalog 的 owner 为 cognitive-environment / office-capability / action-affordance / epistemic-reasoning / review-judgement）。本包的 anchor 证据是静态 gate 扫描（g4r-ce-vocabulary）与行为测试，不是 prompt 散文锚点。
