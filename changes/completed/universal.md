# Proposal：Dedicated Inspector Learning Collapse — 删除 Student/Teacher，以 Meditator + Dedicated Inspector + Exit-time Casebook Synthesis 统一学习与知识复用

**Status:** Proposed（精修：引入 ReuseScope / SessionOwnership；non-reusable vs reusable；CaseFinalize）
**Priority:** P0 / architecture + capability simplification + context reuse + persistent knowledge reuse
**Scope:** Agent roles / Meditator / Inspector / synchronous delegation / ReuseScope / dedicated Session ownership / Casebook lifecycle / Student–Teacher removal / Prefix Cache / persistence integration
**Compatibility:** Clean break for Student / Teacher public and internal role semantics；Dedicated/reusable Inspector 为 baseline；Casebook 继续 repository opt-in 静默；本 Change 同时重构 Session ownership（Work/InternalLeaf × Root/Attached）
**Related proposals:** `changes/proposed/perm-inspector.md`、`changes/proposed/storage.md`、`changes/proposed/js-capability-projected-tools.md`

---

# 0. Executive Decision

本 Change 做四个不可分割的裁决，并引入 `ReuseScope` 与 Session ownership 正式模型（见 §0.5 / §11 / §13.5）。

## 0.1 删除 Student 与 Teacher

彻底删除：

```text
Role.Student
Role.Teacher

fast-student
deep-student
fast-teacher
deep-teacher

StudentLearn
StudentCompile
Teacher request kind

teacher(...)
teacher return
Student final return

Student QA lifecycle
Student idle → compile
SKILL compilation
StudentTeacherRuntime 的业务程序
Teacher Satellite 特例
```

不保留 alias。

不保留 deprecated mode。

不保留“暂时隐藏但以后可能恢复”的兼容实现。

---

## 0.2 Student 的产品职责并入 Meditator

今后只有：

```text
Meditator
```

负责：

```text
理解问题
形成假设
反驳自己的假设
组织证据需求
综合 Inspector 的调查
形成最终结论
```

Meditator 不直接读取 repository。

目标工具面：

```text
Meditator
→ { inspector }
```

删除：

```text
read
glob
grep
js-meditator filesystem surface
```

因此原来的：

```text
Student ≈ Meditator
Teacher ≈ Inspector
```

不再只是概念类比。

它成为真实能力结构。

---

## 0.3 Inspector 调用改成 caller-owned reusable dedicated Session

对所有同步 Inspector caller：

```text
Meditator → Inspector
Coder     → Inspector
DevOps    → Inspector
```

不再每次调用创建独立 Inspector Session。

而是：

```text
一个 OwnerReuseScope
+
Inspector role
=
一个 dedicated Inspector Session
```

同一 ReuseScope 内：

```text
inspector(Q1)
→ same Inspector I

inspector(Q2)
→ same Inspector I

inspector(Q3)
→ same Inspector I
```

Session、transcript、PrefixEpoch、调查历史持续复用。

> **Dedicated 的 owner 不是“物理 Session 永远”，而是“可证明语义兼容的 ReuseScope”。**

---

## 0.4 Reusable Inspector 不再每次调用归档 Case

这是对 `perm-inspector.md` 最重要的修改。

现 Proposal 当前语义是：

```text
一次 Inspector invocation
→ Question
→ repository investigation
→ Answer
→ Inspector completion
→ archive Q/A/evidence Case
```

并规定新 Case 的 `Q.md` 是单次 Inspector invocation 的完整 initial prompt，`A.md` 是实际返回 caller 的 bounded ToolResult。

Reusable dedicated Inspector 落地后，该模型不再成立。

新的裁决：

```text
OwnerReuseScope alive / compatible
→ dedicated Inspector 可被同步调用很多次
→ 每次只积累 ephemeral Case Draft
→ 不 publication

ReuseScope 被证明关闭（graceful）
→ freeze Inspector draft
→ synthesize once
→ publication 一个 canonical Case
→ retire/release dedicated Inspector
```

也就是说：

> **Hot knowledge 留在 dedicated Inspector transcript；cold reusable knowledge 只在 ReuseScope 关闭时编译一次进入 Casebook。**

这正好取代过去：

```text
Student 学习全过程
→ 最后 compile SKILL
```

的产品位置。

但新产物不再是文件系统中的 SKILL。

而是：

```text
Inspector Casebook Case
```

同时正式裁决产品边界：

```text
Dedicated / reusable Inspector Session = baseline capability

Persistent Casebook = repository opt-in
（`.wanxiang/casebook/` 不存在时 feature 整体静默）
```

因此“替代 SKILL”应表述为：

> **在启用 persistent Inspector knowledge 的 repository 中，Casebook 取代过去 Student SKILL 的跨任务知识复用职责。**

不是声称所有场景完全等价。


# 0.5 旧→新映射（先看这张表）

| 删除的旧概念 | 新归属 |
| --- | --- |
| Student reasoning | Meditator（保留 epistemic style，删除 workflow protocol） |
| Teacher evidence | Inspector |
| Teacher persistent session | reusable dedicated Inspector（Work Session，非 leaf Satellite） |
| Teacher CE algebra | generic sync delegate CE（Returned → Completion） |
| Teacher leaf / no-Companion topology | **不保留** |
| QA hot knowledge | owner + Inspector transcripts |
| Compile / SKILL | 删除；cold knowledge → Casebook（opt-in） |
| one-shot vs dedicated 二分措辞 | non-reusable vs reusable Inspector knowledge lifetime |
| `(owner SessionId, role)` | `(OwnerReuseScopeId, role)` |
| owner Session retire → synthesis | graceful ReuseScope close → synthesis once；unexpected `SessionDeleted` / crash → cleanup only |

---

# 1. 为什么这是一个统一架构，而不是三个顺手重构

当前正式 Agent 能力矩阵同时存在：

```text
Coder      → read/write/edit/... + inspector
DevOps     → read/... + inspector + coder
Meditator  → read/glob/grep + inspector
Student    → request-kind-specific
Teacher    → internal execution agent
```

其中 Meditator 当前仍直接拥有 `read/glob/grep`，Student/Teacher 则有独立协议。

Student 当前又被拆成：

```text
StudentLearn
→ { teacher }

StudentCompile
→ { read, glob, grep, write, edit, return }
```

Teacher 是私有叶子 Agent。

另一方面，Teacher 已经证明了一种正确的同步 Session 模型：

```text
同一个 Teacher Session
多轮自然语言调用
不清空历史
不重新注入完整旧 context
```

且 Teacher tool CE 已经 collapse 为：

```text
sendTeacherPrompt
→ await Returned
→ await Completion
```

因此真正应该保留的不是 Teacher 这个角色。

应该保留的是它已经证明正确的运行时结构：

> **一个 caller 长期绑定一个 synchronous specialist Session。**

本 Change 把这个结构提升为通用能力。

---

# 2. 最终只保留两种 Agent-to-Agent 调用

全系统最终只能有两个生命周期模型。

## A. Structured synchronous delegation

```text
caller
→ dedicated specialist
→ caller await
→ specialist return
→ specialist turn terminal
→ caller continues
```

特征：

```text
同步
串行
无 HandleId
无 join
无 list
callee Session 长期复用
OwnerReuseScope lifetime ownership
```

---

## B. Managed asynchronous work

例如：

```text
Manager → fork-agent
Orchestrator → fork-manager
```

特征：

```text
异步
允许大量并发
独立 lifetime
HandleId
join/list
background completion
```

---

不得出现第三套。

尤其禁止把 Dedicated Inspector 做成：

```text
fork
→ handle
→ hidden join
→ 包装成同步 tool
```

那只是把两种模型重新搅在一起。

---

# 3. 本 Change 明确不动 Manager 异步并发语义

Manager：

```text
fork-agent
join
list
```

保持异步。

Orchestrator：

```text
fork-manager
join
```

保持异步。

Manager 可以继续：

```text
并发 fork 多个 Coder
并发 fork 多个 Inspector
并发 fork 多个 DevOps
```

其中某一个 Coder 自己调用 `inspector(...)` 时，才进入本 Change 的 synchronous dedicated 模型。

所以：

```text
Manager concurrency
```

与：

```text
per-caller-ReuseScope synchronous delegate serialization
```

不存在冲突。

---

# 4. Agent Catalog：mandatory baseline 24 → 20

当前正式 Agent 有 Student / Teacher fast/deep pair，因此是 24 个。

目标 Canonical Role：

```fsharp
type Role =
    | Orchestrator
    | Manager
    | Coder
    | Inspector
    | DevOps
    | Browser
    | Meditator
    | Reviewer
    | Blogger
    | Executor
```

因此 **mandatory baseline Agent 数**改为：

```text
20
```

即：

```text
fast-orchestrator    deep-orchestrator
fast-manager         deep-manager
fast-coder           deep-coder
fast-inspector       deep-inspector
fast-devops          deep-devops
fast-browser         deep-browser
fast-meditator       deep-meditator
fast-reviewer        deep-reviewer
fast-blogger         deep-blogger
fast-executor        deep-executor
```

彻底删除：

```text
fast-student
deep-student
fast-teacher
deep-teacher
```

启动配置发现上述旧 Agent：

```text
fail closed
```

不做 alias。

`perm-inspector` 已定义的 conditional Bookkeeper pair 保持不变：

```text
Mandatory baseline agents = 20

Casebook enabled:
  + conditional fast-bookkeeper / deep-bookkeeper pair
```

**不要写“系统 Agent 总数固定 20”。**

Casebook disabled 时不要求存在 Bookkeeper；enabled 时两者都必须配置。

---

# 5. Meditator 最终能力：纯 Reasoner + Student epistemic style

目标工具面：

```text
Meditator
→ inspector
```

不得再有：

```text
read
glob
grep
write
edit
executor
coder
fork
join
list
PTY
network
```

Meditator 的职责只有：

```text
reason
question
compare
challenge
synthesize
```

事实调查必须：

```text
Meditator
→ Inspector
```

删除 Student 后，**不得只改工具矩阵**。最终稿必须正式合并 Student 的认知姿态到 Meditator prompt：

```text
先形成当前理解
主动寻找反例
把事实问题委派 Inspector
针对 Inspector 回答继续追问
区分证据 / 推论 / 不确定性
在理解收敛前避免草率终止
```

这些只是 prompt discipline。

不得重新出现：

```text
LearningState
Compile
QA
return
```

也就是说：

> **保留 Student 的 epistemic style，删除 Student 的 workflow protocol。**

---

# 6. 为什么必须删除 Meditator 的 read/glob/grep

如果保留：

```text
Meditator:
  read
  glob
  grep
  inspector
```

那么产品仍然没有回答：

> 什么情况下 Meditator 自己 read，什么情况下应该叫 Inspector？

结果一定退化成：

```text
便宜的证据自己看
复杂的再叫 Inspector
```

这会造成：

```text
重复 repository scan
Inspector context 无法积累
Casebook knowledge 被绕过
角色边界靠 prompt 自觉
```

正确分层是：

```text
Meditator = reasoning
Inspector = evidence acquisition
```

不是：

```text
Meditator = 小 Inspector + reasoning
```

---

# 7. Student 删除后，不保留任何 Learning Mode 状态机

不得替换成：

```text
MeditatorLearn
MeditatorCompile
MeditatorLearning
MeditatorFinalizing
LearningPhase
```

也不得重新创建：

```text
RequestKind.MeditatorLearn
RequestKind.MeditatorCompile
```

Meditator 就是普通 Work Session。

用户选择：

```text
fast-meditator
```

之后正常对话。

模型自然地：

```text
分析
→ inspector
→ 分析
→ inspector
→ 综合
→ 普通 Assistant terminal
```

没有：

```text
idle → compile
```

没有：

```text
QA exists?
```

没有：

```text
final return
```

没有：

```text
compile stage
```

这不是把 Student state machine 改名。

是删除它。

---

# 8. 删除 QA.md

