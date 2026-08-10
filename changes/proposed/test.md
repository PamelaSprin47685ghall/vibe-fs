# Proposal G4R — 明显没有 Bug

## One World, Pure Time：唯一真实长河与纯时序证明体系

**Status:** Proposed / G4 Exit Blocker
**Scope:** Runtime / Testing / Proof / Liveness / Performance
**Supersedes:** 现有多-canary E2E topology、E2E parallel pool、E2E shuffle/repeat、以真实调度覆盖 race 的证明方式

---

# 0. 判词

当前问题不是“测试太慢”。

也不是“某几个 flaky case 要修”。

真正的问题是：

> **我们正在让物理世界承担本该由逻辑承担的证明责任。**

于是一个业务命题被翻译成：

```text
启动 OpenCode
启动 Host
启动 Session
启动 mock provider
启动 timers
启动 filesystem/Git observation
等待 scheduler
等待 background lane
等待 watchdog
然后看看正确事件是不是碰巧先发生
```

测试一旦依赖这种结构，flake 并不是测试基础设施的偶然瑕疵，而是在告诉我们：

> **代码里的因果关系尚未强到可以脱离墙钟与 scheduler 被直接阅读。**

现有证据已经足够明确：主 suite 曾达到 31 个 E2E，而当前报告仍出现 `manager-unhappy-path` 单 case 90 秒 process timeout；同一 case 的历史失败形态还包括 expectation timeout、no-declared-turn、join-guard/blogger 死循环和 harness/production divergence。 当前最终实测也因此只能得到 30/31，不能签 G4 Exit。

与此同时，过去真正有效的性能改善，全部来自**消灭真实成本**，而不是放宽 timeout：EventStore ODB、事件驱动 quiescence 等曾将 E2E 墙钟从 104.4s 压到约 33–36s。

这证明方向是对的，但还不够彻底。

最终必须进一步收敛到：

```text
真实世界：1 个
OpenCode：启动 1 次
E2E：1 个
墙钟 race proof：0 个
```

---

# 1. 哲学：君子不立危墙

所谓“明显没有 Bug”，不是宣称软件可以数学意义上绝无缺陷。

它的含义是：

> **一个工程师不需要在脑中模拟几十条线程、几十个 timeout 和一次幸运的 scheduler interleaving，才能相信代码。**

如果一个状态“不应该存在”，最好使它无法表达。

如果一个事实只有一个 owner，就只能有一个 writer。

如果两个事件存在 precedence，就把 precedence 写成规则。

如果两个事件没有 precedence，就证明二者交换后收敛。

如果一个行为由时间触发，就把时间作为输入。

如果一个行为依赖某件事完成，就等待那件事，而不是猜它大约多久完成。

如果一个 race 可以枚举，就枚举它，而不是实际制造 race。

**Signal wakes; Fact decides.**

**Time is input, never authority.**

**One fact, one owner, one writer.**

**Race is algebra, not scheduler lottery.**

现有架构原则本来已经要求业务控制流由 `let! / do! / match / bounded recursion / computation expression` 表达，并明确禁止用 `CurrentStage`、`NextAction` 等领域字段保存程序计数器；Kernel/Domain 应保持纯，Application 承担 CE workflow，Infrastructure 才拥有 Host/Git 等物理能力。

本 proposal 只是把同样的原则贯彻到 proof architecture。

---

# 2. 新的绝对规则：整个仓库只有一个 E2E

最终仓库只能存在：

```text
tests/e2e/entry.test.mjs
```

以及它真正必要的 data / mock-provider support。

整个 release test graph 中：

```text
E2E test count      == 1
OpenCode start count == 1
OpenCode lifetime    == 1
physical world count == 1
```

禁止：

```text
第二个 E2E case
第二次 OpenCode spawn
每 scenario 创建一个 world
parallel E2E worker pool
E2E shuffle
E2E retry
E2E repeat-until-pass
以多次运行获得 race coverage
```

这必须是 static ratchet，而不是约定。

测试退出前还必须硬断言：

```text
assert.equal(opencodeSpawnCount, 1)
```

---

# 3. 但这个唯一 E2E 绝不是 happy path

唯一 E2E 的意义不是：

