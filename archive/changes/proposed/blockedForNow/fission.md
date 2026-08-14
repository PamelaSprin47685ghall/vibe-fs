# Proposal：Agent Fission + Role/System/Tool Guidance Refresh

**Status:** Proposed（由用户明确要求创建；尚未 Active，禁止实现）  
**Priority:** P1 / latency + agent autonomy + prompt effectiveness  
**Scope:** Agent execution / fork-join runtime / lifecycle work record / PromptAuthority / role system prompts / tool-definition descriptions / tests / docs  
**Compatibility:** Additive capability；不改变既有 `fork-agent` / `fork-manager` / `join` / SyncDelegate / Reviewer / Finality 的既有业务语义；不复活 Student/Teacher；不引入第二套 persistence / child registry / prompt authority  
**Proposed file:** `changes/proposed/fission.md`  
**Reference inspiration (non-normative):** 用户提供的另一个项目 `multi-agent-teams` SKILL。该文档只作为设计素材；本 Proposal 必须把采用的原则完整重述到万象术正式 docs，生产代码、prompt、runtime 均不得依赖该 SKILL 文件存在。

---

# 0. Executive Decision

本 Change 同时完成两个互相配套的升级：

1. 新增 **`fission(prompts: String)`**：允许一个逻辑 Agent 在不改变对 parent 可见身份的前提下，把自身当前执行横向裂变为 N 个对等 execution lanes。
2. 系统性升级现有各角色的 **system prompt + tool-definition prompt**，把“连续流调度、主动暴露并行、明确 ownership、及时 handoff、不要 wave barrier、不要把 join 当全员 barrier、复用已有上下文、完成不等于正确”等可迁移原则写进万象术自己的角色/工具指导，同时严格服从现有 Role/Prompt/Tool SSOT。

一句话产品定义：

> **Fission is transparent intra-agent parallelism: one logical agent temporarily executes as N coequal lanes, then converges back into one normal completion without exposing the split to its parent.**

中文：

> **Fission 是 Agent 内透明裂变：同一个逻辑 Agent 暂时展开成 N 个对等执行 lane，并在内部收敛回一次普通完成；parent 不感知这次裂变。**

这不是新的层级 fork，不是新 Agent，不是 Replica，不是 SyncDelegate，不是 manager-visible child，也不是 speculative Strength。

---

# 1. 用户已经冻结的产品裁决

以下是本 Proposal 的上位输入，实现者不得自行重裁决。

## 1.1 名称

公开工具名：

```text
fission
```

正式术语：

```text
Fission
Fission Group
Fission Lane
Lane Index
Lane Count
```

禁止把正式实现继续叫：

```text
twin-fork
twin
peer-fork
clone-fork
split-agent
```

`Peer` 已有 fast/deep 同角色配对语义；`Replica` 已被 Strength 使用；`fork` 已表示层级 fork。

## 1.2 Unix-fork-like 调用形状

模型调用一次：

```text
fission("""
A
B
C
""")
```

逻辑上该 tool call 返回三次：

```text
lane 0 receives A
lane 1 receives B
lane 2 receives C
```

三条 lane 都从调用 `fission` 的同一语义点继续执行。

它们不是 parent/child：

```text
lane0 == lane1 == lane2     // coequal
```

## 1.3 同一逻辑 Agent 身份

所有 lane：

```text
same logical AgentId
same CanonicalRole
same AgentTier
same SelectedAgent / EffectiveAgent semantics
same authority
same parent relation
same child set
same worktree / external resource surface
```

parent 不获得新的 agent handle，不看到 lane id，不需要 join fission lanes。

## 1.4 角色 allowlist

第一版 **允许 fission**：

```text
Manager
Coder
Inspector
Browser
Meditator
```

第一版 **明确禁止 fission**：

```text
Orchestrator
DevOps
Reviewer
Blogger
Executor
```

其中用户明确裁决：

```text
DevOps      先不裂
Reviewer    不裂
Orchestrator 不裂
```

fast/deep 不得分叉能力：

```text
fissionAllowed(fast-ROLE) = fissionAllowed(deep-ROLE)
```

## 1.5 Y 工作记录环式接管

若 N 条 lane 的 index 为：

```text
0 .. N-1
```

lane `k` 结束时，它的 canonical Y 工作记录向逻辑 successor：

```text
(k + 1) mod N
```

交接。

最后整个 Fission Group 收敛时，像原 agent 正常结束一样，parent 只收到一次最终完成。

## 1.6 Parent 透明

典型例子：

```text
Manager
  |
  | assign "implement A, B, C"
  v
Coder X
  |
  | fission("A\nB\nC")
  |
  +-- X lane 0 -> A
  +-- X lane 1 -> B
  +-- X lane 2 -> C
          |
       converge
          |
          v
       Coder X
          |
          | one ordinary completion
          v
       Manager
```

Manager 不知道 X 曾经 fission。

---

# 2. 为什么这不是现有 `fork-agent`

现有层级 fork 的语义：

```text
Manager
  ├─ Coder A
  ├─ Inspector B
  └─ Browser C
```

它改变组织拓扑：

```text
new child identity
new handle
parent-visible completion
join/list participation
```

Fission：

```text
Coder X
  ├─ lane 0
  ├─ lane 1
  └─ lane 2
```

不改变逻辑组织拓扑：

```text
no new public AgentId
no new parent-visible handle
no new manager-visible child
no parent join obligation
```

因此：

```text
fork-agent / fork-manager = inter-agent hierarchical parallelism
fission                   = intra-agent coequal parallelism
```

二者必须长期正交。

---

# 3. 为什么现在需要 Fission

当前系统已经鼓励 Manager 细粒度异步并发，但一个被分配到大任务的单个 worker 仍可能形成局部串行瓶颈。

典型：

```text
Manager -> Coder X:
  implement feature A
  implement feature B
  implement feature C
```

如果 A/B/C 在真实 ownership 上独立，X 仍只能：

```text
A -> B -> C
```

Manager 若为了加速只能预先知道内部结构并改成三次 fork：

```text
Manager -> Coder A
Manager -> Coder B
Manager -> Coder C
```

这要求上级了解下级内部拆分边界，破坏局部自治。

Fission 允许：

```text
Manager assigns logical outcome
Coder decides physical parallelism
```

也就是：

> **上级拥有“要什么”，当前 Agent 拥有“自己的工作是否值得横向展开”。**

这与另一个项目 SKILL 中“持续暴露并行、慢节点可重新分区、不要等待 wave barrier”的思想一致，但万象术采用的是更底层、更透明的 primitive。

---

# 4. 目标

本 Change 必须达到：

```text
G1  slow broad Agent can self-parallelize
G2  parent-facing identity remains one
G3  no hierarchical child is invented for each lane
G4  all lanes preserve exact same role/authority/tool permissions
G5  children remain logical-agent-owned and shared
G6  lane completion work is never lost
G7  arbitrary lane completion order converges
G8  final parent completion occurs exactly once
G9  crash/restart is deterministic or fails closed
G10 no second persistence owner
G11 no second child registry
G12 no wave barrier guidance
G13 system prompts teach role/responsibility/collaboration, not stale capability matrices
G14 tool descriptions teach the exact operational affordance available on this attempt
G15 prompt guidance materially helps all current roles, not only fission-capable roles
```

---

# 5. 非目标

本 Change **不做**：

```text
- Orchestrator fission
- DevOps fission
- Reviewer fission
- Blogger / Executor fission
- recursive/nested fission V1
- automatic Host-side semantic task decomposition
- automatic file ownership inference
- auto merge of conflicting edits
- new git worktree per lane
- new Agent role
- new Agent catalog entry
- new fast/deep model selection rule
- StrengthReplica replacement
- fork-agent/fork-manager replacement
- SyncDelegate replacement
- Reviewer cohort/finality redesign
- a new Y/LWR renderer
- a feature-owned journal/database/blob store
- runtime dependency on external SKILL files
```

特别禁止：

> 不得因为 fission 看起来像“内部 subagent”就给每条 lane 创建公开 AgentId，然后在 parent 面隐藏它。那只是把层级 fork 伪装起来，不是本 Proposal。

---

# 6. 核心术语与身份

必须分开三种 identity。

## 6.1 Logical Agent Identity

对 parent / role / tool permission / child ownership 有意义：

```fsharp
type LogicalAgentId = AgentId
```

Fission 前后不变。

## 6.2 Fission Group Identity

Host 内部一次 fission occasion 的稳定 identity。

示意：

```fsharp
type FissionGroupId = private FissionGroupId of string
```

它必须由可 durable 重放的事实确定，不得由 clock/RNG 在 replay 时重新猜。

建议 identity ingredients：

```text
Owner LogicalAgentId
Owner Session / ReuseScope identity
origin ToolCallId
origin ProviderRun identity
```

具体 hash/codec 归既有 identity owner。

`FissionGroupId` **不得进入 parent-visible agent catalog / list / fork handle**。

## 6.3 Fission Lane Identity

Host 内部：

```fsharp
type FissionLaneIndex = private FissionLaneIndex of int
```

lane key：

```fsharp
type FissionLaneKey =
    { Group: FissionGroupId
      Index: FissionLaneIndex }
```

它不是 AgentId。

---