当前 Student 体系把 QA 作为学习期间的 durable knowledge truth，并用它承载 User/Student/Teacher 知识流水。Student 的正式程序目前包括 QA、Learn、Teacher、Compile、最终 cleanup。

本 Change 完成后：

```text
StudentQaStore
QA.md
Student QA Event
Student QA projection
Student QA cleanup
```

全部删除。

原因：

新的 hot knowledge truth 已经存在：

```text
Meditator transcript
+
Dedicated Inspector transcript
```

而需要跨 Session 保存的 reusable knowledge：

```text
Casebook
```

不再需要中间再造一个 Student-only knowledge store。

---

# 9. 删除 SKILL compilation

彻底删除 Student 专用：

```text
.agent/skills/<name>/SKILL.md
```

生成协议。

也删除：

```text
StudentCompile write/edit policy
final SKILL validation
至少触达一个 SKILL
final return 前重读 SKILL
Student artifact cleanup
```

本 Change 不把 SKILL 搬成：

```text
MeditatorSkill
InspectorSkill
CasebookSkill
```

在 Casebook 启用的 repository 中，永久学习产物唯一变为：

```text
Inspector Casebook Case
```

Casebook 未启用时，没有 cold persistent learning artifact；只有 ReuseScope 内的 hot transcript reuse。

---

# 10. 新的知识分层

完成后只有三层。

## 10.1 当前 owner reasoning history

例如：

```text
Meditator transcript
Coder transcript
DevOps transcript
```

负责：

```text
caller 自己想过什么
caller 为什么提出下一问题
caller 如何使用 Inspector answer
```

---

## 10.2 Dedicated Inspector hot memory

负责：

```text
该 ReuseScope 已经问过什么
Inspector 已调查过什么
哪些路径读过
之前的解释是什么
新问题和旧调查怎样关联
```

在 ReuseScope 活着时：

> **这是最高效的知识复用层。**

无需 Casebook roundtrip。

无需重新 fetch 自己刚刚知道的知识。

---

## 10.3 Casebook cold memory

ReuseScope 最终优雅关闭后：

```text
整个 reusable Inspector lifetime
→ synthesis once
→ one reusable Case
```

以后其它 Inspector：

```text
看 index
→ fetch(case)
→ freshness replay
→ 复用
```

这才是跨 Session / 跨 owner 的知识层。

Casebook 仍是 repository opt-in；未启用时只有 10.1 + 10.2。

---

# 11. ReuseScope：Dedicated 绑定的真正生命周期

本 Change 最重要的概念升级：

```text
ReuseScope
=
这段工作在语义上仍允许复用同一上下文的最大生命周期
```

典型映射：

```text
普通 HumanRoot 工作
→ Logical Run / 可继续多轮的同一语义工作上下文

Manager fork 后反复 fork(existing agent_id, compatible requirement)
→ 同一个 reusable child scope

Meditator / Coder / DevOps 的一次可复用工作上下文
→ 对应 reusable agent scope
```

因此 dedicated key 不是：

```text
(owner SessionId, Inspector)
```

而是：

```text
(OwnerReuseScopeId, SyncDelegateRole)
```

Host 明确区分：

```text
普通 completion
Session 生命周期继续
SessionDeleted
```

Teacher 成功 completion 后 Session 不 retire；下一问题继续同一 Session。Manager child join 退休后仍可用同一个 agent id reopen，同一 Session/context 继续使用。所以：

> **不能把 owner Session 进入最终 retire/dispose 直接等同 ReuseScope 终结。**

只有 **ReuseScope 被证明关闭** 时，才：

```text
freeze Inspector draft
→ synthesize once
→ publish
→ retire/release dedicated Inspector
```

这样既能及时得到 Case，也不会等到用户物理删除 Chat Session 才保存知识。

---

## 11.1 Dedicated Inspector identity

定义：

```text
DedicatedInspector(OwnerReuseScopeId)
```

唯一物理绑定：

```text
(OwnerReuseScopeId, Inspector)
→ at most one live Inspector Session
```

例如：

```text
Meditator M1 scope
→ Inspector I1

Coder C1 scope
→ Inspector I2

DevOps D1 scope
→ Inspector I3
```

如果 Coder 本身是 DevOps 的 dedicated Coder：

```text
DevOps D1
  → Coder C1
      → Inspector I2
```

逻辑 ownership / ReuseScope 可以嵌套。

Host 物理 parent 仍遵守现有 family-root flattening。

---

## 11.2 Reuse compatibility 合同

Case identity 继续是 Inspector SessionId（见后文），不新造 KnowledgeId。

因此必须有强合同：

```text
Reuse same Inspector session
iff
new work is compatible with accumulated semantic context.
```

不兼容：

```text
close old reuse scope
→ synthesize old Case
→ new Inspector session
```

如果一个 Inspector Session 被用于十个完全不相关任务：

```text
数据库 → CSS → OAuth → F#
```

最后硬合成一个 Q/A Case 就会废掉。

这恰好与现有 Manager “compatible context 才 reuse”的原则一致：不兼容则应新开。

---

# 12. 同 caller ReuseScope 所有同步 delegate 必须串行

全局 invariant：

```text
一个 immediate caller ReuseScope
同一时刻
最多一个 active synchronous delegate call
```

serialization key：

```text
immediate caller ReuseScope
```

**不是**：

```text
family root
repository
worktree
```

也不是：

```text
每个 delegate 各自 single-flight
```

而是：

```text
caller-scope-level single-flight
```

因此嵌套同步调用合法且不会死锁：

```text
DevOps D waits Coder C
Coder C independently waits Inspector I
```

若错误按 family root 串行：

```text
D 持 family gate 等 C
C 想拿同一个 family gate 调 I
→ deadlock
```

例如同一 DevOps scope：

合法：

```text
coder(...)
await
inspector(...)
await
coder(...)
```

禁止：

```text
Promise.all(
  coder(...),
  inspector(...)
)
```

原因：

1. caller transcript 自然表达 happens-before；
2. callee Session 不会收到重叠 prompt；
3. return routing 单值化；
4. cancellation 简单；
5. dedicated prefix 最大化复用；
6. 真正独立并发工作本来就属于 Manager。

---

# 13. Sync Delegate 图必须无环

第一版建议允许：

```text
Meditator → Inspector

Coder → Inspector

DevOps → Coder
DevOps → Inspector
```

禁止：

```text
Inspector → Coder
Inspector → Meditator
Coder → DevOps
```

因此图是 DAG。

启动时应静态/配置验证：

```text
sync delegate graph acyclic
```

避免：

```text
Coder waits Inspector
Inspector waits Coder
```

形成同步死锁。

---

## 13.5 Session ownership 模型重构：Work vs InternalLeaf，Root vs Attached

当前正式 HOST-008 明确：

```text
Satellite
→ 只属于 WorkSession
→ Satellite 不递归
```

但新结构天然会出现：

```text
DevOps
  → dedicated Coder
      → dedicated Inspector
          → fetch()
              → ephemeral Bookkeeper
```

而 `perm-inspector` 自己又已经提出：

```text
Inspector WorkSession
  └─ Bookkeeper Satellite
```

因此本 Change 必须升级为 **session ownership 模型重构**，不能继续往 `SatelliteKind` 里塞 case。

分离两个正交维度：

```fsharp
type SessionExecutionClass =
    | Work
    | InternalLeaf

type SessionOwnership =
    | Root
    | Attached of
        ownerSessionId: SessionId *
        attachment: AttachmentKind

type AttachmentKind =
    | Companion
    | SyncInspector
    | SyncCoder
    | Bookkeeper of transactionId
```

于是：

```text
Dedicated Inspector
= Work execution class
+ Attached ownership
```

它仍可以获得正常 WorkSession 的 context / Companion 能力。

而：

```text
Bookkeeper
= InternalLeaf
+ Attached ownership
```

仍然是 ephemeral leaf。

这比“所有 attached child 都叫 Satellite”干净得多。

---

## 13.6 复用 Teacher 的调用代数，不复用 Teacher 的 Session 分类

Teacher 当前是特殊叶子、无 Companion。

Dedicated Inspector 却是**故意长期存在**的 hot knowledge session。如果把它也做成无 Companion 的 Satellite，长上下文迟早会撞 context 问题。

现架构规定 Work Session 有自己的 Companion，而 Satellite 是叶子。

因此最终稿明确：

```text
Teacher 被删除。

保留的是：
  synchronous CE protocol
  （send prompt → await Returned → await Completion）

不保留的是：
  Teacher 的 leaf / no-Companion topology
```

也就是说：

> **复用 Teacher 的调用代数，不复用 Teacher 的 Session 分类。**

Dedicated Inspector 最好继续是 Work Session，不应该继承 Teacher 的“无 Companion 叶子”性质。

---

## 13.7 Sync delegate 的 tier 绑定必须钉死

Dedicated Session 要吃 prefix/context reuse，就不能每次：

```text
inspector(fast)
inspector(deep)
```

乱切。

第一版裁决：

```text
owner effective tier
→ deterministic delegate tier
```

具体：

```text
fast owner → fast Inspector / fast Coder
deep owner → deep Inspector / deep Coder
```

模型不可每轮选择 target Agent。

否则“一个 dedicated Inspector”这个不变量本身就不成立。

---

# 14. Dedicated Inspector 的 `return` 是 execution profile，不是新业务阶段

普通 Inspector Work Session：

```text
read
glob
grep
executor
fetch?    // Casebook enabled 时
```

作为 synchronous dedicated callee 时：

```text
Role = Inspector
InvocationMode = SynchronousDelegate

base capabilities
+
return
```

工具面：

```text
read
glob
grep
executor
fetch?
return
```

`return(message)`：

```text
只完成当前同步 Inspector invocation
```

普通 assistant 正文 / idle / reasoning：

```text
不得成为 tool result
```

删除：

```text
StudentLearn
StudentCompile
```

以后不要再造：

```text
InspectorNormal
InspectorDelegatePhase
```

这是正交 capability / execution profile，不是 lifecycle PC。这点应写进 DSL proof。

这直接推广当前 Teacher 已经验证过的：

```text
Returned
→ Completion
```

协议——但不再继承 Teacher Satellite 特例。

---

# 15. 通用调用结构

建议：

```fsharp
task {
    use! scopeLease =
        syncDelegateGate.Acquire(ownerReuseScope)

    let! inspector =
        attachedSessions.GetOrCreateInspector(ownerReuseScope)

    use call =
        inspectorCalls.Begin(ownerReuseScope, inspector)

    do!
        promptDispatcher.Send(
            inspector,
            message)

    let! answer =
        call.Returned.Await(...)

    do!
        call.Completion.Await(...)

    return answer
}
```

业务 caller 只知道：

```text
inspector(message): string
```

看不到：

```text
ReuseScope create/close
Session create
Session recovery
TCS
Returned
Completion
idle nudge
Host reconcile
tier selection
```

delegate Agent 由 owner effective tier 确定性绑定，模型不可每轮选择。

---

# 16. 必须继续等待 completion，而不是 return 后立即放 caller

顺序：

```text
Inspector calls return(A)
→ Returned resolved

Inspector same Host loop continues
→ fixed terminal assistant completion

reconciler proves TurnCompleted
→ Completion resolved

caller gets tool result A
```

这样才能保证下一次：

```text
inspector(Q2)
```

不会和 Q1 的 terminal 尾部重叠。

当前 Teacher collapse 已经证明该双 await 结构可以不用 lifecycle stage bit 表达。

---

# 17. Dedicated Inspector Session lifetime = OwnerReuseScope lifetime

不得：

```text
每次 inspector() 新建 Session
```

不得：

```text
return 后 retire Inspector
```

不得：

```text
调用次数达到 N 自动 rotate
```

正常路径：

```text
ReuseScope alive / compatible
→ Inspector alive

owner asks Q1
→ I

owner asks Q2
→ same I

owner asks Q3
→ same I

ReuseScope graceful close
→ finalize Case once
→ retire/release I
```

