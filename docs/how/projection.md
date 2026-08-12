# Projection — 目标实现

## Implements

行为合同见 `what/projection.md`；本文件只描述 snapshot、planner、intent 排序和 renderer 算法。

## Ownership

Coordinator、Planner 与 Renderer 边界见 `shape/projection.md`。

---

## 装配：三层

一次 provider request 的投影按 shape/projection.md 三层实现：

```text
Effectful Coordinator
    Host 读取 → 完整 snapshot（PROJ-002 的 ProjectionSnapshot）→ 不可变，交 Planner

Pure Projection Planner
    汇总各功能 ProjectionIntent（PROJ-005）→ 排序 → 冲突检查（PROJ-006）→ 产出有序 intent 序列

Canonical Renderer
    逐 intent 落 ProjectionSnapshot 的语义树 → 渲染 provider wire bytes → 生成 digest/seal
```

禁令（PROJ-001/007）落点：功能模块只声明 intent，不得直接接收/改写 `Message list`；渲染器不驱动生命周期。

---

## Intent 排序与冲突（PROJ-005/006）

intent 排序锚点固定为「当前被投影前缀锚」。canonical order、可合并组合与冲突必须先于实现固定，禁止依赖注册顺序：

```text
keepPhysicalPrefix        // 普通 Work base：物理前缀原样
activatePrefixEpoch       // 普通 Work base：active prefix snapshot
useStrengthMirror         // StrengthReplica-only base；与前两者互斥
insertBlogFrames          // Y 有效帧（Entry/Squash）插入历史槽
insertRepair              // Interaction Repair 回合
insertStrengthFrames      // Candidate / Promoted / Replica-local；显式 visibility+anchor
suppressTransportOnly     // transport-only 消息剔除（COMPANION-012）
appendReviewChallenge     // skeptical challenge
reanchorAfterCompaction   // ContextReanchored → Snapshot=None
```

HOST-013 pair-programming marker 不占 intent（wire 无消息地址，anchored replay 在
`PairProgrammingThoughtTransform` raw 域执行，见 HOST-013 程序）。

消息级 `suppressTransportOnly` intent 路径以 proof/实现为准；当前生产未声明该 intent（`TransportMessages` 恒空）。COMPANION-012 字段级过滤由模型边界 / `toSemantic` 承担。

同锚 intent 必须先按规范定义的稳定总序归一化，再执行显式合并或返回 `ProjectionConflict`。禁止依赖模块注册顺序。Strength 的 `useStrengthMirror` 只能作为 StrengthReplica base；与 `keepPhysicalPrefix/activatePrefixEpoch` 同批即冲突。`insertStrengthFrames` 同 DecisionId+digest 幂等；同 anchor/DecisionId 不同 digest、Candidate target 不匹配或 Promoted 反射进当前 Replica mirror均冲突（STRENGTH-009/016）。

合并函数只需证明其真实代数性质：重放型 intent 必须幂等；有序追加型 intent 必须保持 canonical order，不能被虚构为可交换。任何尚未定义的组合 fail closed。PrefixEpoch 始终是冻结 X 前缀选择；`insertBlogFrames` 只在其后构造 Y 可见历史，不得“合入”或改写 active X epoch。

---

## 语义树与两投影

快照落到 `SemanticEventTree`，再两选一投影（VERIFY-007，不同型，禁止隐式互转）：

```text
ProviderSemanticProjection    // 去 ID：语义等价、跨会话可比较、canonical digest 唯一来源（COMPANION-007）
ProviderWireProjection        // 含 ID、字节相等、本地时间线、seal 与前缀缓存用
```

实现边界：

- Semantic：过滤 transport-only 字段（timestamp/cost/usage/runtimeId…，COMPANION-012），只留进模型的字节相关部分；TEXT 序列化顺序固定，供 digest。
- Wire：在 Semantic 之上补合成 identity（COMPANION-013：SealRoot/frameEpoch/ordinal 确定性派生，禁 GUID/random/时间）与本地时间线序号。

禁止：把 Wire 反解析回 Semantic 当 digest（COMPANION-007）；用 wireshape 判语义等价。