# 7. 逻辑身份与物理 Session 必须分开

这是本 Change 最重要的实现约束之一。

现有执行模型明确保护：

```text
每个 provider attempt / session 恰一个单线程序列泵
```

因此禁止：

```text
same physical SessionId
+ 3 simultaneous provider streams
+ shared mutable transcript pump
```

这种实现会直接破坏现有 mailbox / attempt / prefix / host hook 的单线假设。

正确原则：

> **same logical AgentId does not imply same physical lane transport/session identity.**

第一版允许 Host 为每条 Fission Lane 建立内部 physical execution context / hidden lane session，只要同时满足：

```text
- 这些 lane 不是新的 logical Agent
- Role/Tier/authority 均从 owner attempt 投影
- parent 仍只看到 owner AgentId
- child registry owner 仍是 logical Agent owner
- lane runtime 不建立第二份 child map
- physical lane id 不泄漏到模型可调用 agent handle
```

正式 docs 必须明确这一层，否则实现者很容易为了“same id”强行让同一个 Session 并发 provider turn。

---

# 8. `fission` Tool Surface

## 8.1 Schema

V1：

```text
fission(prompts: String)
```

只有一个参数。

禁止 V1 额外暴露：

```text
agent
role
tier
model
permissions
parent
worktree
children
lane_count
strategy
merge_policy
```

这些都不由模型选择。

## 8.2 Prompt parser

输入先做唯一 canonical parser：

```text
1. CRLF / CR normalize to LF
2. remove at most one final LF for ergonomic triple-string calls
3. split by LF
4. require N >= 2
5. every line must contain at least one non-whitespace character
6. preserve each non-empty line's text exactly after newline normalization
```

禁止：

```text
trim every line
collapse spaces
markdown bullet parsing
JSON parsing
guess numbered-list structure
split on semicolon
```

一行就是一个 lane prompt。

例：

```text
fission("A\nB\nC")
```

等价于：

```text
N = 3
lane[0].prompt = "A"
lane[1].prompt = "B"
lane[2].prompt = "C"
```

## 8.3 Capacity

Fission 消耗真实并发资源，必须进入现有全局/Host concurrency budget。

要求：

```text
all-or-none admission
```

如果当前 runtime 无法一次容纳请求的 N 条 lane：

```text
reject whole fission
```

禁止：

```text
requested 5
→ silently spawn 3
```

也禁止先创建一半再因 capacity 失败留下半组。

具体 cap 数值不得在 Domain 复制一份常量；由现有 concurrency owner 提供。

## 8.4 One call, N tool results

origin tool call 只执行一次 fission admission。

每条 lane 看到属于自己的 tool result：

```text
status = "ok"
lane_index = k
lane_count = N
prompt = "<the exact prompt line>"
```

可使用 Synthetic TOML/既有 canonical tool-result renderer；不得手拼第二种 runtime synthetic dialect。

不要求暴露 `FissionGroupId`。

模型只需要通过：

```text
lane_index
lane_count
prompt
```

知道自己是谁、负责什么。

---

# 9. Provider Prefix 语义

所有 lane 从同一个 fission call point 裂开。

概念上：

```text
CommonPrefix
ToolCall(fission)
```

之后：

```text
lane0:
CommonPrefix
ToolCall(fission)
ToolResult(index=0,prompt=A)
...

lane1:
CommonPrefix
ToolCall(fission)
ToolResult(index=1,prompt=B)
...

lane2:
CommonPrefix
ToolCall(fission)
ToolResult(index=2,prompt=C)
...
```

要求：

```text
- common prefix byte identity preserved
- no historical message reordering
- no rewriting owner history to simulate lanes
- lane-specific result only appears after shared call
```

每条 lane 自身后续仍服从现有 PrefixEpoch / HOST-013 / projection rules。

---

# 10. Domain Model：不要用 bool soup

建议建立纯 Domain 表达。

```fsharp
[<RequireQualifiedAccess>]
type FissionRoleEligibility =
    | Allowed
    | Forbidden

type FissionSpec =
    {
        GroupId: FissionGroupId
        OwnerAgentId: AgentId
        OriginToolCallId: ToolCallId
        LanePrompts: NonEmptyList<string>
    }

[<RequireQualifiedAccess>]
type LaneDisposition =
    | Running
    | Settling
    | Closed

type LaneState =
    {
        Index: FissionLaneIndex
        Disposition: LaneDisposition
        Inbox: FissionWorkBundle
        OwnWork: WorkRecordRef option
    }

type FissionWorkBundle =
    private
    | FissionWorkBundle of Map<FissionLaneIndex, WorkRecordRef>

[<RequireQualifiedAccess>]
type FissionDecision =
    | Reject of FissionRejectReason
    | Admit of FissionSpec
```

Reject reason 至少穷尽：

```fsharp
type FissionRejectReason =
    | RoleNotEligible
    | AlreadyFissioned
    | TooFewLanes
    | EmptyLanePrompt of index:int
    | CapacityExceeded
    | InvalidOrigin
    | RuntimeUnavailable
```

不要：

```fsharp
isFissioned: bool
isFinalLane: bool
hasHandoff: bool
shouldReturnParent: bool
```

四五个 bool 组合会制造大量非法状态。

---

# 11. V1 禁止 nested fission

第一版固定：

```text
one logical Agent may have at most one active Fission Group
```

任一 active lane 再调用：

```text
fission(...)
```

返回：

```text
FissionRejectReason.AlreadyFissioned
```

理由：

```text
- 防指数级 lane 爆炸
- child completion affinity 简化
- recovery 简化
- Y ring 唯一
- parent completion owner 唯一
- 第一版性能收益已经足够
```

未来若有数据证明 recursive fission 值得做，另开 Proposal。

不要偷偷允许：

```text
lane 0 -> fission 8
lane 1 -> fission 8
...
```

---

# 12. Role Eligibility 单一 owner

正式 allowlist：

```fsharp
let canFission role =
    match role with
    | Role.Manager
    | Role.Coder
    | Role.Inspector
    | Role.Browser
    | Role.Meditator -> true

    | Role.Orchestrator
    | Role.DevOps
    | Role.Reviewer
    | Role.Blogger
    | Role.Executor -> false
```

这份 policy 必须只有一个 production owner，然后同时投影：

```text
provider-visible tool schema
runtime execution gate
prompt/tool description generation
tests
```

禁止：

```text
system prompt says fission
but schema hides it

schema exposes fission
but runtime role gate rejects it unexpectedly

fast-coder has it
deep-coder does not
```

---

# 13. 为什么每个允许角色都合理

## 13.1 Manager

典型：

```text
mission has independent product lanes A/B/C
```

Manager 可：

```text
fission(
  manage A
  manage B
  manage C
)
```

每条 lane 可以继续使用同一 Manager 身份拥有的 shared children，执行：

```text
fork-agent
join
list
```

parent Orchestrator 仍只看到一个 Manager completion。

Manager fission 的价值是解除：

```text
one Manager serially scheduling several independent subgraphs
```

而不是让 Orchestrator 复制 control plane。

## 13.2 Coder

典型：

```text
implement adapter A
implement adapter B
implement adapter C
```

适合：

```text
disjoint ownership
independent modules
independent tests
independent migrations that do not mutate same target
```

不适合：

```text
three lanes blindly edit same central state machine
one lane changes interface while another assumes old interface
same mutable file/resource with no stable seam
```

Coder fission 是本功能的标杆 use case。

## 13.3 Inspector

最自然的安全 use case：

```text
inspect implementation
inspect tests
inspect architecture evidence
```

只读 evidence acquisition 很适合横向展开。

## 13.4 Browser

适合：

```text
official docs
upstream issues/changelog
independent source families
different hypotheses/search queries
```

主要收益是隐藏网络 latency。

## 13.5 Meditator

适合做多假设、多方案 reasoning：

```text
hypothesis A
hypothesis B
adversarial counterexample
```

然后通过 Y handoff 收敛。

必须继续遵守：

```text
Meditator = reasoning
Inspector = evidence acquisition
```

Meditator lane 需要事实时仍走 Inspector，不因为 fission 给自己恢复 read/glob/grep。

---

# 14. 为什么禁止的角色不裂

## 14.1 Orchestrator

Orchestrator 是 mission-level control-plane authority。

多个同身份 Orchestrator lane 同时：

```text
repartition managers
change goal ownership
decide critical path
integrate mission truth
```

会复制 control plane，而不是加速 worker。

V1 明确无 `fission`。

## 14.2 DevOps

用户明确第一版先不裂。

原因无需升级为永久理论；只冻结 V1：

```text
PTY / process / external mutable target
```

并行副作用冲突面复杂，未来另开 Change。

## 14.3 Reviewer

Reviewer 持单一 causal verdict authority。

若裂：

```text
same ReviewerId
lane0 -> PERFECT
lane1 -> REVISE
```

会污染 verdict identity / seal / witness。

因此无 `fission`。

## 14.4 Blogger

内部记录生产者；没有用户级横向工作面。无 `fission`。

## 14.5 Executor

内部执行叶；无工具。无 `fission`。

---

# 15. Shared Children：身份共享，不得复制 child registry

用户明确：

```text
children shared
```

正式解释：