不兼容新工作时：

```text
close old ReuseScope
→ synthesize old Case
→ open new Inspector under new ReuseScope
```

---

# 18. Prefix Cache 是结构收益，不另造 cache

当前 PrefixEpoch 已规定，同一 epoch 后续 provider request 必须保持之前 wire 的稳定字节前缀；普通回合只追加 suffix。

Dedicated Inspector 因此自然变成：

```text
system
existing history
Q1
tools
A1
completion
Q2
tools
A2
completion
Q3
...
```

而不是：

```text
new Inspector
system
reconstruct context
Q2
```

所以本 Change 禁止新增：

```text
InspectorContextCache
InspectorPromptCache
InspectorHistorySummaryCache
```

Session transcript 自己就是 hot context。

---

# 19. `perm-inspector` 必须引入两种 Inspector knowledge lifetime

当前 Proposal 默认：

```text
Inspector completion
→ Case creation
```

修改为——**不要再叫 “one-shot vs dedicated”**，而叫：

```text
Non-reusable Inspector scope
vs
Reusable Inspector scope
```

## 19.1 Non-reusable Inspector scope

例如异步独立 Inspector Work Session，terminal 后不再被兼容 reopen：

```text
terminal 后可直接 archive
```

继续保留旧行为：

```text
Inspector terminal
→ initial Case archive
```

## 19.2 Reusable Inspector scope

包括但不限于：

```text
Meditator / Coder / DevOps 的 dedicated Inspector
Manager fork 后可通过 existing agent_id 继续 reopen 的 Inspector
```

新行为：

```text
每个 assignment / return
→ capture only
→ 不 publication

每个 invocation completion
→ 不 publication

Inspector idle
→ 不 publication

owner 普通一个 turn 完成
→ 不 publication

owner 继续下一用户 turn（同一 ReuseScope）
→ 不 publication

只有 ReuseScope 最终被证明关闭
→ final synthesis exactly once
→ publication once
```

于是 Meditator dedicated Inspector 只是 reusable Inspector 的一个生产者，不是 Casebook 特例。

这是本 Proposal 最重要的 Casebook 改动。

---

# 20. 为什么不能每一轮都 archive

假设 Meditator 连续问：

```text
Q1: 谁拥有 PromptAuthority？
Q2: 那 recovery 为什么不能重发？
Q3: Reviewer seal 与这个约束是什么关系？
Q4: 请找反例确认。
```

如果每轮 archive：

```text
Case I/Q1
Case I/Q2
Case I/Q3
Case I/Q4
```

会出现：

```text
重复 evidence
上下文割裂
四个高度相关 Q
LRU 噪声
后续 Inspector 不知道该 fetch 哪一个
每轮 Casebook mutation
每轮潜在 Bookkeeper/remote 成本
```

而 dedicated Session 自己已经拥有所有上下文。

因此 owner 活着时 archive 是负收益。

---

# 21. 新增 Ephemeral `InspectorCaseDraft`

Reusable Inspector 活着期间，需要一个**非权威、进程内、可丢失**的 capture resource。

示意：

```fsharp
type InspectorCallCapture =
    { Ordinal: int64
      Question: string
      Answer: string
      Observations: CapturedObservation list }

type InspectorCaseDraft =
    { OwnerReuseScope: ReuseScopeId
      InspectorSession: SessionId
      OwnerOpening: string option
      Calls: ResizeArray<InspectorCallCapture>
      ObservationAccumulator: ... }
```

它必须明确：

```text
process-local
ephemeral
best-effort
safe to lose
not recovery truth
not workflow stage
not PromptAuthority
not Journal truth
```

合法 mutable 注释：

```text
// DSL-MUTABLE: resource — ephemeral Inspector Casebook capture draft.
```

---

# 22. Case Draft 不得决定业务流程

绝对禁止：

```fsharp
if draft.Calls.Count > 3 then ...
```

用于：

```text
决定 caller 下一步
决定是否退出
决定是否 finalize
决定 Meditator 是否“学够了”
```

Draft 只有：

```text
capture
final synthesis input
```

程序永远不能读它来选择业务 branch。

---

# 23. Observation 仍必须在真实 tool execution 时捕获

现 `perm-inspector` 明确要求：

> observation 必须在 Inspector 运行期间从真实 tool execution 捕获，不能 Session 结束后从自然语言猜“它看过什么”。

该原则不变。

每次：

```text
read
glob
grep
recognized executor
fetch
```

发生时增量 capture。

不得等 owner 退出后：

```text
扫描 transcript
→ 猜 Inspector 大概读过哪些文件
```

---

# 24. fetch evidence 继续 flatten

现 Proposal 已要求：

```text
Inspector B fetch(A)
+
B direct evidence
→ B Case observations 包含 A captured evidence + B direct evidence
```

使 B 独立于 A 的存活。

Dedicated Inspector 同样遵守。

如果生命周期中：

```text
I fetch Case A
I read file2
I grep file3
```

ReuseScope-close synthesis 后的新 Case：

```text
observations =
flatten(A evidence)
+
direct evidence
```

不得建立：

```text
Case B depends-on Case A runtime graph
```

---

# 25. ReuseScope close 时到底 synthesize 什么

Finalization 的 evidence base：

```text
1. Owner opening / assignment（若有）
2. 按调用顺序排列的全部 Inspector questions
3. 每个 Inspector 实际返回 caller 的 bounded Answer
4. 全生命周期 flatten 后的 captured observations
5. 对应 evidence snapshot
```

可选低信任 hint：

```text
6. Owner terminal output（optional low-trust organizational hint）
```

注意：

```text
Owner hidden reasoning
```

不要求抓取。

知识传递仍只使用真实 transcript/tool boundary 已存在的内容。

正式证据边界：

```text
Inspector Q/A + observations
= synthesis evidence base

owner terminal
= optional low-trust synthesis hint
```

> **任何进入 canonical A 的 repository factual claim，不得仅因 owner terminal 出现就被视为 evidence-backed。**

---

# 26. Owner terminal 为何只是低信任 hint

原 Student/SKILL 的价值不只是 Teacher 的局部回答，还有 Student 最后的综合。

删除 Student 后，该综合现在发生在：

```text
Meditator final answer
```

Meditator 最终回答可能包含：

```text
推论
取舍
用户偏好
未经 Inspector 证明的组织性判断
```

因此 exit-time synthesis **可以**把 owner terminal 作为低信任 organizational hint，帮助 Bookkeeper 理解“主人最终认为哪些知识重要”。

但它不是 evidence。

如果只看 Inspector Q/A：

```text
可能漏掉 owner 对这些证据的最终组合关系
```

但若把 owner terminal 提升为必需 evidence：

```text
会破坏 Inspector Casebook 的证据边界
```

所以正确姿态是：

```text
保留 Student 最终综合的味道
不破坏 Inspector Casebook 的 evidence-backed 边界
```

---

# 27. Final Case 不等于对话 dump

禁止简单：

```text
Q.md = 所有 questions concat
A.md = 所有 answers concat
```

那只是 transcript archive。

Exit-time 必须进行一次 semantic synthesis。

目标：

```text
多轮 Q/A
+
证据
+
最终综合
↓
一个 canonical reusable Q
+
一个 canonical reusable A
+
flattened observations
+
snapshot
```

---

# 28. 直接复用现有 Bookkeeper，但正式增加两种 request contract

不得新建：

```text
SkillCompiler
LearningCompiler
CaseSynthesizerAgent
StudentReplacement
TeacherReplacement
Learner
Compiler
Synthesizer Agent
```

直接复用 `perm-inspector` 已经定义的私有 Bookkeeper 机制。

现 Proposal 已规定 Bookkeeper 可以修改：

```text
Q
A
```

且 subject repository 对 Inspector 仍保持只读；Bookkeeper 的 Q/A 修改只发生在 staged Case documents 中。

但现有 Bookkeeper 是为：

```text
old Q/A
+
evidence delta
→ refresh current Q/A
```

设计的。

新场景是：

```text
many Q/A
+
full accumulated evidence
→ create one canonical Q/A
```

这不是同一个 prompt。

因此最终稿明确：

```text
same Bookkeeper Agent
same edit-qa tool
same security boundary

but two request contracts:
  CaseRefresh
  CaseFinalize
```

不要假装 refresh prompt 原封不动就能承担 synthesis。

---

# 29. Exit synthesis staging（CaseFinalize）

建议机械 seed：

```text
Staged Q
=
Owner opening
+
ordered Inspector questions

Staged A
=
ordered bounded Inspector answers
+
optional Owner terminal hint（low-trust, clearly labeled）
```

明确使用结构化低信任 data container。

然后给 Bookkeeper 固定 trusted instruction（CaseFinalize contract）：

```text
Convert this completed reusable Inspector scope into one reusable
Inspector Case.

Rewrite Q into the smallest faithful canonical inquiry that describes
the durable subject investigated.

Rewrite A into a self-contained reusable answer containing the
architecture, constraints, evidence-backed findings, important
counterexamples and operational consequences that remain useful
outside this original scope.

Treat owner terminal, if present, as an optional organizational hint
only. Do not treat it as evidence.

Remove conversational scaffolding, task coordination, repeated
questions, acknowledgements and temporary progress narration.

Do not invent evidence.
Do not claim freshness.
Do not claim correctness proof.
Do not modify the subject repository.
```

---

# 30. “Bookkeeper exactly once”= 一次 provider transaction

这是用户要求的核心约束：

> **可复用 Inspector 只在 ReuseScope 最后关闭时合并处理一次。**

所以 reusable Inspector finalization：

```text
at most one Bookkeeper provider transaction
```

现 Bookkeeper 合法多次调用 `edit-qa`。

因此最终稿不要写得让 Reviewer 误解成：

```text
只能 edit 一次
```

正确写法：

```text
Reusable Inspector finalization:
  at most one Bookkeeper provider transaction

inside that transaction:
  edit-qa may be called zero or more times
```

禁止：

```text
每次 Inspector return 都启动 Bookkeeper provider transaction
每次 owner turn 都 Bookkeeper
owner idle 时 Bookkeeper
连续启动第二次 Bookkeeper provider transaction 直到满意
```

publication CAS 可以纯函数式有限 retry，但：

```text
不得启动第二次 Bookkeeper provider transaction
```

---

# 31. “一次”不等于 publication CAS 不能 retry

需要区分：

```text
semantic synthesis provider transaction
```

与：

```text
storage CAS
```

Bookkeeper provider transaction：

```text
最多 1 次
```

而已经生成确定 candidate 后：

```text
pure CAS merge/retry
```

可以在有限预算内重试。

CAS retry 不重新请求模型，不重新解释知识，因此不违反“一次 synthesis”。

---

# 32. Synthesis 后必须再验证 evidence stability

现 `perm-inspector` refresh 已有正确顺序：

```text
freeze old Case
→ replay evidence
→ Bookkeeper
→ final staged Q/A
→ final current evidence verification
→ publication
```

Reusable initial synthesis（CaseFinalize）同样要求：

```text
freeze final draft
→ Bookkeeper once（one provider transaction）
→ replay/verify captured observations against current worktree
```

如果 synthesis 期间 subject evidence 已改变：

```text
discard candidate
do not publish
```

**不得第二次启动 Bookkeeper provider transaction。**

因为 Casebook 是 best-effort cache。

宁可本次没有 Case，也不要把一次 synthesis 偷偷变成循环运行时。

---

# 33. Reusable CaseFinalize 失败语义

以下任一失败：

```text
Bookkeeper provider failure
Bookkeeper invalid edit
output bound failure
evidence drift
Case validation failure
local publication failure
```

结果：

```text
owner completion remains success
Casebook unchanged
draft discarded at teardown
Inspector retired
```

不得：

```text
把 owner 已完成任务改成 failed
修改 owner final output
重新打开 owner Session
重试整个学习过程
```

Casebook 仍然只是 cache。

---

# 34. Casebook 继续坚持 availability over proof

