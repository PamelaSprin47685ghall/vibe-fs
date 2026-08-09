# Proposal：Dedicated Inspector Learning Collapse — 删除 Student/Teacher，以 Meditator + Dedicated Inspector + Exit-time Casebook Synthesis 统一学习与知识复用

**Status:** Proposed
**Priority:** P0 / architecture + capability simplification + context reuse + persistent knowledge reuse
**Scope:** Agent roles / Meditator / Inspector / synchronous delegation / dedicated Session / Casebook / Student–Teacher removal / Prefix Cache / persistence integration
**Compatibility:** Clean break for Student / Teacher public and internal role semantics；Casebook feature-disabled repository 继续保持 Casebook 静默
**Related proposals:** `changes/proposed/perm-inspector.md`、`changes/proposed/storage.md`、`changes/proposed/js-capability-projected-tools.md`

---

# 0. Executive Decision

本 Change 做四个不可分割的裁决。

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

## 0.3 Inspector 调用改成 caller-owned dedicated Session

对所有同步 Inspector caller：

```text
Meditator → Inspector
Coder     → Inspector
DevOps    → Inspector
```

不再每次调用创建独立 Inspector Session。

而是：

```text
一个 owner Session
+
Inspector role
=
一个 dedicated Inspector Session
```

同一 owner 生命周期内：

```text
inspector(Q1)
→ same Inspector I

inspector(Q2)
→ same Inspector I

inspector(Q3)
→ same Inspector I
```

Session、transcript、PrefixEpoch、调查历史持续复用。

---

## 0.4 Dedicated Inspector 不再每次调用归档 Case

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

Dedicated Inspector 落地后，该模型不再成立。

新的裁决：

```text
owner Session alive
→ dedicated Inspector 可被同步调用很多次
→ 每次只积累 ephemeral Case Draft
→ 不 publication

owner Session 最终退出
→ 对该 dedicated Inspector 的整个生命周期
→ 做且只做一次 Case synthesis
→ publication 一个 canonical Case
→ retire Inspector
```

也就是说：

> **Hot knowledge 留在 dedicated Inspector transcript；cold reusable knowledge 只在 owner 退出时编译一次进入 Casebook。**

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
caller lifetime ownership
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
per-owner synchronous delegate serialization
```

不存在冲突。

---

# 4. Agent Catalog：24 → 20

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

因此固定 Agent 数改为：

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

---

# 5. Meditator 最终能力：纯 Reasoner

目标：

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

永久学习产物唯一变为：

```text
Inspector Casebook Case
```

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
该 owner 已经问过什么
Inspector 已调查过什么
哪些路径读过
之前的解释是什么
新问题和旧调查怎样关联
```

在 owner 活着时：

> **这是最高效的知识复用层。**

无需 Casebook roundtrip。

无需重新 fetch 自己刚刚知道的知识。

---

## 10.3 Casebook cold memory

owner 最终退出后：

```text
整个 Inspector lifetime
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

---

# 11. Dedicated Inspector identity

定义：

```text
DedicatedInspector(owner SessionId)
```

唯一物理绑定：

```text
(owner SessionId, Inspector)
→ at most one live Inspector Session
```

例如：

```text
Meditator M1
→ Inspector I1

Coder C1
→ Inspector I2

DevOps D1
→ Inspector I3
```

如果 Coder 本身是 DevOps 的 dedicated Coder：

```text
DevOps D1
  → Coder C1
      → Inspector I2