> **Child ownership belongs to the logical Agent, not to an individual Fission Lane.**

因此：

```text
lane0 fork child C
lane1 list
→ may observe C

lane2 fork(existing C, ...)
→ operates on same logical child identity, subject to existing compatibility/busy rules
```

禁止实现：

```text
lane0Children
lane1Children
lane2Children
```

各建一套 registry。

Host children recovery、abort、retire 继续只有既有 owner。

---

# 16. Shared Children 下必须新增 completion lane-affinity

仅共享 child set 还不够。

如果现有 `join` 仍让任意 lane drain logical owner 的所有 completion：

```text
lane0 waits child A
lane1 calls join
→ accidentally consumes child A result
```

则 lane0 永久等不到自己需要的结果。

因此必须区分：

```text
child identity ownership = logical Agent shared
child active-run completion affinity = initiating Fission Lane
```

概念：

```fsharp
type ChildCompletionAffinity =
    | OrdinaryOwner
    | FissionLane of FissionLaneKey
```

当某 lane：

```text
fork-agent(new child)
fork-agent(existing child, nudge)
```

真正创建/拥有该 active Run 的 operation 必须绑定 current lane affinity。

对已有 busy child 的 nudge：

```text
不创建新 RunId
不偷换 active run affinity
```

继续遵守现有 EXEC-002。

`list` 可见 shared children。

`join` 在 fission lane 中只 drain：

```text
completion affinity == this lane
```

lane 关闭时，它尚未消费的 completion affinity 与未决 child wait ownership 一起交给 successor lane。

这样：

```text
children are shared
results are not stolen
```

---

# 17. Y Handoff：不要直接拼字符串

用户要求 lane `k` 的 Y 交给：

```text
(k + 1) mod N
```

错误实现：

```text
string accumulatedY += laneY
```

会导致：

```text
double inclusion
nested records
arrival-order nondeterminism
restart duplication
```

正确做法是 typed bundle：

```fsharp
type FissionWorkBundle =
    private
    | FissionWorkBundle of Map<FissionLaneIndex, WorkRecordRef>
```

每条 lane 的 canonical record 只出现一次：

```text
index -> WorkRecordRef
```

merge：

```text
same index absent
→ insert

same index same digest/ref
→ idempotent

same index different digest/ref
→ fail closed
```

最终渲染可按：

```text
lane index ascending
```

得到稳定顺序。

运输是 ring，集合语义是 keyed union。

---

# 18. Ring Handoff 必须处理任意 completion order

直觉规则：

```text
successor(k) = (k + 1) mod N
```

但 successor 可能已经提前结束。

例：

```text
lane2 closes
→ sends bundle to lane0

lane0 closes
→ sends {0,2} to lane1

later lane1 closes
→ parent gets {0,1,2}
```

另一个顺序：

```text
lane1 closes first
→ successor lane2 receives {1}

lane2 closes next
→ successor lane0 receives {1,2}

lane0 closes last
→ parent gets {0,1,2}
```

最麻烦：

```text
lane2 already closed
lane1 closes later
```

不能把 `{1}` 丢给不存在的 runtime。

必须有 durable/logical forwarding closure：

```text
handoff to successor
if successor Running/Settling:
    enqueue bundle
else successor Closed:
    mechanically merge into successor's outgoing bundle
    forward to successor(successor)
repeat
```

直到：

```text
active lane receives
or
all lanes closed -> group finalizer owns bundle
```

禁止 timer/polling。

---

# 19. Lane inbox 与 terminal race

Y 可能在某 lane provider attempt 正运行时到达。

不能把文本硬塞进正在发送/streaming 的 provider request。

正确模型：

```text
handoff arrival
→ append/durable lane inbox fact
→ next safe provider boundary materializes it
```

若当前 attempt 恰好完成：

```text
attempt completion
+ inbox non-empty
→ lane is not yet final-closed
→ materialize inbox
→ continue lane so it can consume predecessor work
```

因此需要区分：

```text
provider turn completed
≠
fission lane closed
```

建议 CE：

```fsharp
let rec settleLane lane =
    task {
        let! outcome = awaitCurrentTurn lane

        let! inbox = readInbox lane

        if not inbox.IsEmpty then
            do! materializeHandoffAndContinue lane inbox
            return! settleLane lane
        else
            return! closeLane lane outcome
    }
```

不要把它实现成 persisted `LaneStage = WaitingForHandoff` program counter。

Domain facts + event-driven reconciliation 足够。

---

# 20. Final Convergence

只有当：

```text
all N lane own records exist
all ring handoffs are accounted for
no lane-affined completion is orphaned
exactly one final lane completion is selected
```

才允许 Fission Group 对 parent 产生一次 ordinary completion。

parent-facing invariant：

```text
Fission parent completion count == 1
```

禁止：

```text
parent receives A completion
parent receives B completion
parent receives C completion
```

也禁止：

```text
parent must call join to collect fission
```

Fission 是 owner execution 内部协议。

---

# 21. Final parent record

必须复用既有 canonical Lifecycle Work Record (LWR) machinery。

禁止新增：

```text
FissionSummaryRenderer
TwinWorkLog
LaneTranscriptSummary
```

作为第二记录来源。

每 lane own record 必须来源于 canonical LWR / Y machinery：

```text
includeOpening=false
raw tool excluded
Y-backed body + allowed RawGap/terminal according to existing contract
```

Fission 只拥有：

```text
bundle membership
lane index
handoff/convergence
```

不拥有“如何总结工作记录”。

最终 parent-facing materialization必须组合既有 `WorkRecordRef`，而不是从 raw transcript重新摘要。

如果需要一个 parent-facing aggregate wrapper，应扩展既有 work-record materialization/render surface，以“多个 canonical record refs 的确定性容器”表达；不得复制 Y parser。

---

# 22. Fission lane 的 final prose

非最终 lane 的 final prose：

```text
属于该 lane canonical work record
```

不单独返回 parent。

最后收敛 lane 的 final prose：

```text
成为 logical Agent 的 ordinary final output
```

因为它已经通过 inbox/continue 机制消费可用 predecessor bundles，所以应有机会综合整个 fission 的结果。

若基础设施无法证明最后 lane 已消费所有 required handoff：

```text
fail closed / continue convergence
```

不得为了赶快完成而静默返回只覆盖一个 slice 的 final prose。

---

# 23. Fission 与 shared worktree

Fission 不创建 per-lane git worktree。

所有 lane 保持同 logical Agent 的原外部工作面。

对 Coder：

```text
same worktree
```

因此 fission 是 shared-mutable-surface 并行，不是 merge-based branch parallelism。

system/tool guidance 必须明确：

> 只对真正 separable 的 mutable work 使用 fission。

建议固定短语：

```text
Fission separable work, not merely plentiful work.
```

Coder 应优先选择：

```text
disjoint files
disjoint modules
stable interface seams
independent tests/fixtures
read-only investigation
```

避免：

```text
same fragile file
same central state machine
interface producer and consumer without frozen contract
same migration target
```

Host 不需要做“理解代码后决定是否可并行”的智能 gate；这是 Agent 的职责。

但已有 edit/write/transaction conflict safety 必须照常生效，fission 不得绕过。

---

# 24. Cancellation / abort

Logical owner 被取消：

```text
abort all active fission lane executions
abort/cleanup according to existing logical owner child rules
close group without parent success completion
```

单 lane infrastructure failure：

```text
record failed lane terminal evidence
handoff its canonical work/failure record if available
group does NOT silently pretend success
```

V1 建议：

```text
any unrecoverable lane failure
→ Fission Group failed
```

但其它 lane 可先完成清理/record convergence；最终只向 parent 返回一个 ordinary failed completion。

operator abort：

```text
acts on logical current Agent execution
→ cancels group
```

不要暴露“请分别 abort lane 0/1/2”的 public API。

---

# 25. User message ingress

Fission 不建立新的 user-visible agent conversation。

对 owner 的新用户消息继续属于原 logical Agent / ReuseScope 的既有 ingress 规则。

V1 最简单安全策略：

```text
active Fission Group
+ external new authority/input
→ queue under existing owner semantics
→ do not arbitrarily inject into only one lane
```

何时让新输入进入 active group 必须服从现有 Authority/Prompt 文档；本 Change 不发明第二条 authority routing。

如果现有语义要求等待当前 work turn，则等待。

---

# 26. Recovery / durability

Fission 不是“纯性能优化所以 crash 可以随便丢”。

一旦模型已经：

```text
fission(A,B,C)
```

并且 A/B/C 产生了真实副作用或 child runs，restart 后不能把 group 当作从未发生。

所有需要跨 crash 保持因果正确性的事实只能进统一 EventStore / 既有 persistence owner。

建议最小 event family（命名可按现有 Event 类型风格调整）：

```text
FissionAdmitted
FissionLaneClosed
FissionBundleForwarded
FissionConverged
FissionFailed
```

其中 event payload 只存：

```text
stable identities
lane index/count
prompt digest / canonical prompt payload ref if needed
WorkRecordRef
successor relation derivable from N
child completion affinity facts if existing Run facts cannot derive
```

禁止：

```text
fission-state.json
fission.db
lane-manifest/
runtime-path blob as truth
```

Prompt 大 material / LWR 仍用现有 payload_refs / blob owner。