当前 `perm-inspector` 明确把 Casebook 定义为：

```text
best-effort semantic cache
```

允许：

```text
capture incomplete
old A stale
Bookkeeper imperfect
no-delta 不证明正确
```

本 Change 不把它升级成：

```text
知识真理数据库
```

更不能因为它取代 SKILL，就错误推导：

```text
Casebook Case = authoritative learned truth
```

不是。

它只是更适合复用的 persistent knowledge cache。

---

# 35. Reusable Case 的 Q.md 语义必须修改

旧规则：

```text
Q.md
= Inspector invocation 的完整 initial prompt
```

这只适用于 non-reusable Inspector。

新规则：

## Non-reusable Inspector scope

```text
Q.md initial
= full invocation prompt
```

保持不变。

## Reusable Inspector scope

```text
Q.md
= ReuseScope-close Bookkeeper CaseFinalize 得到的 current canonical inquiry
```

它不是：

```text
第一轮问题
最后一轮问题
owner prompt 原文
所有问题机械 concat
```

---

# 36. Reusable Case 的 A.md 语义

旧规则要求：

```text
A.md
= caller 实际得到的 bounded answer
```

这是单轮 / non-reusable Inspector 合理语义。

Reusable Case 改成：

```text
A.md
= exit-time synthesized reusable answer
```

但仍必须：

```text
满足 ToolResultBound
```

原因：

最终：

```text
fetch(case)
```

还是必须能作为普通 Inspector ToolResult 返回。

所以不引入：

```text
A.full.md
A.long.md
hidden knowledge blob
```

---

# 37. 不增加 Case kind metadata

当前 `meta.toml` 故意只有：

```text
revision
wall_clock
last_access
```

并禁止 `status/owner/generation/phase` 等字段。

本 Change 保持这一点。

不要新增：

```text
kind = dedicated
owner_session = ...
finalized = true
source = meditator
```

reader 不需要知道 Case 是：

```text
non-reusable archived
```

还是：

```text
reusable synthesized
```

它们最终都是：

```text
Q
A
observations
snapshot
```

相同的 reusable Case。

---

# 38. Case identity 继续使用 Inspector SessionId

第一版不新造：

```text
KnowledgeId
LearningId
CaseGroupId
OwnerKnowledgeId
```

继续：

```text
Case key = Inspector SessionId
```

non-reusable：

```text
I1 → Case I1
```

reusable：

```text
I2 在同一 ReuseScope 被使用 12 次
→ ReuseScope graceful close
→ Case I2
```

因此现有：

```text
session_id -- full Q
fetch(session_id)
```

模型保持成立。现 Proposal 当前就是以 Inspector SessionId 确定 Case path 与 fetch identity。

这个设计很好，不建议再造 KnowledgeId。

但它依赖 §11.2 的 reuse compatibility 合同：不兼容工作必须关闭旧 scope 并新开 Inspector，否则一个 Session 混入无关主题会毁掉 Case 质量。

---

# 39. Dedicated Session Replacement

如果 dedicated Inspector 可证明永久丢失：

```text
same OwnerReuseScope
→ replacement Inspector I2
```

第一版不要求恢复已经丢失的 hot transcript。

规则：

```text
旧 session knowledge：
  能从 ephemeral draft 保留多少算多少

replacement：
  正常继续新 Session

owner finalization：
  使用当前仍可获得的所有 draft fragments
  最终 publication key = final active Inspector SessionId（同一 ReuseScope）
```

不得伪造：

```text
I2 == I1
```

也不得为了 Casebook 复制一套 durable in-flight transcript protocol。

Casebook 本来就是 best effort。

---

# 40. Crash 语义

## 40.1 Owner/Inspector 正常运行中 process crash

ephemeral Draft：

```text
允许丢失
```

已经 durable 的旧 Casebook：

```text
不受影响
```

恢复 dedicated Inspector Session：

```text
按 Attached Work Session recovery 规则
```

但 Case Draft：

```text
不作为 recovery truth
```

后续重新 capture 能捕获的 evidence。

---

## 40.2 Crash 发生在 ReuseScope close synthesis 之前

本次新 Case 可能不存在。

允许。

不得在重启后通过：

```text
扫描所有 closed Session
→ 猜哪些需要 compile
```

补做 hidden batch synthesis。

那会制造第二运行时。

---

## 40.3 Crash 发生在 publication CAS 后

Case 已原子可见：

```text
保留
```

Session cleanup 重入不得重新 synthesize。

如果当前 Case key 已存在：

```text
initial finalization = no-op / AlreadyPublished
```

后续修改只能通过正常：

```text
fetch → CaseRefresh
```

协议。

---

## 40.4 unexpected SessionDeleted

```text
cleanup only
no reconstruction synthesis
```

与 graceful ReuseScope close 分流。详见 §42。

---

# 41. 什么情况下算 ReuseScope“最后关闭”

不能使用：

```text
idle
```

因为 Work Session idle 后还可能继续接收用户消息。

也不能使用：

```text
一次 Assistant completion
```

因为同一个 Session / 同一个 ReuseScope 可以多轮。

也不能简单写成：

```text
owner Session 进入最终 retire/dispose
```

因为 Host 明确区分普通 completion 与 Session 生命周期；Teacher 成功 completion 后 Session 不 retire。更麻烦的是 Manager child join 退休后仍可用同一个 agent id reopen，同一 Session/context 继续使用。还有独立的 `SessionDeleted`。

正确 trigger 是：

```text
OwnerReuseScope 被证明关闭，并且以后不会再向该 scope 发送兼容的业务 prompt
```

只有这个边界之后，才能开始 reusable Case synthesis。

---

# 42. Graceful finalize 与外部 `SessionDeleted` 必须分开

## 42.1 正常退出顺序（graceful scope close）

推荐：

```text
Owner terminal output 已确定（若有）
→ 禁止新的 SyncDelegate call
→ freeze InspectorCaseDraft
→ Bookkeeper CaseFinalize once（one provider transaction）
→ evidence stability verify
→ best-effort local publication
→ best-effort remote sync / store replication
→ retire dedicated Inspector
→ dispose draft
→ owner physical teardown 完成
```

## 42.2 unexpected physical deletion / crash

如果收到的是：

```text
HostSignal.SessionDeleted
```

或 hard crash / ambiguous teardown：

```text
cleanup only
no reconstruction synthesis
```

说明物理 Session 可能已经没了，此时不应该假装还能完成 synthesis。

这也符合“不为了 cache 建第二套恢复 runtime”的方向。

```text
graceful scope close
→ best-effort synthesis

unexpected SessionDeleted / crash
→ cleanup only, no reconstruction synthesis
```

---

# 43. Finalization 不得改变 owner terminal bytes

Owner terminal 内容必须在 synthesis 前已经冻结。

Casebook：

```text
success
failure
timeout
CAS conflict
remote offline
```

全部不得改变：

```text
用户已经应该看到的 terminal output
```

这延续现 `perm-inspector` 的正确原则：

> archive publication 失败不能使原 Inspector 已经成功的 Answer 失败。

---

# 44. 非正常 ReuseScope exit

第一版建议：

```text
normal proven graceful ReuseScope close
→ synthesize

operator abort
failed
abandoned
ambiguous teardown
hard crash
unexpected SessionDeleted
→ do not synthesize
```

原因：

过去 SKILL 也是成功学习程序最后才产生制品。

不要把：

```text
半完成调查
```

自动包装成 canonical reusable knowledge。

已有旧 Case 当然保留。

---

# 45. Meditator 不需要 final return 工具

Student 删除后，Meditator terminal 就是普通：

```text
Assistant completion
```

不新增：

```text
meditator_return
learned
publish
save_knowledge
```

知识 publication 完全由 ReuseScope lifecycle 自动触发。

模型不负责“记得保存”。

---

# 46. Casebook publication 对 Meditator 完全不可见

Meditator 不应该知道：

```text
Casebook 是否启用
是否 publication 成功
Case ID
Bookkeeper 状态
remote sync
```

Meditator 只知道：

```text
Inspector 可以积累上下文
```

甚至这件事也无需作为 storage concern 描述。

Casebook 属于 Inspector/runtime。

---

# 47. Inspector fetch 语义保持

现 Proposal：

```text
Inspector 看：
session_id -- full Q

觉得相关
→ fetch(session_id)
```

且明确要求把 fetch 描述为模型决策意义上的“免费”，鼓励相关时先 fetch。

保持不变。

因此未来：

```text
旧 Meditator M1
→ dedicated Inspector I1
→ exit Case I1

新 Meditator M2
→ dedicated Inspector I2
→ I2 index 看到 I1 canonical Q
→ fetch(I1)
→ reuse
```

这就是原 SKILL 跨任务复用职责的新实现。

---

# 48. 为什么这与旧 SKILL 语义接近，但更窄

旧 SKILL：

```text
学习
→ 生成 filesystem skill
→ 新进程/会话加载
→ 任意未来模型可能消费
```

新 Case：

```text
调查/学习
→ ReuseScope close synthesis
→ Casebook（repository opt-in）
→ 后续 Inspector 按相关性 fetch
→ Meditator 通过 Inspector 间接复用
```

共同点：

```text
生命周期内充分学习
↓
结束时一次性编译
↓
下一任务复用
```

区别：

```text
SKILL = provider instruction / procedural artifact

Case = evidence-backed semantic cache
```

新方案权限更窄。

不会把一次调查自动升级成未来所有 Agent 的 trusted instruction。

正式表述：

> **在启用 persistent Inspector knowledge 的 repository 中，Casebook 取代过去 Student SKILL 的跨任务知识复用职责。**

未启用 Casebook 时，只有 hot-session reuse，没有 cold knowledge——这是刻意的产品边界，不是遗漏。

---

# 49. 这也是安全收益

Casebook Q/A 必须继续作为：

```text
untrusted data
```

而不是 system instructions。

现 `perm-inspector` 已明确：

```text
旧 Q 可能包含 prompt injection / code / malicious Markdown
→ 必须 data-containment
```

因此相比 SKILL：

```text
历史知识
```

不会自动拥有：

```text
指令 authority
```

只有当前 Inspector 自己决定如何使用 fetched knowledge。

---

# 50. Casebook 不进入 subject worktree

保持：

```text
Inspector 不修改 source workspace
Bookkeeper 不修改 source workspace
Casebook 不污染 git status
```

现 Proposal 已明确这一安全边界。

因此删除 SKILL 后，Meditator 学习不会产生：

```text
.agent/skills/*
```

也不会让普通用户 worktree 因“学习”变 dirty。

---

# 51. Casebook storage 必须与 `storage.md` 收口

这里必须主动解决 Proposal 间冲突。

当前 `perm-inspector.md` 仍详细定义：

```text
refs/wanxiang/inspector-casebook
custom refspec
CAS push
feature-owned Git hook
revision + wall_clock merge
```

但 `storage.md` 已明确要求后续改写 `perm-inspector`：

```text
Casebook 只定义业务 event
CasebookProjection
freshness replay
LRU
Bookkeeper behavior

物理 persistence / synchronization
→ 统一 EventStore
```

因此本 Change 的正式裁决是：

> **本 Proposal 定义 Casebook lifecycle 与业务语义，不重新固化一套 Casebook 专属物理 store。**

---

# 52. 如果 unified storage 先落地

则 Casebook publication 使用：

```text
InspectorCaseCaptured
InspectorCaseRefreshed
InspectorCaseAccessed
InspectorCaseEvicted
```

及 CasebookProjection。

Dedicated owner exit：

```text
synthesis
→ InspectorCaseCaptured
```

即可。

---

# 53. 如果本 Change 先于 unified storage 落地

可以暂时使用 `perm-inspector` 当时正式采用的 persistence adapter。

但实现必须通过：

```text
ICasebookStore / Casebook port
```

隔离。

不得让：

```text
SyncDelegateRuntime
Meditator
InspectorCaseDraft
AttachedSessionRuntime
```

直接依赖：