> OpenCode 能启动，Manager 做个简单任务，然后成功退出。

那种测试价值太低。

真正的唯一 E2E 应该是一条：

> **长寿、连续、坎坷、受伤、恢复、返工、被打断、发生冲突、最终仍然有序收敛的一笔画。**

它不是 smoke test。

它是：

# The Long Stroke

一整个真实 Wanxiangshu 世界，从出生到死亡，只出生一次。

---

# 4. The Long Stroke 的故事

一个真实 OpenCode 启动。

之后始终使用同一个 Host 世界，不 restart OpenCode。

这条人生至少经历以下连续剧情：

```text
Birth
  ↓
Manager Activation
  ↓
开始真实工作
  ↓
fork child
  ↓
child 尚未完成时发生其它输入
  ↓
Manager 进入 join / wait
  ↓
外部 user message 到来，唤醒阻塞链
  ↓
继续工作
  ↓
provider 发生一次确定性的 transient failure
  ↓
fallback 到另一个 provider/model path
  ↓
恢复并继续，不重复记账
  ↓
产生 candidate result
  ↓
Reviewer 审查
  ↓
REVISE
  ↓
Manager 接收批评
  ↓
重新 fork / 修正
  ↓
期间一个 child/session 被 abort / interrupted
  ↓
durable facts 仍保持一致
  ↓
系统从正式事实继续，而不是依赖遗留内存
  ↓
再次 review
  ↓
PERFECT
  ↓
Manager 请求 finality
  ↓
故意仍存在 outstanding child / pending join / live resource
  ↓
Finality 被正确拒绝或延后
  ↓
资源自然收敛
  ↓
再次 finality
  ↓
进入 publish
  ↓
在 publish 前人为移动真实 Git target
  ↓
产生 stale-head / conflict
  ↓
Orchestrator 根据 durable fact reconcile
  ↓
重新取得合法 publish 权
  ↓
publish 成功
  ↓
出现一次独立 idle occasion
  ↓
nudge / continuation 正确发生
  ↓
再完成一段 minor continuation
  ↓
最终 suicide / LifeCompleted
  ↓
所有 Session quiescent
  ↓
所有 process/resource ownership 清空
  ↓
EventStore 最终 invariant 检查
  ↓
OpenCode clean shutdown
```

这里必须明确：

**故事很长，墙钟必须很短。**

“超长”指的是：

```text
语义经历丰富
因果链长
状态转折多
```

不是：

```text
sleep 很多
timeout 很大
等一分钟
```

---

# 5. 唯一 E2E 必须“坎坷”，但不得承担组合证明

Long Stroke 中主动放入 adversity，是为了证明：

> **真实 production composition 遇到不顺利的世界时，仍然真的接得起来。**

因此它至少应该真实经历几类不同故障轴：

```text
provider failure
review rejection
user interruption
join blocking/wakeup
child/session interruption
finality temporarily forbidden
Git publish conflict
recovery from durable facts
```

但它只走每类故障的一条代表路径。

例如它可以真实经历：

```text
failure → fallback B → success
```

但不应该在 E2E 里继续证明：

```text
A,A,B,A
A,B,A,A
B,A,A,A
cancel 在第 1 个 completion 前
cancel 在第 2 个 completion 后
owner abort 与 blogger abort 的全部排列
```

这些属于纯时序证明。

**E2E 负责证明“世界能经历风雨”。**

**Temporal proof 负责证明“所有合法风雨排列都有定义”。**

二者不可混淆。

---

# 6. 为什么一条坎坷长河比 31 个 E2E 更强

31 个短命 world 实际验证的是：

```text
出生
做一件事
死亡

出生
做另一件事
死亡

出生
再做一件事
死亡
```

这会不断重置大量 process-local 状态。

因此反而很难发现：

```text
旧 subscription 泄漏
旧 session ownership 残留
缓存跨生命周期污染
后台 lane 没有退役
旧 handle 在新 Life 中复活
同一个 Host 连续经历多次 failure 后状态漂移
资源逐渐积累
```

唯一 Long Stroke 恰好反过来。

它让同一个世界经历：

```text
success
failure
recover
REVISE
abort
join
idle
conflict
retry by semantics
finality
continuation
shutdown
```