---

# 27. Restart fold

纯 fold 至少能回答：

```text
is there an open Fission Group for owner?
which lanes exist?
which lanes are closed?
which WorkRecordRef belongs to each lane?
which bundle keys have been forwarded?
has parent completion already converged?
```

recovery：

```text
read EventStore projection
→ reconcile physical lane sessions/runs
→ recover active lanes
→ re-establish completion affinity
→ replay pending handoff
→ converge if all closed
```

不允许：

```text
scan current raw sessions
→ guess which three were probably twins
```

无法证明 lane identity / ownership：

```text
fail closed
```

---

# 28. Exactly-once invariants

必须机械证明：

```text
FISSION LAW 1
one origin ToolCallId -> at most one FissionAdmitted

FISSION LAW 2
one admitted lane index -> at most one canonical own WorkRecordRef

FISSION LAW 3
FissionWorkBundle has at most one record per lane index

FISSION LAW 4
one FissionGroup -> at most one parent-facing terminal completion

FISSION LAW 5
Converged -> bundle keys == {0..N-1}

FISSION LAW 6
parent-visible AgentId before == after

FISSION LAW 7
role/tier/tool capability identical across lanes

FISSION LAW 8
no lane may create a second active Fission Group in V1
```

---

# 29. Prompt Refresh：总体原则

本 Change 第二部分不是“给 fission 写两句介绍”。

要借鉴 `multi-agent-teams` SKILL 的真正优点，把万象术当前角色 prompt 从“工具清单 + 短职责”升级为：

```text
mission / role truth
ownership
continuous-flow execution
parallelism discovery
handoff discipline
reuse
completion discipline
role-specific anti-patterns
```

但必须遵守当前 repo 已冻结的 DRY：

```text
System prompt:
  who you are
  what you own
  how you collaborate

Tool definition:
  what this attempt can invoke
  exact operational semantics
  when the tool is useful
  dangerous misuse patterns
```

不得把 filesystem capability matrix 复制进 system prompt。

---

# 30. 从参考 SKILL 采用的原则

以下思想被采用，但要改写成万象术语，不保留外部项目名/工具名。

## 30.1 Continuous flow, not waves

采用：

> 可运行工作一旦 ready 就启动，不要人为形成 “batch 1 全等完再 batch 2”。

映射到万象术：

```text
Manager:
  fork ready independent work immediately
  fission itself when its own independent management lanes are the bottleneck

Coder/Inspector/Browser/Meditator:
  fission independent internal slices without waiting for unrelated slice completion
```

## 30.2 Live dependency graph

不是要求每个角色维护巨大 DAG 文件，而是 prompt discipline：

```text
distinguish hard dependency from merely related work
do not block independent work
freeze small contract when it unlocks parallel work
```

Manager 最强，Coder 次之。

## 30.3 Refill before bookkeeping

在 Manager tool guidance 中强化：

```text
completion is a scheduling event
after collecting a result, immediately expose newly-ready work before idling
```

## 30.4 Expose parallelism before accepting idleness

采用：

```text
split broad work by real ownership seam
publish interface early
pull forward read-only investigation
repartition unstarted remainder
reuse compatible context
```

现在多一项：

```text
self-fission when the bottleneck is inside the current agent
```

## 30.5 No filler parallelism

采用：

```text
do not manufacture duplicate workers/lanes just to hit a concurrency number
```

## 30.6 Completion is not correctness

所有角色按职责改写：

```text
Coder completion -> implementation claim, not validation proof
Inspector completion -> evidence, not mission truth
Manager child completion -> result to inspect/integrate
Reviewer verdict -> only role with its formal verdict authority
```

## 30.7 Event wait, not polling

`join/list` tool def 强化：

```text
join is waiting/collection, not a wave barrier
list is deliberate reconciliation, not a polling loop
```

---

# 31. System Prompt DRY 不得回退

当前 repo 已明确：

```text
system prompt:
  你是谁
  你的职责
  你的协作方式

generated tool description:
  当前 attempt 可编程调用什么
```

本 Change 必须保持。

因此每个 role prompt 的刷新重点是：

```text
Role Mission
Ownership
Execution Style
Coordination
Completion Condition
Role-specific Anti-patterns
```

禁止写：

```text
You have read, glob, grep...
```

这类 capability inventory 应由 tool surface 决定。

---

# 32. Orchestrator system prompt refresh

Orchestrator **无 fission**。

必须强化：

```text
- You own mission-level decomposition, Manager-job boundaries, global integration and closure.
- Treat work as a live dependency graph, not fixed waves.
- Start newly-ready Manager work as soon as its hard dependencies are satisfied.
- Reuse compatible Manager context/job when the existing fork API supports it.
- Do not wait for unrelated Manager jobs merely because they were launched together.
- Do not duplicate a Manager lane only to increase concurrency.
- A Manager completion is input to integration, not automatic proof the mission is complete.
- Keep the control plane singular: do not emulate fission by opening duplicate Managers for the same ownership merely to have multiple "you"s.
```

保留既有 hidden review/finality 不泄漏要求。

---

# 33. Manager system prompt refresh

Manager 是 fission-capable。

必须强化：

```text
- You own a bounded delivery mission and its live work graph.
- Keep useful independent work flowing continuously; do not schedule in artificial waves.
- Use fork-agent for hierarchical delegation to specialists.
- Use fission when the bottleneck is your own execution and the mission contains multiple separable management lanes that should remain one Manager identity.
- Prefer reuse of a compatible existing child before reopening duplicate context, without sacrificing real parallelism.
- On completion/handoff, update readiness and immediately start newly-unblocked work.
- Treat child completion as evidence/result requiring inspection and integration.
- Join only when no more useful ready work can be started in this lane.
- Do not manufacture duplicate work to fill slots.
- Do not expose hidden Reviewer/finality mechanics.
```

Manager fission example必须进入 prompt或 tool example：

```text
fission("""
own auth delivery lane and coordinate needed agents
own billing delivery lane and coordinate needed agents
own notification delivery lane and coordinate needed agents
""")
```

---

# 34. Coder system prompt refresh

Coder 是 fission-capable。

必须强化：

```text
- You own implementation quality for the assigned scope.
- Identify real ownership seams before parallelizing.
- Use fission for separable implementation/investigation/test/documentation slices that can safely share the same worktree.
- Do not fission multiple blind writers over the same fragile code.
- If one slice defines an interface needed by others, freeze/publish the smallest contract before parallel work.
- Keep changes coherent with the requested observable outcome.
- Use Inspector for evidence questions according to the existing SyncDelegate contract.
- A tool command succeeding is not proof the implementation is correct; inspect results and reconcile interactions.
- Before final completion, account for all fission handoffs and integrated effects.
```

不要在 system prompt 抄 filesystem methods。

---

# 35. Inspector system prompt refresh

Inspector 是 fission-capable。

必须强化：

```text
- You own evidence acquisition, not mutation and not final mission authority.
- Split broad investigations by evidence surface, subsystem, hypothesis, or source.
- Fission independent read-only investigations aggressively when it reduces latency.
- Report concrete evidence, paths/symbols/observations, uncertainty, and implications.
- Send/return useful stable findings as soon as they can unblock the caller; do not withhold all evidence until a giant monolithic conclusion if the protocol permits incremental continuation.
- Do not duplicate the same scan across lanes without a reason.
- Reconcile contradictory evidence before final completion.
```

---

# 36. Browser system prompt refresh

Browser 是 fission-capable。

必须强化：

```text
- You own external/web evidence acquisition for the assigned question.
- Fission independent source families, query hypotheses, official-vs-upstream checks, or long-latency searches.
- Prefer authoritative/primary sources where the task requires factual or technical certainty.
- Distinguish source evidence from inference.
- Reconcile disagreement rather than selecting the first convenient result.
- Do not fission near-duplicate searches solely to appear parallel.
```

具体网络工具名称仍由 tool descriptions 提供。

---

# 37. Meditator system prompt refresh

Meditator 是 fission-capable。

必须继续保持正式边界：

```text
Meditator = reasoning
Inspector = evidence acquisition
```

增强：

```text
- Form a current understanding before converging.
- Actively search for counterexamples and disconfirming explanations.
- Use fission for genuinely distinct reasoning branches: competing hypotheses, alternative designs, adversarial critique, or independent derivations.
- Delegate factual uncertainty to Inspector rather than silently turning yourself into an evidence scanner.
- Distinguish evidence, inference, assumptions, and uncertainty.
- Converge the branches into one coherent answer; do not return a bag of incompatible mini-answers.
```

不要给 Meditator 恢复 read/glob/grep。

---

# 38. DevOps system prompt refresh

DevOps **V1 无 fission**。

仍可借鉴 continuous-flow 原则，但不能提示 fission：

```text
- You own operational/process-oriented execution within your existing tool boundaries.
- Use existing PTY/executor/coder/inspector delegation surfaces according to their contracts.
- Distinguish independent observation from conflicting mutation of the same process/service/resource.
- Reuse compatible child context where supported.
- Do not poll list/join as a fake event loop.
- Do not claim success from command exit alone when the mission requires observing a resulting state.
```

---

# 39. Reviewer system prompt refresh

Reviewer **无 fission**。