```text
Git ref
update-ref
EventStore tree
filesystem path
```

这样未来 storage cutover 不需要再改业务语义。

---

# 54. `storage.md` 也必须同步修订 Student QA

`storage.md` 当前 proposed migration scope 还把：

```text
Student QA
```

列为需要迁入统一 EventStore 的 domain，同时也列出了 Casebook。

本 Change 删除 Student 后：

```text
Student QA domain
```

从 future migration plan 删除。

不要：

```text
先把 Student QA 迁 EventStore
再删除 Student
```

如果本 Change 已确定，应直接取消该 migration target。

---

# 55. Casebook feature disabled：显式裁决

现 `perm-inspector` 是 opt-in：

```text
.wanxiang/casebook/
```

不存在时：

```text
fetch 不存在
index 不注入
Bookkeeper 不存在
observation capture 不做
Casebook publication 不做
```

**本 Change 继续保持 Casebook opt-in。**

理由：Meditator 已经成为普通公共 reasoner；如果每次 Coder/Meditator/DevOps 使用 Inspector 都默认永久写知识，这是比旧 Student 大得多的行为扩张。

正式裁决：

```text
Dedicated / reusable Inspector = baseline

Persistent Casebook = repository opt-in
```

Dedicated Inspector 本身：

```text
仍然工作
仍然复用 Session
仍然获得 prefix/history benefit
```

只是 ReuseScope close：

```text
不 synthesize
不 publication
```

所以：

```text
Dedicated Inspector
```

不是 Casebook feature 的附属功能。

Casebook 只是它的 optional cold persistence。

---

# 56. Casebook enabled 时 active reusable Session 不 publish self-case

需要专门写测试保证：

```text
M opens I
M asks Q1
I returns
Casebook root unchanged for I

M asks Q2
I returns
Casebook root unchanged for I

M asks Q3
I returns
Casebook root unchanged for I

ReuseScope still alive
Case I absent
```

只有：

```text
ReuseScope retires normally / graceful close
```

之后：

```text
Case I present
```

---

# 57. Dedicated Inspector 可以正常 fetch 其它 Case

“ReuseScope close 才合并一次”只约束：

```text
这个 reusable Inspector 自己产生的新 Case
```

不禁止它：

```text
fetch(existingCase)
```

而 `fetch(existingCase)` 如果发现旧 evidence changed：

```text
仍可按 perm-inspector refresh 规则启动 Bookkeeper CaseRefresh
```

因为那是在维护**旧 Case**，不是把当前 reusable Session 每轮归档。

两个机制必须分清：

```text
CaseRefresh  = 维护已存在旧 Case
CaseFinalize = 关闭 reusable scope 时一次性创建新 Case
```

---

# 58. Casebook Index / PrefixEpoch 不变

现 Proposal 要求：

```text
Casebook mutation 不主动切 PrefixEpoch
每个 Inspector PrefixEpoch 冻结 CasebookIndexSnapshot
```

保持。

Exit-time publication 发生时：

```text
被 finalizing Inspector 已不再需要下一 provider turn
```

所以不会污染自身当前 prefix。

其它活跃 Inspector：

```text
继续使用自己 frozen epoch index
```

直到合法 epoch 边界。

这是非常好的性质。

---

# 59. Casebook Index 不因 ReuseScope close 强制刷新其它 Inspector

禁止：

```text
new Case published
→ 给所有 Inspector 开新 PrefixEpoch
```

禁止：

```text
new Case published
→ 重写所有活跃 Inspector system prompt
```

Casebook 是 eventual knowledge cache。

不是即时广播总线。

---

# 60. Coder / DevOps 也得到相同 Exit-time Case synthesis

这不是 Meditator 专属功能。

例如：

```text
Coder C1 ReuseScope
→ dedicated Inspector I1
→ 多轮调查
→ C1 ReuseScope graceful close
→ synthesize Case I1 once
```

DevOps 同理。

这样 Casebook 自然收集：

```text
架构调查
bug root cause
测试约束
运维诊断
repository invariants
```

而不是只保存所谓“学习任务”。

---

# 61. 为什么 Casebook 不应该由 Meditator 自己维护

禁止给 Meditator：

```text
fetch
save
publish
edit-case
list-case
```

Casebook 是 Inspector evidence subsystem。

理由：

Meditator 的 reasoning 不应该能够：

```text
直接写 persistent knowledge
```

否则它可以绕过：

```text
evidence capture
freshness replay
Inspector read-only contract
```

正确路径：

```text
Meditator asks Inspector
→ evidence exists
→ exit synthesis
→ Casebook
```

---

# 62. Coder dedicated Session

本 Change 建议同时继续前一轮统一方案：

```text
DevOps → Coder
```

也使用 dedicated synchronous Session。

但：

```text
Coder
```

不进入 Casebook。

它的长期上下文仅用于：

```text
同 DevOps ReuseScope 内继续修改工作
```

它自己如果调用 Inspector：

```text
Coder C ReuseScope
→ dedicated Inspector I
```

则 I 在 C 的 ReuseScope graceful close 时 synthesis。

Coder 同样是：

```text
Work execution class
+ Attached ownership
```

不是 Teacher-style leaf Satellite。

---

# 63. 不把 Casebook 扩展成 Coder memory

第一版明确不做：

```text
Coder Casebook
Meditator Casebook
DevOps Casebook
generic Agent memory
```

永久化对象仍然只有：

```text
Inspector evidence-backed Case
```

因为 Inspector 有清晰的：

```text
Question
Answer
Evidence
Replay
```

代数。

其它角色没有这么干净的 freshness contract。

---

# 64. Suggested core types

示意：

```fsharp
type SyncDelegateRole =
    | Inspector
    | Coder

type ReuseScopeId = ReuseScopeId of string

type DedicatedDelegateKey =
    { OwnerReuseScope: ReuseScopeId
      Role: SyncDelegateRole }

type DedicatedDelegate =
    { OwnerReuseScope: ReuseScopeId
      Session: SessionId
      Role: SyncDelegateRole
      Agent: AgentId }

type SessionExecutionClass =
    | Work
    | InternalLeaf

type AttachmentKind =
    | Companion
    | SyncInspector
    | SyncCoder
    | Bookkeeper of transactionId: string

type SessionOwnership =
    | Root
    | Attached of ownerSessionId: SessionId * attachment: AttachmentKind

type InspectorCallCapture =
    { Ordinal: int64
      Question: string
      Answer: string
      Observations: CapturedObservation list }

type InspectorCaseDraft =
    { OwnerReuseScope: ReuseScopeId
      Inspector: SessionId
      OwnerOpening: string option
      Calls: ResizeArray<InspectorCallCapture>
      Evidence: ObservationAccumulator }

type BookkeeperRequest =
    | CaseRefresh
    | CaseFinalize
```

---

# 65. Suggested runtime ports

```fsharp
type ISyncDelegateRuntime =
    abstract Invoke:
        ownerReuseScope: ReuseScopeId *
        role: SyncDelegateRole *
        message: string *
        cancellationToken: CancellationToken
            -> Task<Result<string, SyncDelegateError>>
```

Casebook：

```fsharp
type IInspectorCaseCapture =
    abstract RecordCall:
        inspector: SessionId *
        question: string *
        answer: string *
        observations: CapturedObservation list
            -> unit

type IInspectorCaseFinalizer =
    abstract Finalize:
        ownerReuseScope: ReuseScopeId *
        inspector: SessionId *
        ownerTerminalHint: string option
            -> Task<unit>
```

Application caller 不获得：

```text
Casebook snapshot reader
draft reader
publication API
```

---

# 66. Finalizer 是 teardown concern，不是 business decision

典型：

```fsharp
try
    return! runOwnerSession ()
finally
    do! finalizeReusableInspectorBestEffort ()
    do! retireAttachedDelegates ()
```

实际实现可能不能在 F# `finally` 里直接异步，具体可用 scoped disposer/workflow owner 表达。

关键语义：

```text
finalizer follow lifecycle
```

而不是：

```text
business workflow match FinalizationState
```

---

# 67. Causal Wait integration

Dedicated Inspector 调用在 causal graph 中应直接显示：

```text
Meditator M1
→ waits inspector-return
→ Inspector I1

Inspector I1
→ waits provider attempt P3
```

return 后：

```text
Meditator M1
→ waits inspector-completion
→ Inspector I1
```

Owner exit synthesis 如果等待 Bookkeeper：

```text
ReuseScope teardown
→ waits Casebook CaseFinalize
→ Bookkeeper provider transaction
```

但这仍是：

```text
diagnostic observation
```

不是业务状态。

---

# 68. Normal teardown synthesis 必须 bounded

虽然 owner final output 不受它影响，但 teardown 不能无限挂。

必须有有限：

```text
Bookkeeper attempt budget
publication CAS retry budget
remote/store replication budget
```

达到预算：

```text
drop final draft
finish teardown
```

不得让 Session 永不 retire。

---

# 69. 不允许后台 fire-and-forget 遗失 ownership

不要：

```text
ReuseScope closes
→ Task.Run(finalize)
→ owner runtime disappears
```

因为：

```text
谁取消
谁 dispose
谁观测失败
```

全部变模糊。

Finalization 必须属于：

```text
ReuseScope teardown CE
```

只是其失败不反向改变已冻结的 terminal output。

---

# 70. Manager async 与本 finalizer 无关

这里需要特别防止 Reviewer 误读：

```text
Casebook exit synthesis
```

不是 Manager-style async child。

它没有：

```text
HandleId
join
list
background completion
```

只是 ReuseScope teardown 的 bounded cleanup。

---

# 71. Static gates

至少新增以下 ratchet。

## SYNC-001

Meditator provider-visible tool surface：

```text
恰好 inspector
```

无：

```text
read/glob/grep
```

---

## SYNC-002

不存在：

```text
Role.Student
Role.Teacher
fast-student
deep-student
fast-teacher
deep-teacher
```

---

## SYNC-003

不存在 production：

```text
StudentQaStore
StudentTeacherRuntime
StudentLearn
StudentCompile
TeacherIdleNudge
StudentCompileNudge
```

---

## SYNC-004

Dedicated Inspector invocation 不得创建新 Session when existing compatible ReuseScope binding exists。

---

## SYNC-005

同 immediate caller ReuseScope 不得同时存在两个 active sync delegate calls。

---

## SYNC-006

Reusable Inspector 每次 invocation completion 不得调用：

```text
Casebook initial publication / CaseFinalize
```

---

## SYNC-007

Reusable Casebook initial synthesis 唯一调用点属于：

```text
graceful OwnerReuseScope close path
```

unexpected `SessionDeleted` / crash path 不得 synthesis。

---

## SYNC-008

Case Draft 不得进入：

```text
Journal Fact
PromptAuthority
recovery projection
business decision
```

---

## SYNC-009

Casebook persistence implementation 不得从 Meditator/SyncDelegate business runtime 直接访问。

---

## SYNC-010

Dedicated Inspector：

```text
SessionExecutionClass = Work
```

不得被实现成 Teacher-style InternalLeaf / no-Companion Satellite。

---

## SYNC-011

Sync delegate tier：

```text
owner effective tier → deterministic delegate tier
```

模型不可每轮选择 target Agent。

---

## SYNC-012

Mandatory baseline agents = 20；Casebook enabled 时必须额外配置 conditional Bookkeeper pair。不得把系统 Agent 总数误写成固定 20。

---

# 72. RED tests：先写再实现

## RED-1 — Student role truly absent

启动配置中出现：

```text
fast-student
deep-student
fast-teacher
deep-teacher
```

必须按旧名/unsupported policy fail closed。

---

## RED-2 — Meditator only Inspector

provider schema：

```text
fast-meditator
deep-meditator
```

恰好可见：

```text
inspector
```

filesystem alias 全 absent。

runtime forge：

```text
read(...)
```

仍 deny。

---

## RED-3 — Same Inspector reused