因此它真正证明：

> **这是一个可以长寿的 production world，而不是一批每次活十秒就重新投胎的 canary。**

---

# 7. E2E 只能观察正式语义，不能观察内部舞步

当前复杂 canary 的另一个问题，是 harness 越来越知道 production 内部 choreography。

这已经产生过明显风险：`manager-unhappy-path` 的历史失败在 harness divergence、join-guard/blogger、expectation 等多种形态之间移动。

新的 Long Stroke 禁止依靠内部程序计数器式 expectation 驱动故事。

允许观察：

```text
durable fact
public tool result
provider-visible request
user-visible response
Git ref / object result
session lifecycle contract
final ownership invariant
```

禁止把下面这些变成测试 authority：

```text
“现在应该刚好进入内部第 7 步”
某后台 lane 此刻应该先 emit 某内部 token
某 internal helper 必须在另一个 helper 前 N ms 执行
为了推动剧情直接操纵 production 私有状态
```

Harness 可以**注入外部世界事件**。

Harness 不可以**导演 production 内部动作**。

---

# 8. 其它所有现有 E2E 全部降为 Pure Temporal Proof

现行 VERIFY-002 已经给出了正确方向：

```text
1. 纯状态
2. 单边界集成
3. 确定性事件重放
4. 单 canary
5. release gate
```

并明确写有“单 canary”。

现在应把它贯彻到底。

所有当前以 E2E 表达的：

```text
manager-unhappy
manager-full-loop
fallback traces
context recovery
temporal ownership
review lifecycle
restart / recovery
orchestrator conflict
finality cohort
join guard
idle occasion
```

除 Long Stroke 中选出的代表故事之外，全部改写成：

```text
pure state law
temporal trace theorem
deterministic workflow test
single-boundary adapter contract
```

---

# 9. Pure Temporal Proof 的定义

“纯时序”不是重新实现一个 test-only Manager。

绝对禁止：

```text
TestManagerStateMachine
FakeReviewBusinessLogic
TestJoinSemantics
ExpectedRecoveryImplementation
```

否则只是创造第二套 production。

测试必须调用**同一套正式 production rule / CE workflow**。

测试世界只替换物理 ports：

```text
Real production:
    Node clock
    real OpenCode
    real process
    real Git
    real provider transport

Temporal world:
    VirtualClock
    DeterministicCompletion
    InMemoryEventStore
    RecordedProviderPort
    ExplicitEventQueue
```

业务逻辑一份。

composition 两份。

---

# 10. Race 从“跑出来”改成“列出来”

以后禁止这种 race proof：

```text
setTimeout(A, 20)
setTimeout(B, 20)

跑 100 次
希望两个顺序都见到
```

改为显式 trace：

```text
Trace 1:
    A
    B

Trace 2:
    B
    A
```

然后由 production code 计算。

如果 A/B 相互独立：

```text
fold(A ; B) == fold(B ; A)
```

必须成立。

如果存在 precedence：

```text
A > B
```

那么两个输入排列都必须产生规范规定的唯一结果。

因此 race correctness 被重新定义为：

> **有限事件代数上的 confluence / precedence property。**

---

# 11. “Flaky”从此应成为架构错误，而不是测试分类

如果同一个 deterministic temporal test 两次结果不同：

这是纯 bug。

如果必须依赖 wall clock 才能重现某个逻辑 race：

这是边界设计 bug。

如果一个测试必须 `repeat 3` 才让人放心：

证明层次错了。

如果需要提升 timeout：

先假定 causal structure 有问题。

现有 proof 文档已经禁止通过 widening window 隐藏 race，并禁止让额外 total timeout 与 causal watchdog 竞争。

本 proposal 将其提升为更强规则：

> **所有需要概率重复才能获得信心的 semantic test，一律不得作为 release proof。**

---

# 12. Restart 也不应需要重新启动 OpenCode

许多所谓 restart test 真正要证明的不是：

```text
Node process 能启动第二遍
```

而是：

```text
Ephemeral memory disappears
Durable facts remain
New runtime projection is reconstructed
Continuation remains correct
```

因此 pure temporal world 应能执行：