必须保持单一 verdict authority：

```text
- You are a singular review authority for the current typed review request.
- Inspect concrete evidence and the bounded work record required by the request.
- Completion by workers is not proof of correctness.
- Report defects, evidence, uncertainty, missing coverage, cleanup, and required corrections.
- Do not expose hidden orchestration mechanics, identities, barriers, cohorts, confirmation rounds, or consumers.
- Do not parallel-self-replicate or emulate fission through hidden delegation.
```

现有 process/finality typed prompt 差异继续由 Review owner 管，不在通用 role prompt 混成一个状态机。

---

# 40. Blogger system prompt refresh

Blogger 内部、无 fission。

只做小幅清晰化：

```text
- Own canonical work-record synthesis for the assigned source/range.
- Preserve concrete work/evidence and uncertainty; do not invent mission facts.
- Do not describe hidden consumers or orchestration mechanics unless the existing canonical contract explicitly allows it.
- Do not become a scheduler or reviewer.
```

不得把参考 SKILL 的 Main/peer 协作概念灌进 Blogger。

---

# 41. Executor system prompt refresh

Executor 内部、无工具、无 fission。

保持极窄：

```text
- Execute only the exact runtime task the Host assigned.
- Do not infer broader authority.
- Return bounded factual execution outcome.
```

---

# 42. `fission` tool definition 文案

Tool description 必须足够强，让模型知道何时该用。

推荐语义正文：

```text
Split your current logical agent execution into multiple coequal lanes while
keeping the same agent identity, role, authority, parent relation, shared
children, and shared worktree/resource surface.

`prompts` contains one non-empty lane assignment per line. The call returns once
per lane; each lane receives its own `lane_index`, `lane_count`, and prompt and
continues independently from this call point.

Use fission when your own assigned work contains multiple genuinely separable
slices and parallel execution will reduce latency. Fission separable work, not
merely plentiful work. Do not use it for overlapping writers, unresolved
producer/consumer contracts, or several lanes contending for the same mutable
resource.

Fission is internal to this agent. Your parent sees only one logical agent and
one final completion after convergence.
```

Manager 版本可再补一句：

```text
Children are shared by the logical Manager; use fork-agent normally inside each
lane.
```

但最好 description renderer 按 role 注入一小段 role-specific guidance，而不是五份完全复制。

---

# 43. Fission tool output 文案

每 lane tool result：

```text
status = "ok"
lane_index = 0
lane_count = 3
prompt = """A"""
```

结果旁 canonical instruction comment 可简短说明：

```text
Continue as this lane of the same logical agent. Own the assigned prompt.
```

禁止泄漏：

```text
physical session id
internal lane session id
EventStore ids
successor runtime id
hidden attachment topology
```

---

# 44. `fork-agent` tool definition refresh

借鉴 SKILL 的 Assignment Contract，但适配当前万象术，不要求每次生成巨型模板。

description 应明确：

```text
- Delegate a bounded outcome, not vague "handle backend" work.
- Include the context the child cannot otherwise know.
- State scope/non-goals when overlap risk exists.
- Reuse an existing compatible agent_id when continuity is valuable.
- Reuse must not reduce real parallelism.
- Independent children do not form a wave; launch newly-ready work immediately.
- Completion is a result to inspect/integrate, not automatic mission completion.
```

如果当前 schema 已支持 existing AgentId nudge，description 必须与真实 schema 对齐，不能只写 prompt 幻觉。

---

# 45. `fork-manager` tool definition refresh

Orchestrator 不 fission，所以它的扩展并行仍来自 `fork-manager`。

description：

```text
- Create/reuse a Manager for one bounded delivery lane.
- Prefer same Manager for the same compatible delivery goal when the real API supports reuse.
- Open another Manager only for a genuinely independent delivery lane.
- Managers launched together have no implicit barrier.
- Do not duplicate ownership only to increase concurrency.
```

---

# 46. `join` tool definition refresh

借鉴 SKILL 中最重要的反 wave 语义：

```text
Join is collection/event wait, not a "wait for the whole batch" barrier.
```

Manager / Orchestrator 描述应加入：

```text
Before blocking in join, start any useful independent work that is already ready.
After join returns one or more completions, process them, update readiness, and
start newly-unblocked work before joining again.
```

在 fissioned Manager lane：

```text
join only drains child completions affined to this lane
```

这条是 runtime contract，不只是 prompt。

---

# 47. `list` tool definition refresh

只给拥有 list 的角色。

文案：

```text
Use list for deliberate roster/reconciliation when you need exact current
handles/status. Do not poll list in a loop to simulate an event scheduler.
```

---

# 48. Sync `inspector` tool definition refresh

Coder/Meditator/DevOps 的 Inspector delegate description：

```text
Ask a focused evidence question with enough context to investigate it.
Prefer continuation/reuse within the compatible scope rather than restating the
entire repository problem from scratch.
Use the returned evidence in your own reasoning; Inspector is not a substitute
for your role's decision/implementation authority.
```

不得在描述里泄漏 Executor internals。

---

# 49. Sync `coder` tool definition refresh

DevOps 可见的 Coder delegate：

```text
Delegate a bounded implementation outcome with explicit target/scope and
acceptance facts. Reuse compatible context when supported. The Coder owns code
changes; DevOps remains responsible for the operational mission that required
them.
```

DevOps V1 无 fission，不能通过 description 暗示自己可调用。

---

# 50. Filesystem / js-ROLE descriptions

本 Change 不推翻现有 JS Tool DRY：

```text
js-ROLE owns full programmable SDK description
builtin tools keep short native semantics + recommendation hook
```

Prompt refresh 只可调整：

```text
- collaboration/selection guidance
- fission safety reminder where useful
- prefer batching/programmatic operations if already valid
```

不得把完整 fission manual 复制进：

```text
read
edit
write
glob
grep
patch
```

五六份 description。

---

# 51. Tool Definition 单一生成原则

对于 role-sensitive `fission`：

建议：

```fsharp
let fissionDescription role =
    baseFissionDescription
    + roleSpecificFissionGuidance role
```

而不是：

```text
fission-manager.txt
fission-coder.txt
fission-inspector.txt
fission-browser.txt
fission-meditator.txt
```

五份手写 SSOT。

同理 fork/join/list description 若已有 renderer，应在现有 owner 中扩展，不另建 “PromptEnhancement” shadow registry。

---

# 52. Prompt 不能声称不存在的工具

永久 gate：

```text
Every tool name mentioned as callable in a role system prompt
must be provider-visible for that exact role/attempt,
unless the prose explicitly says the role does not have it.
```

特别证明：

```text
Manager mentions fission      -> fission visible
Coder mentions fission        -> visible
Inspector mentions fission    -> visible
Browser mentions fission      -> visible
Meditator mentions fission    -> visible

Orchestrator prompt            -> must not instruct calling fission
DevOps prompt                  -> must not instruct calling fission
Reviewer prompt                -> must not instruct calling fission
```

---

# 53. Prompt 长度与重复控制

参考 SKILL 很长，但生产 role prompt 不应原样复制。

目标是把其原则压缩成 role-specific durable guidance。

每个角色新增内容建议：

```text
~8–16 concise behavioral bullets / short paragraphs
```

Fission tool definition承担详细使用时机。

禁止把完整 multi-agent handbook 塞进每次 system prompt，损害 prefix/token。

---

# 54. Formal Docs Impact

激活后先改 docs，再实现。

至少影响：

```text
docs/why/agent.md
docs/what/agent.md
docs/shape/agent.md
docs/how/agent.md
docs/proof/agent.md

docs/why/execution.md
docs/what/execution.md
docs/shape/execution.md
docs/how/execution.md
docs/proof/execution.md

docs/why/prompt.md
docs/what/prompt.md
docs/shape/prompt.md
docs/how/prompt.md
docs/proof/prompt.md

docs/why/companion.md
docs/what/companion.md
docs/shape/companion.md
docs/how/companion.md
docs/proof/companion.md
```

若 fission lane physical sessions 需要 Host ownership 新 case：

```text
docs/{why,what,shape,how,proof}/host.md
```

若统一 EventStore event family 需登记：

```text
docs/{why,what,shape,how,proof}/persist.md
```

glossary 增加：

```text
Fission
Fission Group
Fission Lane
FissionWorkBundle
```

`docs/README.md` 导航按治理规则更新。

---

# 55. 建议 Clause 分配

不要新建 `docs/what/fission.md` 形成孤岛，除非现有文档规模确实需要。

优先由既有 owner 承担。

## Agent

```text
AGENT-*  Fission role eligibility
AGENT-*  Fission same-role/tier/authority projection
AGENT-*  Role prompt responsibility/collaboration guidance
```

具体空号需激活时核对正式 docs，不能盲占已使用 ID。

## Execution

```text
EXEC-*  fission call/admission
EXEC-*  lane identity and no-public-handle
EXEC-*  shared children + completion affinity
EXEC-*  ring handoff
EXEC-*  convergence exactly once
EXEC-*  cancellation/recovery
```

## Prompt

```text
PROMPT-* lane request profile inheritance
PROMPT-* fission tool result projection
PROMPT-* role system prompt/tool-description DRY
```

## Companion/LWR