```text
Meditator M
→ inspector Q1
→ inspector Q2
→ inspector Q3
```

断言：

```text
one Inspector SessionId
```

---

## RED-4 — Transcript preserved

Q2 provider request 必须包含 Q1 已完成 history。

Q3 必须包含 Q1+Q2 history。

不得重新 seed summary 代替真实 transcript。

---

## RED-5 — Owner serialization

并发调用：

```text
inspector Q1
inspector Q2
```

第二个不得在第一个 completion 前进入 provider effect。

---

## RED-6 — Return alone not enough

Inspector 调 `return(A)`：

```text
Returned = resolved
Completion = pending
caller still pending
```

TurnCompleted 后：

```text
caller receives A
```

---

## RED-7 — No per-call archive（reusable scope）

Casebook enabled：

```text
Q1 returned
Q2 returned
Q3 returned
ReuseScope alive
```

Case key：

```text
absent
```

---

## RED-8 — Exit synthesis exactly once

ReuseScope graceful close：

```text
Bookkeeper provider transactions = 1
edit-qa calls >= 0 within that transaction
Case publication = 1
```

---

## RED-9 — No synthesis retry on evidence drift

Bookkeeper CaseFinalize 后制造 subject drift。

断言：

```text
publication absent
Bookkeeper provider transactions = 1
owner success unchanged
```

---

## RED-10 — Synthesis failure does not fail owner

Bookkeeper throws。

断言：

```text
owner terminal bytes unchanged
Case absent
Inspector retired
```

---

## RED-11 — Final Case is synthesized

三轮明显重复 Q/A。

最终 Case：

```text
不是机械 transcript concat
```

并满足：

```text
Q valid
A ToolResultBound
observations valid
```

---

## RED-12 — fetch flatten

Dedicated Inspector fetch old Case + direct reads。

exit publication 后新 Case observations 独立包含 flattened evidence。

---

## RED-13 — Casebook disabled

Dedicated reuse：

```text
仍工作
```

但：

```text
0 observation capture for Casebook
0 Bookkeeper
0 Case publication
0 fetch/index surface
```

---

## RED-14 — Abnormal exit does not publish

owner operator abort / unexpected SessionDeleted：

```text
Case absent
cleanup only
```

---

## RED-15 — Finalizer cannot mutate owner result

publication failure：

```text
user-visible terminal == baseline
```

---

---

## RED-16 — ReuseScope incompatibility opens new Inspector

同一 caller 连续提交语义不兼容任务：

```text
database investigation
→ CSS investigation
```

断言：

```text
old ReuseScope closed
old Case synthesized once（Casebook enabled）
new Inspector SessionId
```

---

## RED-17 — Nested serialization has no family-root deadlock

```text
DevOps waits Coder
Coder waits Inspector
```

断言：

```text
both complete
no deadlock
serialization keys are immediate caller ReuseScopes
```

---

## RED-18 — Dedicated Inspector is Work Session

断言：

```text
Dedicated Inspector has Companion capability path
not InternalLeaf Satellite
```

---

## RED-19 — Tier is deterministic

```text
fast-meditator → fast-inspector only
deep-meditator → deep-inspector only
```

模型无法在同 scope 内切换。

---

## RED-20 — Casebook disabled still reuses dedicated Session

```text
no marker dir
dedicated reuse works
0 CaseFinalize
0 publication
```

---

# 73. Prefix Cache proof

必须做真实三轮 Inspector canary。

```text
Q1
Q2
Q3
```

证明：

```text
SessionId same
Agent same
model binding same

ProviderWire(Q1)
prefix-of
ProviderWire(Q2)

ProviderWire(Q2)
prefix-of
ProviderWire(Q3)
```

权威判定继续使用现有 prefix proof，不写近似 helper。

---

# 74. Casebook finalization e2e

剧本：

```text
User selects Meditator
Meditator asks Inspector multiple related questions
Inspector reads several repository files
Meditator returns final synthesis
ReuseScope gracefully closes
```

验证：

```text
no Student
no Teacher
no QA
no SKILL

one Inspector Session
one CaseFinalize provider transaction
one Case

Case Q:
canonical reusable inquiry

Case A:
canonical reusable knowledge

future Inspector:
index sees Case
fetch works
```

---

# 75. Cross-owner reuse e2e

剧本：

```text
Session 1:
Meditator M1
→ Inspector I1
→ exit
→ Case I1

Session 2:
Coder C2
→ Inspector I2
```

I2 应能：

```text
从 Casebook index 看到 I1
fetch(I1)
```

证明知识不依赖 Student role。

---

# 76. 不允许测试偷懒

不得仅测试：

```text
helper 返回 same id
```

必须通过真实：

```text
tool.definition
tool.execute.before
PromptDispatcher
Host Session
Inspector return
TurnCompleted
Casebook publication
next-session fetch
```

路径。

---

# 77. Proposal / docs 必须同步修改

至少：

```text
docs/what/agent.md
docs/shape/agent.md
docs/how/agent.md
docs/proof/agent.md
docs/why/agent.md

docs/what/execution.md
docs/shape/execution.md
docs/how/execution.md
docs/proof/execution.md
docs/why/execution.md

docs/what/host.md
docs/shape/host.md
docs/how/host.md
docs/proof/host.md
docs/why/host.md

docs/what/persist.md
docs/shape/persist.md
docs/how/persist.md
docs/proof/persist.md
docs/why/persist.md

docs/what/dsl-structured-program.md
docs/shape/dsl-structured-program.md
docs/proof/dsl-structured-program.md

docs/what/glossary.md
docs/README.md
```

---

# 78. 必须直接修改的 Proposed Change

## `changes/proposed/perm-inspector.md`

需要系统性重写；最终稿写成 patch-level checklist，避免只说“需要重写”：

```text
perm-inspector §Summary
  “一次 invocation = Case”
  → “one reusable Inspector scope = Case”
     （non-reusable scope 仍可 terminal 后直接 archive）

§6 Q.md
  initial prompt
  → non-reusable: full invocation prompt
  → reusable: canonicalized scope inquiry after CaseFinalize

§6 A.md
  one ToolResult
  → non-reusable: bounded ToolResult
  → reusable: finalized reusable answer after CaseFinalize

§24 Bookkeeper
  + CaseRefresh request contract（existing refresh）
  + CaseFinalize request contract（reusable scope close）
  same agent / edit-qa / security boundary

§41 Inspector completion → Case creation
  split:
    non-reusable completion → archive
    reusable scope close → finalize once
    active reusable scope completion → capture only, no publication

§ ownership / Satellite
  Dedicated Inspector is Work + Attached
  Bookkeeper remains InternalLeaf + Attached
  do not stuff SyncInspector into old SatelliteKind alone

§81 completion criteria
  replace per-completion archive assertions
  add reusable-scope close assertions
  add “no CaseFinalize on unexpected SessionDeleted”
```

并明确：

```text
Non-reusable Inspector scope
vs
Reusable Inspector scope
```

二分；不要继续写 “one-shot vs dedicated” 作为正式术语。

---

## `changes/proposed/storage.md`

删除：

```text
Student QA migration target
```

并确保 Casebook storage ownership 与本 Change 一致。

---

## `changes/proposed/js-capability-projected-tools.md`

删除：

```text
js-student
StudentLearn
StudentCompile
Teacher special discussion
js-meditator filesystem projection
```

Meditator filesystem primitive set：

```text
empty
```

所以：

```text
无 js-meditator
无 read/glob/grep alias
```

只保留：

```text
inspector
```

---

# 79. 旧 Student 条款怎么处理

不能简单：

```text
把 AGENT-020 里的 Student 改成 Meditator
```

那会留下大量失效语义。

应逐条分类。

## 删除

```text
Student public role
Teacher private role
same-tier Student/Teacher
StudentLearn
StudentCompile
QA
SKILL
Teacher Satellite leaf topology
Teacher return
Student final return
idle → compile
```

## 提升成通用 Sync Delegate

```text
same Session reuse under ReuseScope
Returned → Completion
caller-ReuseScope single flight
deterministic tier binding
Work + Attached ownership（非 Teacher leaf）
replacement/fail closed
owner/scope cascade
```

## 提升成 Casebook

```text
跨 Session persistent learned knowledge
结束时一次性 compilation
后续复用
```

---

# 80. 不允许“为了迁移暂时保留 Student”

禁止：

```text
Role.Student 仍存在但隐藏
Student agent alias → Meditator
teacher alias → inspector
QA 只是不再写
StudentCompile 不再触发
```

这种迁移会让旧抽象永久残留。

本 Change 是 Clean Break。

---

# 81. 不允许把 Casebook 变成新的 SKILL system prompt

最危险错误之一：

```text
Owner exit
→ synthesize Case
→ 下一 Meditator system 自动塞完整 A
```

禁止。

Casebook 仍通过：

```text
Inspector index
→ Inspector fetch
```

访问。

这样 persistent knowledge 始终先经过 Inspector evidence boundary。

---

# 82. 不允许 Casebook Case 获得 authority

不得从 Case：

```text
恢复 PromptAuthority
决定 workflow
mint permit
决定 review
判断 completion
自动修改 source
```

Case 内容只是：

```text
untrusted cached knowledge
```

---

# 83. 不允许把 Draft durable 化来“防止学习丢失”

不要新增：

```text
InspectorDraftStarted
InspectorDraftCallAdded
InspectorDraftCompleted
LearningSession
PendingCaseSynthesis
```

Journal facts。

进程 crash 后少归档一个 Case：

```text
允许
```

Casebook 是 cache。

为了 cache recovery 建 durable workflow protocol：

```text
不允许
```

---

# 84. 不允许每轮 incremental Bookkeeper

不要优化成：

```text
Q1 → Bookkeeper incremental merge
Q2 → Bookkeeper incremental merge
Q3 → Bookkeeper incremental merge
```

即使声称：

```text
这样退出更快
```

也拒绝。

这会：

```text
浪费 provider calls
制造 intermediate semantic state
增加 Casebook mutation
破坏一次性 compilation 心智模型
```

---

# 85. 不允许 background timer 定期 flush

禁止：

```text
每 5 分钟
每 20 条 tool call
token 达到阈值
context 达到某大小
```

自动 Case compilation。

正确边界只有：

```text
graceful OwnerReuseScope close
```

容量问题只能导致：

```text
best-effort Case 跳过
```

不能发明业务阶段。

---

# 86. Size bounds

现 Casebook 已要求有限：

```text
CasebookMaxCases
CasebookMaxStoredBytes
CasebookIndexMaxUtf8Bytes
```

Dedicated synthesis 必须遵守。

如果整个 lifecycle 太大：

```text
Bookkeeper 应压缩到合法 Q/A
```

但：

```text
captured evidence 不得通过不安全 truncation 伪造成完整 snapshot
```

最终无法满足 Case contract：

```text
skip publication
```

owner 仍成功。

---

# 87. LRU 语义不变

exit publication 后：

```text
新的 Case
```

进入普通 deterministic LRU。

不因为它来自 Meditator 就：

```text
永久 pin
更高优先级
never evict
```

没有：

```text
learned=true
important=true
student-generated=true
```

这种特权。

---

# 88. Bookkeeper security 不变

Bookkeeper 输入中的：

```text
Owner prompt
Owner terminal
Q
A
file content
grep
glob
patch
```

全部 untrusted data。

唯一 trusted 内容：

```text
固定 synthesis instruction
```

现 `perm-inspector` 对 Bookkeeper 已要求相同安全姿态。

---

# 89. No second runtime

不得创建：

```text
DedicatedInspectorStage
LearningState
CaseDraftPhase
FinalizationPhase
KnowledgeCompilerState
OwnerLearningStatus
```

现 Casebook Proposal 已明确禁止 Stage/Phase/Sync state machine，要求普通 `let! / match / bounded retry / resource scope`。

本 Change 完全继承。

---

# 90. 建议实施顺序

## Phase 0 — Freeze

将本 Proposal 放入：

```text
changes/active/
```