---

## Canonical digest

```text
CanonicalDigest = SHA-256( 规范序列化(ProviderSemanticProjection(tree)) )
```

- 规范序列化：固定字段序、UTF-8、无空白歧义；同语义对话跨 ID 产出同一 digest。
- 使用点：CoveredPrefixDigest（COMPANION-011 重算失配 fail closed）、canary 的 `LegacyDigest = DslDigest` 切换判据、Review 双 PERFECT。

---

## Seal 生成点

Wire 渲染收敛后、首字节外发前收敛 `ProviderInputSeal`：

```text
耗时最长、边界最窄处生成；
ProviderRunIdentity 由排查到唯一未完成 assistant 顶（HOST-010）；
seal 一经发出不可变（COMPANION-009 字节级 sealed 屏障）。
```

---

## 接线

| 来源 | 落点 |
|------|------|
| X prefix probe 候选 | `AttemptExecutionProfile.ProjectionChoice` → `activatePrefixEpoch`（how/context.md CTX-010） |
| Y frames | insert during `BloggerMain`/`BloggerSquash`（how/companion.md） |
| LWR / delta | Semantic 源同 XTrace、不同投影（COMPANION-007；delta 见 how/context.md CTX-013） |
| reanchor | transform 观察到 compaction → `reanchorAfterCompaction`（how/host.md HOST-006） |
| challenge / seal | how/review.md REVIEW-003/004/006 |
| Strength Replica base / frames | `useStrengthMirror` / `insertStrengthFrames`（how/strength.md；STRENGTH-009/016） |
| pair-programming marker | how/host.md HOST-013 — 由 `PairProgrammingThoughtTransform` 直接按 durable gap anchor replay（wire 级 DSL 无消息地址，无法做 anchored 渲染，故不占用 intent）；参与 Wire/seal，不进 XTrace |

---

## PROJ-008：迁移顺序

旧投影必须按以下顺序迁移到 DSL：

1. 普通 X + ActivePrefixEpoch projection
2. attempt-local PrefixProbe projection
3. Companion BloggerMain / BloggerSquash / BloggerDelta projection
4. InteractionRepair projection
5. ReviewConfirmation + skeptical challenge Seal projection
6. Host compaction reanchor 后 projection

迁移期间测试环境可同时运行 LegacyProjection 和 DslProjection 并比较 canonical digest；生产环境不得按请求随机混用两套实现。

切换条件：所有历史 canary 轨迹 `LegacyDigest = DslDigest`；允许有意变化的差异须有明确的新 SSOT 条款。

切换后删除 `LegacyProjection`，不长期维护双实现。

---

## Provider Horizon 渲染过滤（PROJ + ARCH-014）

Canonical Renderer 落 wire 前，每个 provider-visible field 过 Horizon filter（ARCH-014）：

```text
Did the participant already know this?        → omit
Did they just supply this themselves?          → omit
Is it implied by successful completion?        → omit
Is it useful only for correlation/debug?       → keep internal
Would different values change next action?     → if no → omit
Does the participant need the value itself
  rather than merely its consequence?          → if no → render consequence
                                                → if yes → preserve minimal observation
```

### 禁止穿过 horizon（本域渲染硬禁）

```text
SessionId / AgentId / RunId / ToolCallId / Journal EventId / cursor / offset
status / code / error DTO / phase / ordinal / kind（机器态枚举）
settled / proposed / reviewing / semanticMerge 标签
Host TodoTable sink 枚举冒充 CurrentObligations
spool_path（已删路径）/ 其它虚假 affordance
```

```text
pass:
  consequences + WorkRecord / obligations [{name, work}] / exit_code / verdict
  / root_requirement / commissioner_record prose
fail:
  id 泄漏、状态机枚举、DTO decoder 字段
```

Semantic projection 去 ID（COMPANION-007）；Wire 仅补合成 identity（COMPANION-013），**不得**把机器 id/status 回灌 Semantic 或模型可见 data 平面。  
MagicTodoProjection 只读 obligations 真值（PROJ-009）；REVISE sink 对齐不产生新 ProjectionIntent。