```text
COMPANION-* lane canonical WorkRecordRef
COMPANION-* FissionWorkBundle aggregation reusing LWR
```

Clause ID 在 Active 开工时从当前 docs 实际空位分配。

---

# 56. Runtime Ownership 建议

建议新增一个物理 owner：

```text
FissionRuntime
```

但它只拥有：

```text
active group physical lane lifetimes
lane -> physical execution mapping
event subscriptions
inbox wake
convergence single-flight
```

它**不拥有**：

```text
Agent catalog
role permission matrix
child registry
LWR generation
EventStore
PromptDispatcher
filesystem transactions
```

这与现有架构“物理 runtime owner 只管 lifetime，业务事实进 EventStore”的方向一致。

---

# 57. FissionRuntime 不得成为第二 child owner

Shared children 继续从已有 Host/fork runtime 读取。

FissionRuntime 只提供 current lane context：

```text
CurrentFissionLaneKey option
```

fork runtime 在创建 child active run 时记录 affinity。

禁止：

```text
FissionRuntime.Children : Map<...>
```

---

# 58. Physical lane topology

激活时先做 Host canary，确认 OpenCode/SDK 对以下哪种 physical 实现可行：

```text
A. hidden sibling Sessions sharing logical owner projection
B. one owner session + separate provider lane transport abstraction
```

但无论选 A/B，都必须满足第 7 节的单线泵不变量。

**如果 A 是唯一可行路径，不得因为 physical session 是多个就把它们升级成多个 logical Agents。**

它们可以是：

```text
FissionLaneSession
```

Host-private runtime concept。

是否要扩 `SessionOwnership`，必须服从 HOST-008 的正交模型；禁止随手塞 `SatelliteKind.Fission`。

建议优先：

```text
ExecutionLane 是 Session 之上的 runtime abstraction
```

如果宿主硬要求一 lane 一 session，再由 Host docs 明确它们的 ownership/projection。

---

# 59. Provider profile inheritance

每 lane 必须从 origin attempt 冻结继承：

```text
CanonicalRole
SelectedAgent
EffectiveAgent
tier
model binding semantics
ToolCapabilitySet
request authority root
directory/worktree
```

不得：

```text
lane0 fast
lane1 deep
lane2 user-select model
```

Fallback 若在某 lane 内发生，继续走该 lane 正常同角色 peer fallback 规则；不能改变 role 或 fission eligibility。

---

# 60. Fission 与 Strength

两者正交。

Strength：

```text
speculative same-role fast InternalLeaf/Attached Replica
candidate -> promotion
read-only
```

Fission：

```text
authoritative current agent execution
coequal lanes
same logical AgentId
may mutate if role permits
all lanes are real work
```

禁止把 fission 实现成：

```text
spawn N StrengthReplica
choose/promote one
```

Fission 所有 lane 都必须完成/入账。

---

# 61. Fission 与 SyncDelegate

SyncDelegate：

```text
caller -> dedicated callee
synchronous
different logical role/session identity
Returned -> Completion dual await
```

Fission：

```text
same logical caller
same role
parallel lanes
no parent-visible callee
```

V1 若 fission lane 内调用 SyncDelegate：

```text
single-flight key must include immediate lane/caller execution scope sufficiently
to avoid one lane blocking another independent lane merely because AgentId same
```

但不能按 family root gate，否则：

```text
Manager/Coder fission lane0 inspector()
lane1 inspector()
→ incorrectly serialized
```

这里必须在 Active 时专门审计现有 `OwnerReuseScopeId`：fission lane 是否需要派生 lane-local execution scope while preserving logical owner identity。

这是 blocking design point，不能靠测试碰运气。

---

# 62. Fission 与 Manager child reuse

Manager lanes children shared。

推荐 prompt：

```text
Reuse compatible children when their context matches the lane's work.
Do not reuse a child whose existing context would make ownership ambiguous.
Do not let reuse reduce genuine independent parallelism.
```

如果 laneA 复用 laneB 创建的 idle child：

```text
new active Run affinity = laneA
```

如果 child busy：

```text
existing active Run affinity unchanged
```

符合 EXEC-002 busy nudge 语义。

---

# 63. Fission 与 `join`

Fissioned Manager `join`：

```text
drain completions affined to current lane
```

非 fission Manager：

```text
current semantics unchanged
```

当 lane closes：

```text
pending affinity lane k
→ successor(k) ownership
```

这必须是 durable/recoverable transfer 或从 durable lane-close/handoff facts 唯一推导。

不能只改内存 dictionary。

---

# 64. Fission 与 `list`

Manager lane 的 `list`：

```text
shows logical Manager's shared children
```

不显示：

```text
fission lane 0
fission lane 1
fission lane 2
```

否则 Manager 自己会把 lane 当 child，概念污染。

---

# 65. Fission 与 Coder writes

本 Change 不增加全局 lock。

但必须有测试证明：

```text
two lanes editing disjoint files -> both survive
```

以及受控冲突：

```text
two lanes intentionally target same snapshot-sensitive file
→ existing mutation conflict/anchor semantics decides
→ no silent file corruption introduced by fission runtime
```

不要为了 fission 新造 `FissionFileLockRegistry`。

---

# 66. Fission 与 Todo/Manager Life

Manager fission 后仍是同一个 Manager Life。

不得：

```text
one lane = one Manager Life
```

Magic Todo / process review / Finality 的 authority remains logical Manager.

因此第一版需明确 Manager fission lane 对 `todowrite`/suicide 等 Manager-only lifecycle side effects 的规则。

安全建议：

```text
- fission lanes may perform ordinary fork/join/list scheduling
- singular Manager-lifecycle checkpoint/finality tools remain logically single-owner
```

如果 `todowrite` 在多个 lane 同时可调用，会产生并发 proposal/review checkpoint，必须有明确 merge/serialization。

**Blocking requirement：Active 前先 inventory Manager-only lifecycle tools。**

建议 V1：

```text
Manager fission lanes may not independently invoke singular lifecycle/finality tools
unless the existing tool already has a concurrency-safe logical-Life owner.
```

若 tool schema 可按 lane隐藏/动态 gate，优先 fail closed，而不是发明 last-writer-wins。

`fission` 的核心价值仍可通过并行 fork/join 管理子图获得。

---

# 67. Role Prompt × singular authority

通用规则：

> Fission copies execution capacity, not singular authority semantics.

因此：

```text
Reviewer verdict       -> Reviewer has no fission
Orchestrator mission   -> Orchestrator has no fission
Manager Life finality  -> remains one logical owner
```

对 Manager 中仍 singular 的 side-effect，用 typed runtime gate，不靠 prompt 自觉。

---

# 68. Implementation Phase 0：事实调查 Canary

正式实现前必须先做 canary，不能猜 Host 能力。

至少确认：

```text
C0.1 Can two hidden physical sessions share same role/config/worktree safely?
C0.2 Can origin transcript prefix be cloned/replayed byte-identically?
C0.3 How are tool-call IDs/session IDs bound by Host callbacks?
C0.4 Can lane-specific tool results be projected without mutating common history?
C0.5 What event marks provider-turn completion vs session terminal?
C0.6 Can a closed/idle hidden lane be resumed after a handoff arrives?
C0.7 How does abort propagate?
C0.8 How does current child runtime key owner/session/reuse scope?
C0.9 Can Manager lifecycle tools be safely invoked concurrently? Inventory only; do not assume.
```

任一 blocking canary 不明确：

```text
do not implement production fission runtime
```

先把真实 Host seam 写入 Active evidence。

---

# 69. Implementation Phase 1：Formal docs

严格先：

```text
why
what
shape
how
proof
```

明确：

```text
logical Agent identity
physical lane identity
role allowlist
tool schema
parser
capacity
shared children
completion affinity
Y bundle
ring
convergence
recovery
prompt DRY
```

docs 绿后再写 RED。

---

# 70. Implementation Phase 2：Pure Domain

先实现纯函数：

```text
FissionPolicy.canRole
FissionPromptParser.parse
FissionDecision.decide
FissionWorkBundle.empty/add/merge
FissionRing.successor
FissionConvergence.decide
ChildCompletionAffinity transfer decision
```

必须不引用 Host/OpenCode。

---

# 71. Implementation Phase 3：Persistence projection

新增统一 EventStore event codec/fold。

先证明：

```text
admit idempotent
lane close idempotent
bundle key conflict fail closed
forward idempotent
converged exactly once
restart fold exact
```

再接 runtime。

---

# 72. Implementation Phase 4：Physical lane runtime

实现：

```text
create/recover lanes
bind origin prefix
send per-lane fission tool result
run provider turns
observe lane turn completion
wake on handoff
close lane
```

此阶段不接 Manager shared-child affinity，先用无 child 的 Inspector/Browser/Meditator canary。

---

# 73. Implementation Phase 5：LWR handoff

接既有 canonical LWR：

```text
lane close
→ materialize WorkRecordRef
→ add to FissionWorkBundle
→ send successor
```

若 LWR materialize失败：

```text
fail closed
```

不允许只用 final prose 顶替 Y。

---

# 74. Implementation Phase 6：Coder

接 shared worktree + mutation tests。

先：

```text
disjoint writes
```

再：

```text
conflict safety
```

不先做 Manager。