```

逻辑 ownership 可以嵌套。

Host 物理 parent 仍遵守现有 family-root flattening。

---

# 12. 同 owner 所有同步 delegate 必须串行

建议全局 invariant：

```text
一个 owner Session
同一时刻
最多一个 active synchronous delegate call
```

不是：

```text
每个 delegate 各自 single-flight
```

而是：

```text
owner-level single-flight
```

例如 DevOps：

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

# 14. Inspector 作为 dedicated callee 时增加 return

普通 Inspector Work Session：

```text
read
glob
grep
executor
fetch?    // Casebook enabled 时
```

Dedicated Inspector：

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

普通 assistant 正文：

```text
不得成为 tool result
```

idle：

```text
不得成为 tool result
```

reasoning：

```text
不得成为 tool result
```

这直接推广当前 Teacher 已经验证过的：

```text
Returned
→ Completion
```

协议。

---

# 15. 通用调用结构

建议：

```fsharp
task {
    use! ownerLease =
        syncDelegateGate.Acquire(owner)

    let! inspector =
        attachedSessions.GetOrCreateInspector(owner)

    use call =
        inspectorCalls.Begin(owner, inspector)

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
Session create
Session recovery
TCS
Returned
Completion
idle nudge
Host reconcile
```

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

# 17. Dedicated Inspector Session lifetime = owner lifetime

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
owner alive
→ Inspector alive

owner asks Q1
→ I

owner asks Q2
→ same I

owner asks Q3
→ same I

owner terminal/retire
→ finalize Case
→ retire I
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

# 19. `perm-inspector` 必须引入两种 Case lifecycle

当前 Proposal 默认：

```text
Inspector completion
→ Case creation
```

修改为：

## 19.1 One-shot Inspector

例如异步独立 Inspector Work Session：

```text
one-shot / non-dedicated Inspector
```

继续保留旧行为：

```text
Inspector terminal
→ initial Case archive
```

---

## 19.2 Dedicated reusable Inspector

新行为：

```text
每个 invocation return
→ 不 publication

每个 invocation completion
→ 不 publication

Inspector idle
→ 不 publication

owner 普通一个 turn 完成
→ 不 publication

owner 继续下一用户 turn
→ 不 publication

只有 owner Session 最终 retire
→ final synthesis
→ publication once
```

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

Dedicated Inspector 活着期间，需要一个**非权威、进程内、可丢失**的 capture resource。

示意：

```fsharp
type InspectorCallCapture =
    { Ordinal: int64
      Question: string
      Answer: string
      Observations: CapturedObservation list }

type InspectorCaseDraft =
    { OwnerSession: SessionId
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

owner exit synthesis 后的新 Case：

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

# 25. Owner exit 时到底 synthesize 什么

Finalization 输入至少包括：

```text
1. Owner opening / assignment
2. 按调用顺序排列的全部 Inspector questions
3. 每个 Inspector 实际返回 caller 的 bounded Answer
4. Owner terminal output
5. 全生命周期 flatten 后的 captured observations
6. 对应 evidence snapshot
```

注意：

```text
Owner hidden reasoning
```

不要求抓取。

知识传递仍只使用真实 transcript/tool boundary 已存在的内容。

---

# 26. 为什么要包含 Owner terminal output

原 Student/SKILL 的价值不只是 Teacher 的局部回答。

还有 Student 最后的综合。

删除 Student 后，该综合现在发生在：

```text
Meditator final answer
```

如果 exit-time synthesis 只看 Inspector Q/A：

```text
会漏掉 owner 对这些证据的最终组合关系。
```

因此 normal owner terminal output 应作为低信任 synthesis input。

Bookkeeper 可吸收其中有复用价值的部分。

但它不是 evidence。

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

# 28. 直接复用现有 Bookkeeper，不新增 Learner/Compiler Agent

不得新建：

```text
SkillCompiler
LearningCompiler
CaseSynthesizerAgent
StudentReplacement
TeacherReplacement
```

直接复用 `perm-inspector` 已经定义的私有 Bookkeeper 机制。

现 Proposal 已规定 Bookkeeper 可以修改：

```text
Q
A
```

且 subject repository 对 Inspector 仍保持只读；Bookkeeper 的 Q/A 修改只发生在 staged Case documents 中。

因此 exit synthesis 就是 Bookkeeper 的一个新入口。

---

# 29. Exit synthesis staging

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
Owner terminal output
```

明确使用结构化低信任 data container。

然后给 Bookkeeper 固定 trusted instruction：

```text
Convert this completed owner/Inspector working session into one reusable
Inspector Case.

Rewrite Q into the smallest faithful canonical inquiry that describes
the durable subject investigated.

Rewrite A into a self-contained reusable answer containing the
architecture, constraints, evidence-backed findings, important
counterexamples and operational consequences that remain useful
outside this original session.

Remove conversational scaffolding, task coordination, repeated
questions, acknowledgements and temporary progress narration.

Do not invent evidence.
Do not claim freshness.
Do not claim correctness proof.
Do not modify the subject repository.
```

---

# 30. Bookkeeper 只能执行一次 synthesis provider call

这是用户要求的核心约束：

> **可复用 Inspector 只在主人最后退出时合并处理一次。**

所以 initial dedicated finalization：

```text
exactly one Bookkeeper synthesis attempt
```

禁止：

```text
每次 Inspector return 都 Bookkeeper
每次 owner turn 都 Bookkeeper
owner idle 时 Bookkeeper
连续 retry 3 次 Bookkeeper 直到满意
```

---

# 31. “一次”不等于 publication CAS 不能 retry

需要区分：

```text
semantic synthesis
```

与：

```text
storage CAS
```

Bookkeeper：

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

Dedicated initial synthesis 同样要求：

```text
freeze final draft
→ Bookkeeper once
→ replay/verify captured observations against current worktree
```

如果 synthesis 期间 subject evidence 已改变：

```text
discard candidate
do not publish
```

**不得第二次启动 Bookkeeper。**

因为 Casebook 是 best-effort cache。

宁可本次没有 Case，也不要把一次 synthesis 偷偷变成循环运行时。

---

# 33. Dedicated synthesis 失败语义

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

# 35. Dedicated Case 的 Q.md 语义必须修改

旧规则：

```text
Q.md
= Inspector invocation 的完整 initial prompt
```

这只适用于 one-shot Inspector。

新规则：

## One-shot Inspector

```text
Q.md initial
= full invocation prompt
```

保持不变。

## Dedicated Inspector

```text
Q.md
= owner-exit Bookkeeper synthesis 得到的 current canonical inquiry
```

它不是：

```text
第一轮问题
最后一轮问题
owner prompt 原文
所有问题机械 concat
```

---

# 36. Dedicated Case 的 A.md 语义

旧规则要求：

```text
A.md
= caller 实际得到的 bounded answer
```

这是单轮 Inspector 合理语义。

Dedicated Case 改成：

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
one-shot
```

还是：

```text
dedicated synthesized
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

one-shot：

```text
I1 → Case I1
```

dedicated：

```text
I2 被 owner 使用 12 次
→ owner exit
→ Case I2
```

因此现有：

```text
session_id -- full Q
fetch(session_id)
```

模型保持成立。现 Proposal 当前就是以 Inspector SessionId 确定 Case path 与 fetch identity。

---

# 39. Dedicated Session Replacement

如果 dedicated Inspector 可证明永久丢失：

```text
owner
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
  最终 publication key = final active Inspector SessionId
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
按 Attached Session recovery 规则
```

但 Case Draft：

```text
不作为 recovery truth
```

后续重新 capture 能捕获的 evidence。

---

## 40.2 Crash 发生在 owner exit synthesis 之前

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
fetch → refresh
```

协议。

---

# 41. Owner 什么情况下算“最后退出”

不能使用：

```text
idle
```

因为 Work Session idle 后还可能继续接收用户消息。

也不能使用：

```text
一次 Assistant completion
```

因为同一个 Session 可以多轮。

正确 trigger 是：

```text
owner Session 进入最终 retire/dispose
```

也就是：

> **可以证明以后不会再向该 owner Session 发送新的业务 prompt。**

只有这个边界之后，才能开始 dedicated Case synthesis。

---

# 42. 正常退出顺序必须固定

推荐：

```text
Owner terminal output 已确定
→ 禁止新的 SyncDelegate call
→ freeze InspectorCaseDraft
→ Bookkeeper synthesis once
→ evidence stability verify
→ best-effort local publication
→ best-effort remote sync / store replication
→ retire dedicated Inspector
→ dispose draft
→ owner physical teardown 完成
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

# 44. 非正常 owner exit

第一版建议：

```text
normal proven terminal
→ synthesize

operator abort
failed
abandoned
ambiguous teardown
hard crash
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

知识 publication 完全由 Session lifecycle 自动触发。

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
→ owner exit synthesis
→ Casebook
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

# 55. Casebook feature disabled

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

保持。

Dedicated Inspector 本身：

```text
仍然工作
仍然复用 Session
仍然获得 prefix/history benefit
```

只是 owner exit：

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

# 56. Casebook enabled 时 active dedicated Session 不 publish self-case

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

M still alive
Case I absent
```

只有：

```text
M retires normally
```

之后：

```text
Case I present
```

---

# 57. Dedicated Inspector 可以正常 fetch 其它 Case

“owner exit 才合并一次”只约束：

```text
这个 dedicated Inspector 自己产生的新 Case
```

不禁止它：

```text
fetch(existingCase)
```

而 `fetch(existingCase)` 如果发现旧 evidence changed：

```text
仍可按 perm-inspector refresh 规则启动 Bookkeeper
```

因为那是在维护**旧 Case**，不是把当前 dedicated Session 每轮归档。

两个机制必须分清。

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

# 59. Casebook Index 不因 owner exit 强制刷新其它 Inspector

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
Coder C1
→ dedicated Inspector I1
→ 多轮调查
→ C1 最终完成
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
同 DevOps Session 内继续修改工作
```

它自己如果调用 Inspector：

```text
Coder C
→ dedicated Inspector I
```

则 I 在 C retire 时 synthesis。

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

type DedicatedDelegateKey =
    { Owner: SessionId
      Role: SyncDelegateRole }

type DedicatedDelegate =
    { Owner: SessionId
      Session: SessionId
      Role: SyncDelegateRole
      Agent: AgentId }

type InspectorCallCapture =
    { Ordinal: int64
      Question: string
      Answer: string
      Observations: CapturedObservation list }

type InspectorCaseDraft =
    { Owner: SessionId
      Inspector: SessionId
      OwnerOpening: string option
      Calls: ResizeArray<InspectorCallCapture>
      Evidence: ObservationAccumulator }
```

---

# 65. Suggested runtime ports

```fsharp
type ISyncDelegateRuntime =
    abstract Invoke:
        owner: SessionId *
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
        owner: SessionId *
        inspector: SessionId *
        ownerTerminal: string
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
    do! finalizeDedicatedInspectorBestEffort ()
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
Owner teardown
→ waits Casebook synthesis
→ Bookkeeper provider attempt
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
owner exits
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
owner teardown CE
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

只是 owner resource teardown 的 bounded cleanup。

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

Dedicated Inspector invocation 不得创建新 Session when existing compatible binding exists。

---

## SYNC-005

同 owner 不得同时存在两个 active sync delegate calls。

---

## SYNC-006

Dedicated Inspector 每次 invocation completion 不得调用：

```text
Casebook initial publication
```

---

## SYNC-007

Dedicated Casebook initial synthesis 唯一调用点属于：

```text
owner final retirement path
```

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

## RED-7 — No per-call archive

Casebook enabled：

```text
Q1 returned
Q2 returned
Q3 returned
owner alive
```

Case key：

```text
absent
```

---

## RED-8 — Exit synthesis exactly once

owner normal retire：

```text
Bookkeeper calls = 1
Case publication = 1
```

---

## RED-9 — No synthesis retry on evidence drift

Bookkeeper 后制造 subject drift。

断言：

```text
publication absent
Bookkeeper calls = 1
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

owner operator abort：

```text
Case absent
```

---

## RED-15 — Finalizer cannot mutate owner result

publication failure：

```text
user-visible terminal == baseline
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
Session is retired
```

验证：

```text
no Student
no Teacher
no QA
no SKILL

one Inspector Session
one exit Bookkeeper
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

需要系统性重写：

```text
Summary
Case lifecycle
Q semantics
A semantics
Inspector completion archive
Bookkeeper invocation
initial publication
tests
completion criteria
implementation order
```

明确增加：

```text
one-shot Inspector
vs
dedicated Inspector
```

二分。

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
Teacher Satellite
Teacher return
Student final return
idle → compile
```

## 提升成通用 Sync Delegate

```text
same Session reuse
Returned → Completion
single flight
replacement/fail closed
owner cascade
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
owner final retirement
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
Bookkeeper once
stability verify
publish best effort
```

先做 unit/integration。

---

## Phase 5 — perm-inspector rewrite

正式修改：

```text
one-shot lifecycle
dedicated lifecycle
Q/A canonical semantics
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
[ ] Dedicated Inspector owner-local
[ ] 同 owner sync calls 串行
[ ] Inspector return→completion 双 await
[ ] Manager async semantics 未被改变
```

---

# 92. Completion Criteria — Casebook

```text
[ ] one-shot Inspector 可按原生命周期 archive
[ ] dedicated Inspector 每轮不 archive
[ ] dedicated Inspector owner exit 才 synthesis
[ ] 一次 exit 最多一次 Bookkeeper synthesis
[ ] evidence drift 不重跑 synthesis
[ ] synthesis failure 不影响 owner terminal
[ ] final Case Q 是 canonical inquiry
[ ] final Case A 是 bounded reusable answer
[ ] observations 全生命周期 flatten
[ ] current worktree stability verify
[ ] publication atomic
[ ] active Inspector prefix 不因 publication 改写
[ ] Casebook disabled 时零附加行为
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
[ ] dedicated Inspector 可按 existing attached-session contract reuse/replacement
[ ] Case Draft crash 可丢
[ ] 无 PendingCase durable workflow
[ ] hard crash 后不扫描 closed Session 补 synthesis
[ ] publication 后 teardown crash 不重复 initial synthesis
```

---

# 95. Completion Criteria — Adversarial

必须有受控反例证明以下都会 RED：

```text
给 Meditator 加 read
重新加 Student role
per-call Inspector Session
两个 sync call 并发
return 后不等 completion
每轮 archive Case
每轮 Bookkeeper
timer flush Case
Draft 写 Journal
Case 自动注入 Meditator system
Case 决定业务 workflow
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

# 97. 旧系统与新系统的对应表

| 旧概念                            | 新概念                                      |
| ------------------------------ | ---------------------------------------- |
| Student                        | Meditator                                |
| Teacher                        | Inspector                                |
| teacher tool                   | inspector tool                           |
| Teacher persistent Session     | Dedicated Inspector Session              |
| Student↔Teacher multiple Q/A   | Meditator↔Inspector multiple sync calls  |
| QA.md                          | 删除；hot knowledge 留 transcript            |
| StudentCompile                 | 删除                                       |
| SKILL synthesis                | Owner-exit Casebook synthesis            |
| SKILL artifact                 | Inspector canonical Case                 |
| future SKILL loading           | future Inspector index + fetch           |
| Teacher Returned→Completion    | generic SyncDelegate Returned→Completion |
| Teacher Satellite lifetime     | generic attached dedicated Session       |
| Student learning state machine | 删除                                       |

---

# 98. 最终知识循环

成功后的完整知识循环应该只有：

```text
当前 Session：

Reasoner
  Meditator
      ↓ asks

Evidence specialist
  Dedicated Inspector
      ↓ investigates

Repository / runtime evidence
      ↓

Inspector returns
      ↓

Meditator synthesizes
      ↓

User result
```

Session 结束：

```text
Owner final knowledge
+
Inspector Q/A
+
captured evidence
      ↓
Bookkeeper once
      ↓
Canonical Inspector Case
```

下一 Session：

```text
New Inspector
      ↓
Casebook index
      ↓
fetch relevant Case
      ↓
freshness replay
      ↓
reuse / investigate further
```

---

# 99. 最终架构分工

```text
Meditator
负责：
  思考

Inspector
负责：
  证据

Dedicated Session
负责：
  当前 Session 内的长期上下文复用

PrefixEpoch
负责：
  provider prefix/cache 稳定

Case Draft
负责：
  当前 dedicated Inspector 生命周期的 best-effort capture

Bookkeeper
负责：
  owner 退出时一次性知识压缩

Casebook
负责：
  跨 Session reusable knowledge cache

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
每次 inspector() 创建新 Session
允许同 owner 并发 sync delegate
Inspector 正文直接当 return
return 后不等待 completion
每轮 Inspector answer archive Case
每轮增量 Bookkeeper
定时 Case flush
按 token/context size 提前 compile
Case Draft 写 Journal
Case Draft 用于 recovery
Case Draft 用于业务 branch
owner idle 被当最终退出
一次 Assistant completion 被当 Session exit
Casebook failure 改变 owner terminal
Case 自动注入 Meditator trusted prompt
Casebook 成为 correctness proof
Casebook 成为 PromptAuthority
为 Casebook 再建第二 persistence system
为了 storage migration 继续实现 Student QA
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
Dedicated Inspector
+
Inspector transcript
+
Exit-time Casebook synthesis
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
Dedicated synchronous specialist Session
+
Inspector Casebook
```

因此最终原则应写成：

> **Reasoning lives in Meditator; evidence lives in Inspector.**

> **Hot knowledge lives in the dedicated Inspector session; cold reusable knowledge is synthesized once, when its owner retires.**

> **Learning is no longer a special Agent program. It is ordinary reasoning plus persistent evidence reuse.**

做到这里，Student/Teacher 才算真正被“吸收”进系统，而不是换名字继续活着。