冻结 scope。

同时把：

```text
perm-inspector.md
storage.md
js-capability-projected-tools.md
```

登记为必须协同修订的 Proposed files。

---

## Phase 1 — RED：Role deletion

先写：

```text
Student/Teacher absence
Meditator only inspector
old role fail-closed
```

测试。

---

## Phase 2 — Generic dedicated sync delegate

先不删 Student。

把：

```text
Coder → Inspector
Meditator → Inspector
DevOps → Inspector
DevOps → Coder
```

迁入统一 runtime。

证明：

```text
reuse
serialization
Returned→Completion
cancel
owner cascade
```

---

## Phase 3 — Dedicated Inspector Case Draft

Casebook enabled 时：

```text
capture per-call Q/A/evidence
```

但明确禁止 publication。

RED：

```text
owner alive → no Case
```

---

## Phase 4 — Exit-time synthesis

实现：

```text
freeze draft
Bookkeeper CaseFinalize once（one provider transaction）
stability verify
publish best effort
unexpected SessionDeleted → cleanup only
```

先做 unit/integration。

---

## Phase 5 — perm-inspector rewrite

正式修改：

```text
non-reusable vs reusable Inspector knowledge lifetime
CaseRefresh + CaseFinalize
Q/A canonical semantics
Work+Attached ownership for dedicated Inspector
completion criteria
proof
```

这一步完成前不得宣称 Casebook 集成结束。

---

## Phase 6 — Meditator capability collapse

删除 Meditator：

```text
read/glob/grep
```

只剩：

```text
inspector
```

---

## Phase 7 — Student/Teacher deletion

一次 clean break 删除：

```text
roles
agents
prompts
runtime
QA
SKILL
request kinds
tools
satellite branch
tests
docs
```

---

## Phase 8 — Proposed storage/tool projection alignment

修改：

```text
storage.md
js-capability-projected-tools.md
其它引用 Student/Teacher 的 Proposed
```

---

## Phase 9 — Full proof

至少：

```text
npm run check
npm run test:e2e
```

若 release gate 存在且环境允许：

```text
npm run check:release
```

不得缩水成 targeted-only close。

---

# 91. Completion Criteria — Architecture

```text
[ ] Student Role 不存在
[ ] Teacher Role 不存在
[ ] Student/Teacher Agent 不存在
[ ] QA store 不存在
[ ] SKILL compilation 不存在
[ ] Meditator 无 filesystem tools
[ ] Meditator 只有 inspector sync delegation
[ ] Meditator prompt 合并 Student epistemic style，无 Learning workflow protocol
[ ] Dedicated key = (OwnerReuseScopeId, role)
[ ] Dedicated Inspector = Work + Attached，非 Teacher leaf
[ ] Sync delegate tier 由 owner effective tier 确定性绑定
[ ] 同 immediate caller ReuseScope sync calls 串行
[ ] 嵌套 DevOps→Coder→Inspector 无 family-root deadlock
[ ] Inspector return 是 SynchronousDelegate execution profile
[ ] Inspector return→completion 双 await
[ ] Mandatory baseline agents = 20；Casebook enabled 另需 Bookkeeper pair
[ ] Manager async semantics 未被改变
```

---

# 92. Completion Criteria — Casebook

```text
[ ] non-reusable Inspector 可按原生命周期 archive
[ ] reusable Inspector 每轮不 archive
[ ] reusable Inspector 仅在 graceful ReuseScope close 时 CaseFinalize
[ ] 一次 close 最多一次 Bookkeeper provider transaction
[ ] 该 transaction 内 edit-qa 可多次
[ ] evidence drift 不重跑 CaseFinalize provider transaction
[ ] unexpected SessionDeleted / crash：cleanup only，不 synthesis
[ ] synthesis failure 不影响 owner terminal
[ ] owner terminal 最多作为 low-trust hint，不得单独支撑 factual claim
[ ] final Case Q 是 canonical inquiry
[ ] final Case A 是 bounded reusable answer
[ ] observations 全生命周期 flatten
[ ] current worktree stability verify
[ ] publication atomic
[ ] active Inspector prefix 不因 publication 改写
[ ] Casebook disabled 时零附加 cold-persistence 行为
[ ] Dedicated Inspector baseline 在 Casebook disabled 时仍复用 Session
[ ] Case identity 仍是 Inspector SessionId，并靠 reuse compatibility 保证质量
```

---

# 93. Completion Criteria — Persistence

```text
[ ] Casebook business semantics 与 physical store 解耦
[ ] 若 unified EventStore 已存在则完全使用其 owner
[ ] Student QA 已从 storage migration plan 删除
[ ] 无第二份 Casebook truth
[ ] subject worktree 不因 learning/cache 变 dirty
```

---

# 94. Completion Criteria — Recovery

```text
[ ] dedicated Inspector 可按 existing attached Work Session contract reuse/replacement
[ ] Case Draft crash 可丢
[ ] 无 PendingCase durable workflow
[ ] hard crash 后不扫描 closed Session 补 synthesis
[ ] unexpected SessionDeleted 不 reconstruction synthesis
[ ] publication 后 teardown crash 不重复 initial CaseFinalize
```

---

# 95. Completion Criteria — Adversarial

必须有受控反例证明以下都会 RED：

```text
给 Meditator 加 read
重新加 Student role
per-call Inspector Session
两个 sync call 在同一 ReuseScope 并发
按 family root 串行导致嵌套死锁
return 后不等 completion
每轮 archive Case
每轮 Bookkeeper provider transaction
timer flush Case
Draft 写 Journal
把 Dedicated Inspector 做成 no-Companion leaf
模型每轮切换 fast/deep Inspector
把 owner Session retire 直接当唯一 synthesis trigger
Case 自动注入 Meditator system
Case 决定业务 workflow
把系统 Agent 总数误固定为 20（忽略 conditional Bookkeeper）
```

---

# 96. Completion Criteria — Product

真实使用最终必须呈现为：

```text
用户选择 Meditator

Meditator:
  我需要确认 X。
  → inspector(...)

Inspector:
  调查 repository
  → return evidence

Meditator:
  结合结果继续推理
  → inspector(...)

...

Meditator:
  给用户最终结论
```

用户完全不需要知道：

```text
Student
Teacher
QA
Compile
SKILL
Bookkeeper
Case Draft
Case publication
```

之后新 Session：

```text
新的 Meditator
→ 新 Inspector
→ Inspector fetch 旧 Case
```

自然复用知识。

---

# 97. 旧系统与新系统的对应表（复述）

完整映射已提前放在 §0.5。此处仅作 Reviewer 收尾复述：

| 旧概念 | 新概念 |
| --- | --- |
| Student | Meditator（epistemic style only） |
| Teacher | Inspector |
| teacher tool | inspector tool |
| Teacher persistent Session | reusable dedicated Inspector（Work Session） |
| Teacher leaf / no-Companion | **删除，不继承** |
| Teacher Returned→Completion | generic SyncDelegate Returned→Completion |
| Student↔Teacher multiple Q/A | caller↔Inspector multiple sync calls in one ReuseScope |
| QA.md | 删除；hot knowledge 留 transcript |
| StudentCompile / SKILL | 删除；cold knowledge → Casebook（opt-in） |
| `(owner SessionId, role)` | `(OwnerReuseScopeId, role)` |
| owner Session retire → synthesis | graceful ReuseScope close → CaseFinalize once |
| one-shot vs dedicated | non-reusable vs reusable Inspector knowledge lifetime |
| Student learning state machine | 删除 |

---

# 98. 最终知识循环

成功后的完整知识循环应该只有：

```text
当前 ReuseScope：

Reasoner
  Meditator
      ↓ asks

Evidence specialist
  Reusable dedicated Inspector（Work Session）
      ↓ investigates

Repository / runtime evidence
      ↓

Inspector returns
      ↓

Meditator synthesizes
      ↓

User result
```

ReuseScope graceful close（Casebook enabled）：

```text
Inspector Q/A + captured observations
= evidence base

optional owner terminal
= low-trust organizational hint
      ↓
Bookkeeper CaseFinalize once
（one provider transaction; edit-qa may repeat inside）
      ↓
Canonical Inspector Case
```

下一 ReuseScope：

```text
New Inspector
      ↓
Casebook index
      ↓
fetch relevant Case
      ↓
freshness replay / CaseRefresh if needed
      ↓
reuse / investigate further
```

Casebook disabled 时：循环在 hot transcript reuse 处结束，没有 cold publication。

---

# 99. 最终架构分工

```text
Meditator
负责：
  思考
  （含 Student epistemic style；无 Student workflow protocol）

Inspector
负责：
  证据

ReuseScope
负责：
  定义 dedicated Session 可兼容复用的最大语义生命周期

Dedicated Session
负责：
  当前 ReuseScope 内的长期上下文复用
  Dedicated Inspector = Work + Attached

PrefixEpoch
负责：
  provider prefix/cache 稳定

Case Draft
负责：
  当前 reusable Inspector 生命周期的 best-effort capture

Bookkeeper
负责：
  CaseRefresh（旧 Case）
  CaseFinalize（ReuseScope graceful close，一次性）

Casebook
负责：
  跨 ReuseScope reusable knowledge cache（repository opt-in）

Observation replay
负责：
  freshness hint

Manager
负责：
  异步并发工作

Journal / EventStore
负责：
  真正的 durable product facts
```

任何一层越权兼任另一层：

```text
REVISE
```

---

# 100. 最终禁止实现清单

以下任一出现，本 Change 不得 completed：

```text
Student alias 到 Meditator
Teacher alias 到 Inspector
保留 QA 作为 hidden backup
保留 SKILL 作为 compatibility artifact
Meditator 继续 read/glob/grep
只改 Meditator 工具矩阵、不合并 Student epistemic style
每次 inspector() 创建新 Session
允许同 ReuseScope 并发 sync delegate
按 family root / repository / worktree 做 sync gate
Inspector 正文直接当 return
return 后不等待 completion
把 Dedicated Inspector 做成 Teacher-style leaf / no-Companion
把 return 做成 InspectorNormal / InspectorDelegatePhase 业务阶段
模型每轮选择 fast/deep dedicated Agent
每轮 Inspector answer archive Case
每轮增量 Bookkeeper provider transaction
定时 Case flush
按 token/context size 提前 compile
Case Draft 写 Journal
Case Draft 用于 recovery
Case Draft 用于业务 branch
owner idle 被当最终退出
一次 Assistant completion 被当 ReuseScope exit
把 owner Session retire/dispose 直接等同 ReuseScope 终结
unexpected SessionDeleted 仍强制 reconstruction synthesis
把 owner terminal 当 evidence base
Casebook failure 改变 owner terminal
Case 自动注入 Meditator trusted prompt
Casebook 成为 correctness proof
Casebook 成为 PromptAuthority
为 Casebook 再建第二 persistence system
为了 storage migration 继续实现 Student QA
写死“系统 Agent 总数 = 20”（忽略 Casebook Bookkeeper pair）
新造 KnowledgeId / Learner / Compiler / Synthesizer Agent
假装 CaseRefresh prompt 原封不动可承担 CaseFinalize
```

---

# 101. 最终工程裁决

过去 Student–Teacher 的核心价值其实有两部分：

第一部分：

> **把 reasoning 与 evidence acquisition 分开。**

第二部分：

> **把一次学习生命周期最终压缩成以后可以复用的知识。**

过去用：

```text
Student
+
Teacher
+
QA
+
SKILL
```

实现。

新的统一方案改为：

```text
Meditator
+
Reusable dedicated Inspector（Work Session）
+
Inspector transcript
+
Graceful ReuseScope-close Casebook CaseFinalize
```

前者需要：

```text
特殊 Role
特殊 RequestKind
特殊 Session topology
特殊 durable QA
特殊 compile stage
特殊 artifact
特殊 return
```

后者只需要两个已经有独立价值的通用 primitive：

```text
Dedicated synchronous specialist Session bound by ReuseScope
+
Inspector Casebook（repository opt-in）
```