---

# 75. Implementation Phase 7：Manager

最后接最复杂的：

```text
shared children
completion affinity
fork existing
busy nudge
join
list
lane close affinity handoff
Manager Life singular tools
```

Manager e2e 必须作为 fission 关闭前的 blocking gate。

---

# 76. Implementation Phase 8：Prompt refresh

不要一上来先改 prompt 掩盖 runtime 未完成。

当每个工具真实可见/可执行后：

```text
role system prompt
fission tool def
fork/join/list
sync delegate descriptions
```

统一更新。

Tool description schema/parameters/jsonSchema 若 Host 有多表示，必须从同一 owner同步生成。

---

# 77. Unit Tests：Parser

至少：

```text
" A\nB\nC " preserves non-newline text
CRLF normalizes
one final LF accepted
N=1 rejected
empty middle line rejected
whitespace-only line rejected
two lines accepted
large N over capacity rejected atomically
```

---

# 78. Unit Tests：Role eligibility

矩阵：

```text
Manager      yes
Coder        yes
Inspector    yes
Browser      yes
Meditator    yes
Orchestrator no
DevOps       no
Reviewer     no
Blogger      no
Executor     no
```

fast/deep pair equality property。

---

# 79. Unit Tests：Bundle algebra

Property：

```text
merge associative for disjoint/same-ref keys
merge idempotent
different ref same lane -> conflict
complete bundle iff keys exactly 0..N-1
render order stable by index
```

---

# 80. Unit Tests：Ring

对 N=2..16 随机 completion permutation：

```text
close lanes in arbitrary order
→ eventual bundle contains every index exactly once
→ exactly one convergence
```

特别：

```text
successor already closed
multiple consecutive closed successors
lane N-1 wraps to 0
```

---

# 81. Unit Tests：Exactly once

```text
same origin tool replay
→ one admission

same lane terminal replay
→ one own record

same handoff replay
→ no duplicate record

convergence replay
→ no second parent completion
```

---

# 82. Integration：same Agent identity

Coder X fission A/B/C：

断言 parent surface：

```text
agent_id before == after == X
no lane ids in list
no extra child handles
one completion
```

lane internal：

```text
same role
same tier
same effective tool capability
same worktree
```

---

# 83. Integration：Inspector fission

```text
Inspector fission 3 read-only investigations
all run concurrently
each produces canonical record
ring converges
caller receives one completion containing integrated evidence
```

---

# 84. Integration：Browser fission

用 deterministic fake network provider：

```text
three independent delayed sources
wall-clock/event ordering proves overlap
no wave barrier
converged result includes all source work
```

不依赖公网 e2e。

---

# 85. Integration：Meditator fission

三 branch：

```text
hypothesis A
hypothesis B
counterexample
```

各自可调用 dedicated Inspector。

证明：

```text
Meditator role remains inspector-only
fission does not accidentally project read/glob/grep
```

---

# 86. Integration：Coder shared worktree

A/B/C 各改独立文件：

```text
all three edits present
one parent completion
```

再受控同文件冲突：

```text
no fission-owned silent overwrite path
```

---

# 87. Integration：Manager shared children

Manager fission 3 lanes：

```text
lane0 forks Coder A
lane1 forks Inspector B
lane2 forks Browser C
```

断言：

```text
list from any lane sees A/B/C handles
child registry has one logical owner
completion A affined lane0
completion B affined lane1
completion C affined lane2
join lane0 never consumes B/C
```

---

# 88. Integration：Manager cross-lane reuse

```text
lane0 creates idle child X
lane1 later reuses existing X
```

断言：

```text
same child handle
new active run affinity = lane1
no duplicate child registry entry
```

busy child：

```text
lane0 owns active X run
lane1 nudges X while busy
→ no new RunId
→ affinity remains lane0
```

对齐 EXEC-002。

---

# 89. Integration：lane closes with pending child

```text
lane0 has active/ready child completion ownership
lane0 closes
→ affinity transfers via successor ring
→ result not orphaned
```

successor 已 closed 也要继续 mechanical forward。

---

# 90. Recovery Tests

每个 frontier crash：

```text
after FissionAdmitted before physical lanes all created
after lane0 physical creation
after lane result sent
after lane0 WorkRecordRef
after bundle forward
after successor already closed
after all lane close before convergence event
after convergence event before parent physical completion publication
```

restart：

```text
no duplicate lane work
no duplicate parent completion
no lost WorkRecordRef
```

---

# 91. Cancellation Tests

```text
owner operator abort while 3 lanes running
→ all lanes stop
→ no success convergence

one lane unrecoverable failure
→ group fails once
→ parent does not see two terminals

session delete / owner teardown
→ all lane physical resources cleaned by one owner path
```

---

# 92. Prompt Golden Tests

每个 role system prompt检查：

```text
no stale tool inventory
role identity/responsibility/collaboration present
fast/deep byte-equal modulo agent/model binding as existing contract requires
```

fission roles：

```text
mention self-fission strategy
mention separable work
```

non-fission roles：

```text
do not instruct calling fission
```

---

# 93. Tool Description Golden Tests

`fission`：

```text
schema only prompts:string
one-line-per-lane semantics
same identity/role/authority
shared children/resource surface
parent transparent
separable-not-plentiful warning
```

`join`：

```text
not a wave barrier
refill/process readiness guidance
```

`list`：

```text
not polling loop
```

`fork-*`：

```text
bounded assignment
reuse compatible context
no duplicate ownership
```

---

# 94. Static Gates

新增静态/生成门禁。

## 94.1 Eligibility SSOT

生产只允许一个 role→fission policy owner。

## 94.2 No fake lane Agents

禁止出现类似：

```text
fast-fission-coder
lane-coder
twin-coder
Role.FissionLane
```

作为 public Agent catalog/Role。

## 94.3 No second child map

扫描禁止 `FissionRuntime` 自有 child registry 类型/字段。

## 94.4 No feature persistence

禁止：

```text
fission.db
fission.json
lane-manifest
```

作为 runtime truth。

## 94.5 No Reviewer/DevOps/Orchestrator fission

schema + runtime 双重 gate。

## 94.6 No nested V1

active group 中第二 fission 必须 mechanical reject。

## 94.7 Prompt/tool parity

prompt 提到可调用 tool → exact role schema可见。

---

# 95. Anti-patterns：一票否决

以下任一出现不得 Completed：

```text
- 每 lane 新建公开 AgentId
- parent list 能看到 fission lanes
- parent 需要 join lanes
- 同一个 physical session 跑多个并发 provider pump
- lane 自己拥有独立 child registry
- lane join 可以偷别 lane 的 child completion
- 用 final prose 代替 canonical Y/LWR
- work record 直接 string append
- successor closed 就丢 handoff
- completion order 决定非 durable 的随机最终记录
- nested fission 默认开启
- Reviewer 暴露 fission
- DevOps 暴露 fission
- Orchestrator 暴露 fission
- Meditator 因 fission 获得 filesystem read
- fission 自己选择 fast/deep/model
- 新建 fission persistence store
- system prompt 复制 tool capability matrix
- tool description 与 runtime/schema 不一致
- 把外部 SKILL 文件打包成 runtime dependency
- 把 external SKILL 中 Main-only validation 等项目特有规则原样搬来覆盖万象术角色边界
```

---

# 96. Prompt refresh 的验收方法

不要只做字符串 snapshot。

需要 scenario evaluation。

## Manager eval

给：

```text
three independent delivery lanes
one blocked dependency
one reusable existing child
```

期望：

```text
starts independent lanes
reuses compatible child
does not wait blocked lane
may self-fission when own scheduling becomes bottleneck
join not used as batch barrier
```

## Coder eval

给三个独立模块 + 一个共享 fragile module。

期望：

```text
fission independent three
does not fission conflicting shared module blindly
```

## Inspector eval

给三类证据问题。

期望：

```text
fission read-only investigation
```

## Browser eval

给官方 docs + upstream issue + release note。

期望：

```text
parallel source families
reconcile
```

## Meditator eval

给三种竞争解释。

期望：

```text
fission hypotheses
use Inspector for facts
synthesize
```

## DevOps eval

期望：

```text
does not call fission
```

## Reviewer eval

期望：

```text
single verdict path
does not call fission
```

---

# 97. Performance Eval

需要证明不是只增加复杂性。

选一个 synthetic Coder workload：

```text
A = 3 independent delayed tool workflows
B = serial baseline
```

测：

```text
provider wall time
total tokens
tool count
failed tool calls
```

验收重点：

```text
fission reduces critical-path latency materially
```

不要求 token 一定减少；但不得出现无界指数调用。

---

# 98. Concurrency budget eval

构造：

```text
requested lanes > available capacity
```

必须：

```text
zero partial lanes
one typed rejection
```

构造两个 logical agents 同时 fission：

```text
global cap respected
```

不得每个 Agent 自己认为“我有 N slots”而突破全局 owner。

---

# 99. Migration / compatibility

这是 additive feature。

旧 session/journal 无 fission facts：

```text
behaves exactly as before
```

不需要 legacy migration。

升级后只有新 `fission` call 才产生 group。

若 downgrade 后遇到未知 fission event：

```text
按统一 EventStore/version compatibility policy处理
```

