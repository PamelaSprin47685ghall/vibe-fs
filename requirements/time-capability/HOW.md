# HOW — time-capability（实现模型与约束，非 normative）

> 本文件解释实现，不另造 normative owner。WHAT 命题见 `WHAT.md`；测试落点见 `PROOF.md`。

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