```text
world1
→ durable facts F

DROP EPHEMERAL CELLS

world2 := recover(F)

→ continue
```

然后证明：

```text
no duplicate publish
no duplicate completion
ownership preserved
retired handle not resurrected
terminal exactly once
```

真正 Node/OpenCode 能否 compose 已由唯一 Long Stroke 证明。

**为了测试 recovery 而再次启动 OpenCode，属于重复证明物理边界。**

---

# 13. Time 必须彻底退居 Port

任何 semantic unit / temporal test 都禁止：

```text
DateTimeOffset.UtcNow
Date.now()
setTimeout
sleep
real timer deadline
```

时间应该是：

```text
ITimerPort
VirtualClock
AdvanceBy(...)
```

所以测试：

```text
timeout after 5s
```

实际执行：

```text
clock.advanceBy(5s)
```

墙钟消耗近似零。

只有唯一 Long Stroke 和单边界 timer adapter contract 可以接触真实时间。

---

# 14. Adapter Contract：物理事实各证明一次

真实边界仍然需要测试，但它们不叫 E2E。

例如：

```text
Node timer adapter
Process exit / kill adapter
PTY adapter
Git ODB byte identity
resource loading
package loading
```

每个 contract 只回答一个问题：

> physical implementation 是否满足 semantic port contract？

现有 `object-identity` 已经是正确例子：它用真实 git binary 验证我们产生的 blob/tree oid 与 Git 一致。

这种测试很好。

但绝不能在一个 Git adapter contract 里顺便重新跑整个 Manager Life。

---

# 15. 新测试金字塔

最终不是普通金字塔。

而是：

```text
                     ┌──────────────────┐
                     │ 1 × Long Stroke  │
                     │ 1 OpenCode world │
                     └──────────────────┘
                              ▲
                  ┌────────────────────────┐
                  │ tiny adapter contracts │
                  └────────────────────────┘
                              ▲
             ┌────────────────────────────────┐
             │ deterministic temporal proofs  │
             │ race / recovery / lifecycle    │
             └────────────────────────────────┘
                              ▲
        ┌──────────────────────────────────────────┐
        │ pure laws / fold / projection / algebra │
        └──────────────────────────────────────────┘
```

越接近物理世界：

```text
数量越少
组合越少
自由度越少
```

越接近纯逻辑：

```text
case 越多
排列越多
证明越彻底
运行越快
```

---

# 16. Long Stroke 的性能纪律：剧情长，墙钟短

这一点必须写成硬门。

现有测量表明，**纯 OpenCode + strict mock** 的一次 turn 总耗时大约只有 95ms。

因此一个包含许多语义转折的 E2E，不应该自然需要几十秒。

目标：

```text
Pure law suite              < 1s
Temporal workflow suite     < 2s
Adapter contracts           < 3s
The Long Stroke             < 6s

npm test / semantic full    < 10s
```

这里不是允许四项简单相加到 12s。

它们应合理并行，真正 release critical path：

```text
< 10s
```

而唯一 E2E 自身：

```text
target < 6s
```

如果 Long Stroke 需要 20s、40s、90s：

不要改成：

```text
timeout = 120000
```

而要问：

```text
真实成本在哪里？
为什么这个 causal transition 需要这么久？
是不是又有 synchronous process？
是不是轮询？
是不是重复读 whole EventStore？
是不是测试在等待没有语义意义的内部状态？
```

过去从 104s 到 33s 的改进已经证明，真正的性能问题应该通过消灭物理浪费解决，而非 padding。

---

# 17. Long Stroke 的 watchdog

唯一真实 E2E 仍然需要 watchdog。

但只有一个语义：

> **距离最后一次真正 causal progress 已经多久？**

不是：

```text
test 总共跑多久
有 SSE byte 就算 progress
后台 blogger 在动就算 progress
```

已有 VERIFY-004 关于 causal progress、background progress、禁止 competing total deadline 的哲学继续保留。

但是旧的：

```text
MAX_PARALLEL
CANARY_STARTUP_WIDTH
bark stagger
parallel worker pool
```

全部废除。

它们只是旧的多-world topology 的事故性机制。

当前静态测试甚至明确钉死 `MAX_PARALLEL workers` 和 startup-width bark chain。