本 Change 不发明 feature-local兼容 reader。

---

# 100. Documentation for users/models

不要向普通 parent/user解释内部物理 lane session。

公开模型只需要知道：

```text
fission is same-agent internal parallelism
one line = one lane
same identity/permissions/children
use only for separable work
parent sees one final completion
```

Host/internal docs 再解释：

```text
physical lane execution
affinity
EventStore
LWR
ring
recovery
```

---

# 101. 典型完整 trace：Coder A/B/C

初始：

```text
Manager M
└─ Coder X
```

X：

```text
fission("implement A\nimplement B\nimplement C")
```

Host：

```text
FissionAdmitted G(X, call42), N=3
```

lane projection：

```text
X/G/0 receives implement A
X/G/1 receives implement B
X/G/2 receives implement C
```

注意：

```text
logical AgentId is X for all
```

并行工作：

```text
lane0 edits A files
lane1 edits B files
lane2 edits C files
```

假设完成顺序：

```text
lane1
lane0
lane2
```

lane1：

```text
materialize canonical LWR1
bundle {1}
successor 2 inbox += {1}
close 1
```

lane0：

```text
materialize LWR0
bundle {0}
successor 1 already closed
mechanical forward through 1
→ successor 2 inbox += {0}
close 0
```

lane2 当前 attempt 完成时：

```text
inbox = {0,1}
```

所以不直接 close：

```text
materialize predecessor records into safe next boundary
continue lane2
```

lane2 整合后完成：

```text
materialize LWR2
bundle {0,1,2}
successor 0 closed
all lanes accounted
→ Converged
```

parent：

```text
Manager M receives exactly one Coder X completion
```

没有：

```text
Coder X/0 handle
Coder X/1 handle
Coder X/2 handle
```

---

# 102. 典型完整 trace：Manager fission

Orchestrator：

```text
Manager M: deliver auth + billing + notifications
```

M：

```text
fission("""
own auth delivery
own billing delivery
own notification delivery
""")
```

lanes：

```text
M/0 fork-agent Coder A
M/1 fork-agent Coder B
M/2 fork-agent Browser C
```

shared child set：

```text
M.children = {A,B,C}
```

completion affinities：

```text
Run(A) -> M/0
Run(B) -> M/1
Run(C) -> M/2
```

每 lane 只 join 自己 active-run completion。

M/1 先完成，Y/children affinity handoff给 M/2。

最终：

```text
one Manager M completion
```

Orchestrator 不知道有三 lane。

---

# 103. 为什么不是 wave scheduler

错误：

```text
fission A/B/C
wait A+B+C all terminal
then one new synthesis agent
```

这相当于内部 wave barrier + extra agent。

正确：

```text
each lane runs independently
completion immediately produces ring handoff
successor continues when needed
convergence emerges from event facts
```

没有额外 “gather agent”。

---

# 104. 为什么不是 `scatter/gather`

虽然外形类似：

```text
scatter
gather
```

但产品语义不同：

```text
- no separate gather API
- no parent participation
- gather target is ring successor / last logical lane
- identity remains one agent
```

因此正式名字保持 `fission`。

---

# 105. Why：应写入的第一性原理

正式 `why` 层至少讲清：

1. 层级 fork 要求 parent 预知 child 分工；fission 把局部拆分权还给 worker。
2. 同一 Agent 的 broad independent work 不应因为单 provider execution 而被迫串行。
3. 对 parent 透明使性能优化不污染组织协议。
4. Role/authority/children 共享保证“裂的是执行容量，不是身份”。
5. Reviewer/Orchestrator 等 singular authority 不适合裂，说明 fission 不是“所有角色都更多并发越好”。
6. continuous-flow guidance 使 primitive 真正被模型在正确位置使用。
7. system prompt / tool description 分工防止 capability drift。

---

# 106. What：必须冻结的 observable semantics

正式 `what` 必须至少包含：

```text
tool availability by role
prompts parsing
N results
lane_index/count/prompt
same logical AgentId
same role/tier/authority
shared children
parent invisibility
one final completion
nested rejection
capacity all-or-none
failure/cancel
```

不要在 `what` 写具体 class/file 名。

---

# 107. Shape：必须冻结的 ownership

正式 `shape` 至少：

```text
FissionRuntime owns physical lane lifetime only
Agent owner owns child identities
EventStore owns durable facts
Companion/LWR owns work record
PromptAuthority owns request profile
Tool registry owns execution permission
Role policy owns fission eligibility
```

画出：

```text
Logical Agent X
├─ FissionRuntime -> physical lanes
├─ Child owner    -> shared children
├─ LWR owner      -> canonical records
└─ EventStore     -> durable fission facts
```

---

# 108. How：必须写清的算法

正式 `how` 至少给出：

```text
parser
admission
lane creation atomicity
per-lane tool-result projection
child affinity
lane close
bundle merge
ring forwarding closure
inbox wake
convergence
restart
abort
```

让新人可以照算法实现，而不是“合理处理”。

---

# 109. Proof：必须给永久矩阵

正式 `proof` 至少：

```text
role matrix
parser matrix
ring permutation property
exactly-once
parent invisibility
same AgentId
same role/tier
shared children
affinity no-steal
restart frontiers
prompt/schema parity
non-fission roles absence
```

---

# 110. Completion Criteria — Product

全部满足才可关闭：

```text
[ ] Manager can fission
[ ] Coder can fission
[ ] Inspector can fission
[ ] Browser can fission
[ ] Meditator can fission

[ ] Orchestrator cannot fission
[ ] DevOps cannot fission
[ ] Reviewer cannot fission
[ ] Blogger cannot fission
[ ] Executor cannot fission

[ ] one-line-per-lane parser frozen
[ ] all-or-none capacity
[ ] same logical AgentId
[ ] same role/tier/authority
[ ] shared children
[ ] no lane handles exposed
[ ] no parent join
[ ] one parent completion
[ ] arbitrary completion order converges
[ ] canonical Y/LWR only
[ ] restart deterministic
[ ] nested V1 rejected
```

---

# 111. Completion Criteria — Prompt Refresh

```text
[ ] all current roles receive role-specific mission/ownership/collaboration guidance
[ ] no system prompt duplicates filesystem capability matrix
[ ] fission roles are taught when/how to fission
[ ] non-fission roles are not told to call it
[ ] Manager continuous-flow/no-wave guidance upgraded
[ ] fork-agent/fork-manager descriptions teach bounded ownership and reuse
[ ] join description says event collection, not wave barrier
[ ] list description rejects polling-loop use
[ ] Inspector/Browser evidence roles emphasize concrete evidence and uncertainty
[ ] Meditator keeps reasoning/evidence separation
[ ] Reviewer keeps singular verdict and hidden-mechanics discipline
[ ] external SKILL is not a runtime artifact/dependency
```

---

# 112. Completion Criteria — Architecture

```text
[ ] no second child registry
[ ] no second work-record renderer
[ ] no feature-owned persistence
[ ] no parallel provider pump inside one physical session abstraction unless Host contract is explicitly upgraded and proven
[ ] lane physical identity never becomes public Agent identity
[ ] child completion affinity prevents cross-lane steal
[ ] SyncDelegate scope interaction proven
[ ] Manager Life singular side-effects audited
[ ] ToolCapabilitySet/runtime gate parity
```

---

# 113. Completion Criteria — Validation Order

最终 gate 顺序：

```text
1. docs/spec
2. pure domain tests
3. EventStore fold/property
4. fission parser/tool schema
5. Inspector fission integration
6. Browser fission integration
7. Meditator fission integration
8. Coder disjoint-write integration
9. child affinity integration
10. Manager fission e2e
11. restart/cancel e2e
12. prompt golden/evals
13. affected existing fork/join/sync/review tests
14. global build/test/lint/spec
```

任何阶段失败：

```text
repair
→ restore green
→ continue
```

不要把所有改动堆完最后一次跑全局。

---

# 114. Final Definition of Done

最终真实体验应是：

```text
Manager:
  "Coder X, implement A/B/C."

Coder X:
  sees independent slices
  → fission(A,B,C)

A/B/C execute concurrently
no new public agents
same worktree
same permissions
same children

internal Y records converge

Coder X:
  returns once

Manager:
  sees one Coder X result
  never needed to know fission existed
```

与此同时：

```text
Inspector can parallelize evidence
Browser can parallelize sources
Meditator can parallelize hypotheses
Manager can parallelize its own bounded scheduling lanes
```

而：

```text
Orchestrator remains one control plane
DevOps remains unsplit in V1
Reviewer remains one verdict authority
```

这就是本 Change 的完成形状。

---

# 115. 一句话裁决

> **万象术已有的 `fork-*` 负责“创造下级并行”，新增 `fission` 负责“同一 Agent 自身横向展开”。Fission 只裂执行容量，不裂身份、权限、parent、children 或最终返回；Manager/Coder/Inspector/Browser/Meditator 可用，Orchestrator/DevOps/Reviewer/Blogger/Executor V1 禁用。与此同时，以 continuous-flow、真实 ownership、及时 handoff、context reuse、no-wave-barrier 为核心刷新所有角色 system prompt 与 tool definitions，使模型不仅“拥有并发”，还会在正确边界主动使用它。**