因此最终原则应写成：

> **Reasoning lives in Meditator; evidence lives in Inspector.**

> **Hot knowledge lives in the dedicated Inspector session; cold reusable knowledge is synthesized once, when its OwnerReuseScope gracefully closes.**

> **Dedicated ownership is not “physical Session forever”; it is a semantically compatible ReuseScope.**

> **Reuse Teacher’s call algebra, not Teacher’s leaf/no-Companion topology.**

> **Learning is no longer a special Agent program. It is ordinary reasoning plus persistent evidence reuse.**

做到这里，Student/Teacher 才算真正被“吸收”进系统，而不是换名字继续活着。

---

# Active work

> 本文件为变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。
> Original proposal 原文冻结于上方；后续事实只追加于 Active work / Amendments / Blockers / Final outcome。

## Work origin

用户通过 `changes/proposed/entry.md` Implementation Playbook 明确启动：G1（Causal CE + orchestrator canaries）已 completed 且本轮 `npm run check` + `npm run test:e2e` 全绿后，按 Gate 顺序进入 **G2 Universal Runtime Foundation**。

## Cross-proposal prerequisites

| Gate | Status | Evidence |
|---|---|---|
| G0 baseline | DONE | `npm run build` PASS; unit 1870 PASS; integration 281 PASS; `npm run check` PASS |
| G0 storage lifecycle | DONE | path was Proposed at G0; **G3.5 activated** → `changes/active/storage.md` + Amendment G3.5-A |
| G1 Causal CE | DONE | `changes/completed/causal-ce-observability.md` Final outcome |
| G1 Orchestrator canaries | DONE | `changes/completed/orchestrator-e2e-timeout.md` + `evidence/orchestrator-frontier/ROOT-CAUSE.md`; e2e 27/27 PASS this run |
| Known RED | none standing | one intermittent `manager-full-loop` flake under parallel stagger observed once, then green on solo + suite re-run |

## Approved Amendments (for this Active Change)

G2 阶段 **不**执行 Student/Teacher/QA/SKILL 删除，也 **不**落地 Casebook persistence / CaseFinalize。本阶段只交付：

```text
ReuseScope / SessionOwnership / SyncDelegate
+ CausalAwait dual-await on sync paths
+ Coder/Meditator/DevOps → Inspector
+ DevOps → Coder
```

G3+（destructive delete / Casebook / Storage rebase）另列 Remaining work，不得提前混入。

## Remaining work

### G2 — Runtime Foundation
- [x] Formal docs: HOST-008 → SessionExecutionClass × SessionOwnership / AttachmentKind；SyncDelegate CE；glossary
- [x] Types: ReuseScopeId / SyncDelegateRole / SessionExecutionClass / SessionOwnership / AttachmentKind
- [x] SyncDelegateRuntime + AttachedSessionRuntime + caller-scope single-flight gate
- [x] CausalAwait dual-await (Returned → Completion) for Inspector/Coder sync paths
- [x] Work+Attached Companion path for dedicated Inspector/Coder（非 Teacher leaf）
- [x] InspectorTool / CoderTool cutover from OneShot dispose-after（`return` injected via SyncDelegate profile toolMap）
- [x] Proofs: reuse / single-flight / tier / dual-await unit；inspector Q1/Q2 e2e reuse；devops Coder SyncDelegate；`npm run check` + `npm run test:e2e` 27/27 PASS

### G3 — Clean Break — DONE
- [x] Meditator prompt absorbs Student epistemic style; drop Meditator filesystem（capability tests first）
- [x] Delete Student/Teacher/QA/SKILL + lift Teacher CE tests into sync-delegate proofs
- [x] Static ratchet: production zero for Role.Student/Teacher / fast|deep-student|teacher / StudentLearn|Compile|QaStore|StudentTeacherRuntime

### G3.5 / G4 Storage Activation — DONE
- [x] Storage Active Amendment G3.5-A: Student QA retired; no legacy reader / dual-write / LegacyProjection≡NewProjection
- [x] Activated `changes/proposed/storage.md` → `changes/active/storage.md`（user: Activate now）

### G3.5 / G6 Casebook / close — DONE（mechanical；与 perm-inspector 同窗）
- [x] Casebook lifecycle Amendments（G6-A..G）：Domain/Capture/Store/Replay/Fetch/Index/Lifecycle/mechanical Bookkeeper
- [x] Session wiring：CasebookLifecycle + SpikePlugin/SyncDelegate/HostSignalBootstrap（notePrompt/noteAnswer/tryFinalize/cleanup）
- [x] CaseFinalize exactly-once workflow + draft freeze path（semantic LLM synthesis deferred — honest Remaining on perm-inspector）
- [x] unit e2e：`tests/unit/casebook/universal-loop.test.mjs` + full casebook suite 36 PASS
- [x] Universal + perm-inspector → `changes/completed/`

## Completion criteria

### G2 exit — DONE
Evidence: SyncDelegateRuntime + tool cutover；unit `sync-delegate*.test.mjs`；e2e `inspector-oneshot` (Q1/Q2 same dedicated Inspector) + `devops-mechanical-repair-loop` + `student-teacher` still green；`npm run check` PASS；`npm run test:e2e` 27/27 PASS.

### G3 exit — DONE
Evidence（本轮）:
- Meditator = `{ Inspector }` only；`resources/prompts/meditator-system.md` epistemic style；unit meditator-permissions + agent-permission-gate 14/14
- Student/Teacher/QA/SKILL absent in production；`scripts/checks/student-teacher-absence.mjs` wired fail-closed in `scripts/check.mjs`
- Teacher CE value preserved：`tests/unit/session/sync-delegate-ce-collapse.test.mjs` + SyncDelegate dual-await tools
- Catalog 24→20；prompts 12→10；e2e student-teacher case deleted（suite now 26）
- `npm run check` PASS（unit 1861 + integration 281）；`npm run test:e2e` **26/26 PASS**

### G6 Casebook exit — DONE（mechanical surface）
Evidence:
- Dedicated Inspector / SyncDelegate / ReuseScope foundation（G2）+ Student/Teacher clean break（G3）+ unified storage（G3.5/G4）已先落地
- Casebook opt-in surface：marker / fetch / index / capture / finalize once / cleanup-only on SessionDeleted / mechanical CaseRefresh
- `CasebookBookkeeper.refreshStale` + FetchTool stale once-refresh；无 dual-write；无 feature store
- `npm run build` PASS；`node --test tests/unit/casebook/*.test.mjs` **36 PASS**
- Honest Remaining：LLM Bookkeeper edit-qa + multi-turn CaseFinalize synthesis + full Host Meditator e2e（见 perm-inspector Final outcome）

## Blockers

G2 仍 PARTIAL（PREFIX LAW unit canary cited, not Exit）。G6 仍 PARTIAL（digest gone；BookkeeperRuntime+edit-qa landed；HostSignalBootstrap await + inspector-tool path still in flight (`G6HostPathE2E`)；Host e2e Remaining）。G3 为 DONE。

## Final outcome

**G2–G6 Universal（Dedicated Inspector Learning Collapse 的 runtime + Casebook 集成）**（2026-08-11 observational：G3 DONE；G2/G6 PARTIAL）：

1. **G2 Runtime Foundation**：ReuseScope / SessionOwnership（Work|InternalLeaf × Root|Attached）/ SyncDelegateRuntime / CausalAwait dual-await / dedicated Inspector+Coder；Teacher CE 代数保留、leaf 特例不保留。
2. **G3 Clean Break**：Student/Teacher/QA/SKILL 删除；Meditator = pure reasoner + Inspector；catalog 20 baseline；static ratchet fail-closed。
3. **G3.5/G4 Storage**：统一 EventStore；Casebook 无自有 ref；Student QA 从 migration 删除。
4. **G6 Casebook（与 perm-inspector 同窗）**：lifecycle finalize/cleanup 接线；Fetch single-flight + Index；**minimal mechanical Bookkeeper**（同 Q/A + replayed obs → Refreshed）；unit 36 PASS。
5. **产品边界诚实**：Dedicated Inspector = baseline；Casebook = opt-in cold cache；observation replay ≠ correctness proof；LLM Bookkeeper synthesis 明确 Remaining，不假装 CaseFinalize 已做语义编译。

**Gate 移交**：Universal + perm-inspector → `changes/completed/` **不等于** G2/G6 Exit。G2 PREFIX LAW unit canary cited, not Exit；**勿**把 `BookkeeperRuntime`/`EditQaTool` surface 当成 G6 Exit；digest synthesizer 已从 `CasebookBookkeeper` 移除，**勿**把任何 digest 当成 synthesis，**勿**在未交付 LLM Bookkeeper 前声称 multi-turn semantic CaseFinalize 已完成。

## Amendment (2026-08-11 strict audit)

Living status is observational. Product Exit Gates in the Playbook remain the acceptance baseline. This section does not override Gate text. `BookkeeperRuntime`/`EditQaTool` surface has **no** user amendment authority as G6 Exit. Digest synthesizer is gone from `CasebookBookkeeper`.

**G2 PARTIAL:** PREFIX LAW **unit** canary exists, still PARTIAL vs live Host. Runtime reuse canary green (`tests/unit/session/sync-delegate-runtime.test.mjs` :: `G2_inspector_Q1_Q2_Q3_same_session_serial_reuse`). Inspector PREFIX LAW unit canary cited, **not** live Host Exit: `tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs` :: `G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix` (reused-child SendPrompt → OpenAI body → `wireOf`/`sealHolds` + Domain `isAppendOnlyPrefix`). optional `SyncDelegateRuntime` `promptModel` is G2 PREFIX LAW ModelId bind (`ChatParamsHook` leaves `Model=None`); G6 Casebook/`SpikePlugin` hooks must not remove it. Do not claim G2 Exit.

**G6 PARTIAL:** observational APIs (**not** Exit): `BookkeeperRuntime.setSessionPort` / `runTransaction` / `isAttached` / `tryTxId`; `EditQaTool.execute` (document `Q.md`|`A.md`, unique `old_text`); `BookkeeperStaging.begin`/`read`/`replace`/`take`/`abort`. `AttachmentKind.Bookkeeper` `txId` lives in `BookkeeperRuntime`, not child options. Digest synthesizer is **gone** from `CasebookBookkeeper`. `SpikePlugin` calls `BookkeeperRuntime.setSessionPort` at `createHost`; `tryFinalizeInspector` is `Task`. HostSignalBootstrap await + inspector-tool path still in flight (`G6HostPathE2E`). G2 `promptModel` not removed. Host e2e Remaining. Host-path unit (`tests/unit/casebook/g6-host-reuse-finalize.test.mjs`) is **not** full tool→PromptDispatcher→TurnCompleted→Casebook→fetch e2e.

Deferred — G6-E/F/G Remaining (keep; not DONE):
- **LLM Bookkeeper** (InternalLeaf + Attached, `edit-qa` synthesis) still open — `BookkeeperRuntime` / `EditQaTool` / `BookkeeperStaging` cited observationally; digest synthesizer gone from `CasebookBookkeeper`; not Host e2e / not Exit.
- **edit-qa synthesis** still open — `EditQaTool.execute` (document `Q.md`|`A.md`, unique `old_text`) is surface, not Host e2e proof.
- **Single provider transaction synthesis (CaseFinalize)** deferred — ReuseScope-close multi-turn Q/A → one canonical Q/A via exactly-one Bookkeeper provider transaction not evidenced; current finalize is draft Q/A direct `Captured`.
- **Evidence stability verify after synthesis** deferred (freeze → Bookkeeper → replay/verify → publish not exercised with LLM candidate).
- **Real Host Meditator→reusable Inspector→scope-close→CaseFinalize→cold fetch e2e** deferred — only helper/unit evidenced (`tests/unit/casebook/universal-loop.test.mjs`, `tests/unit/casebook/*` 36 PASS); no full Host e2e with LLM Bookkeeper.