这些 static gate 必须删除或重写。

不能让旧 harness 的实现细节变成永恒法律。

---

# 18. VERIFY-001 / 002 / 004 修订

## VERIFY-001

改为：

```text
0. Static architecture/proof gates

1. Pure laws
   no Host / clock / process / network

2. Temporal workflow proof
   production workflow + deterministic ports
   explicit event traces
   exhaustive/bounded interleavings

3. Adapter contract
   exactly one physical boundary

4. The Long Stroke
   exactly one real OpenCode E2E
   exactly one OpenCode lifetime

5. Release
   one deterministic full proof run
   build/package/packing
```

---

## VERIFY-002

改为：

```text
1. Pure law
2. Deterministic temporal trace
3. Single physical adapter contract
4. One Long Stroke
5. Release
```

禁止把 semantic branch 直接晋级到 E2E。

如果某 branch 必须由 OpenCode 才能验证，作者必须回答：

```text
它到底依赖哪个不可模拟的 physical contract？
```

回答不出来：

REVISE。

---

## VERIFY-004

保留：

```text
causal progress
semantic watchdog
diagnostics before death
time injection
no fixed sleep
no timeout padding
no repeat-until-pass
```

删除：

```text
scenario parallelism
startup width
bark chain across scenarios
shuffle
三轮 E2E repeat
one process per canary
```

新增：

```text
VERIFY-004 One Physical World
VERIFY-004 OpenCode Spawn Exactly Once
VERIFY-004 Semantic Race Has Deterministic Trace Proof
VERIFY-004 Temporal Tests Use Virtual Time
VERIFY-004 No Wall-Clock Semantic Assertion
VERIFY-004 Long Stroke Observes Public/Durable Semantics
```

---

# 19. G4R 静态 Ratchets

至少增加以下机器门：

```text
E2E_ENTRY_COUNT == 1

OPENCODE_SPAWN_SITE_IN_E2E == 1

NO_E2E_WORKER_POOL

NO_E2E_SHUFFLE

NO_E2E_REPEAT

NO_TEMPORAL_CHILD_PROCESS

NO_TEMPORAL_NETWORK

NO_TEMPORAL_REAL_TIMER

NO_TEST_SLEEP

NO_PRODUCTION_RAW_CLOCK
except declared physical time adapter

NO_PRODUCTION_RAW_SETTIMEOUT
except declared timer adapter

NO_PER_CASE_CANARY_TIMEOUT

NO_SECOND_BUSINESS_STATE_MACHINE
```

以及 architecture gate：

> Domain/Kernel 不得因为测试需要新增程序计数器字段。

---

# 20. 迁移策略

不允许一边修 31 个旧 E2E，一边慢慢造新体系。

那会继续往危墙下面搬砖。

实施顺序：

### G4R-0 — Freeze

立即冻结：

```text
禁止新增 E2E
禁止新增 timeout
禁止 retry
禁止降低 parallelism 作为修复
禁止继续精修旧 scenario choreography
```

当前 `manager-unhappy-path` 不再以“把这个 TOML 调到绿”为目标。

它只是迁移素材。

---

### G4R-1 — Temporal Kernel

建立：

```text
VirtualClock
DeterministicCompletionSource
DeterministicEventQueue
InMemory durable port
Recorded provider port
Crash/drop-ephemeral operation
Trace runner
```

全部调用 production workflow。

---

### G4R-2 — Race Extraction

优先迁移当前最痛的：

```text
manager-unhappy
manager-full-loop
fallback-aabb
join guard
context recovery
orchestrator conflict/restart
finality cohort
```

每一个旧 scenario 解构为若干明确 theorem。

例如：

```text
owner failure 与 blogger interruption 任意排列
→ 同一 logical failure 最多记录一次
```

这正针对历史上已经真实出现的“同一次 owner failure 因跨 Session identity 被双记，append 顺序决定 trajectory”的 bug。

---

### G4R-3 — Build The Long Stroke

不是把旧 31 个 scenario 串起来。

重新写一条**自然故事**。

原则：

```text
少控制
多观察

少内部 expectation
多 durable invariant

少人为 choreography
多真实 adversity
```

每一个困难都应该是外部世界合理可能施加的困难。

---

### G4R-4 — Delete Old Canary World

Long Stroke 绿后：

```text
删除旧 case runner
删除 scenario pool
删除 startup stagger
删除 per-case watchdog
删除 shuffle/repeat infrastructure
删除不再需要的大型 TOML choreography
```

不是 deprecated。

是删除。

---

### G4R-5 — Time Boundary

扫清 semantic layers 中的：

```text
Date.now
UtcNow
setTimeout
sleep
real delay
```

全部下沉。

---

### G4R-6 — 10s Gate

最后才建立：

```text
semantic full test wall < 10s
Long Stroke wall < 6s
```

它们是 regression gate。

性能退化就是 RED。

---

# 21. G4R Exit Criteria

G4 不允许在下面全部满足之前 Exit。

## Correctness

```text
[ ] 所有旧 semantic E2E 已迁成 pure/temporal proof
[ ] race permutation deterministic
[ ] recovery deterministic
[ ] virtual time owns semantic deadlines
[ ] exactly-once laws explicit
[ ] ownership laws explicit
[ ] no scheduler-dependent proof
```

## Physical

```text
[ ] exactly one E2E
[ ] exactly one OpenCode spawn
[ ] exactly one continuous OpenCode lifetime
[ ] Long Stroke contains multiple adversity classes
[ ] Long Stroke reaches final clean shutdown
```

## Adversity

Long Stroke 至少真实经历：

```text
[ ] provider transient failure
[ ] fallback
[ ] join blocked then causally awakened
[ ] reviewer REVISE
[ ] interrupted/aborted child or session
[ ] finality temporarily blocked
[ ] durable recovery/continuation
[ ] publish conflict / stale target
[ ] successful reconciliation
[ ] later successful finality
```

它必须证明：

> **不是因为一路顺风才成功。**

---

## Architecture

```text
[ ] no test-only business implementation
[ ] no domain program-counter additions
[ ] no semantic raw wall clock
[ ] no semantic raw timers
[ ] Signal wakes; Fact decides
[ ] one fact / one owner / one writer
```

---

## Performance

```text
[ ] npm semantic full < 10s
[ ] Long Stroke target < 6s
[ ] no timeout inflation
[ ] no retry-until-pass
[ ] no repeat-for-confidence
```

---

## Proof

```text
[ ] VERIFY-001 revised
[ ] VERIFY-002 revised
[ ] VERIFY-004 revised
[ ] obsolete parallel-canary static gates deleted
[ ] new one-world ratchets machine enforced
[ ] npm run check green
```

---

# 22. Release Philosophy

过去 release confidence 是：

```text
跑很多真实世界
× 跑很多轮
× shuffle
× concurrency
× watchdog
= 大概没有 race
```

以后应该是：

```text
Pure laws
+
explicit temporal algebra
+
deterministic adversarial traces
+
physical boundary contracts
+
one tortured real world
=
confidence
```

注意最后不是：

```text
one happy real world
```

而是：

```text
one tortured real world
```

它应该受伤。

它应该返工。

它应该被拒绝。

它应该等待。

它应该被用户打断。

它应该遇到 provider failure。

它应该留下 durable history。

它应该遇到 stale publish。

它应该恢复。

但它不能靠第二次投胎获得成功。

---

# 23. 最终定义

**Pure test 问：**

> 规则本身是真的吗？

**Temporal test 问：**

> 无论合法事件以什么顺序来到，规则仍然是真的吗？

**Adapter test 问：**

> 这个物理边界有没有说谎？

**唯一 E2E 问：**

> 如果真的把所有东西接起来，给这个世界一段并不顺遂的人生，它能不能只出生一次，经历坎坷，留下正确历史，最后有尊严地死去？

这就是唯一需要 OpenCode 回答的问题。

---

# 24. 最后一条原则

> **不要用更多世界证明一个世界是正确的。**

应该把一个世界内部所有可推理的规律抽出来，变成纯逻辑。

然后只留下一个真实世界。

让它长寿。

让它坎坷。

让它受尽折腾。

让它从不靠运气。

最后让它自己收敛。

这才叫：

# 明显没有 Bug。
