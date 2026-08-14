# Reverse coverage ledger

Phase A 产出：把现行 `docs/what/*.md`（及其 shape/how 同前缀条款）逐 proposition 判未来 owner。
本文件是 living ledger：每轮 append 一个 topic 的覆盖表 + 四类 delta，不重写已闭合结论。

## Schema

```text
Clause / proposition
Current topic
Future owner
Classification = OWNED | HOW | GARBAGE | ORPHAN | NEEDS-SPLIT
Evidence notes
```

判据（不变）：WHY、DOES NOT OWN、failure meaning、independent-change test。
按 proposition 不按 Clause 文件搬家；一个 Clause 含两个独立 WHY 标 NEEDS-SPLIT。

## Progress

| Topic | 状态 | 备注 |
|---|---|---|
| `prompt.md`（含 shape PROMPT-005/008、how PROMPT-009） | DONE | 21 条款；3 条 NEEDS-SPLIT，无新包 |

下一 topic：`agent.md`。

---

# `docs/what/prompt.md`（+ shape/prompt.md + how/prompt.md）

## 覆盖表

### OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| PROMPT-001 | `PhysicalUserMessage ≠ AuthorityTurn`；`role=user` 只是运输格式；零宽/空白/模板/时间戳/Synthetic TOML 形态都不是 Authority 证据 | `interaction-authority` | interaction-authority OWNS 原文 `PhysicalUserMessage ≠ AuthorityTurn`；CURRENT EVIDENCE 已引 PROMPT-001 |
| PROMPT-002 | Root 独占权：新建 LogicalRun、选/改 SelectedAgent、成 Fallback root、重置 repair 预算、成延续来源 | `interaction-authority` | 正命题唯一；「Root 不得」列表是引用其它 owner 事实：Companion 关联→`session-ontology`，SessionPersona 重绑→`participant-identity`，`Model=None`→`dispatch-protocol` |
| PROMPT-003 | Continuation 禁区：只延续已有 LogicalRun，不建 RunId、不改 SelectedAgent、不更新 LastAuthorityProfile、不重置 fallback/repair | `interaction-authority` | 「历史 TeacherQuestion/Student… 已删」= GARBAGE 迁移 note；ManagerGuard 历史 journal 解析保留=HOW |
| PROMPT-004 | `PromptOrigin`（Root/Continuation/HostInternal/Unknown）+ `RootAuthorityKind`（HumanRoot/AgentOwnerRoot）+ UnknownOrigin fail-closed | `interaction-authority` | interaction-authority OWNS 四类 provenance 区别 |
| PROMPT-005 | PromptDispatcher 唯一写入口；Claimed→Submitted→PhysicalAccepted / Abandoned 四态 | `dispatch-protocol` | dispatch-protocol OWNS claim/submission/physical acceptance 分型；PROOF-MAP 已列 |
| PROMPT-006 | execution binding 来源与稳定：managed session 创建即冻结；user-facing 追最近真实用户请求；`ExplicitExecutionOverride` 单次不冻底；不一致 fail-closed | `participant-identity` | 核心 WHY = binding 漂移不得冒充换人（identity RED）。override/fail-closed 发送海关与 `dispatch-protocol`/`provider-attempt-recovery` 共用机制，语义 owner 在 identity |
| PROMPT-007 | Fire-and-forget 只省调用方等待，不绕过 claim/authority/持久化/幂等/错误记录；禁 `postPromptFireAndForget` 旁路 | `dispatch-protocol` | dispatch-protocol OWNS detached 只改等待、不绕过 claim |
| PROMPT-009 | 来源解析优先级：accepted HostMessageId→claimed PromptKey→compaction/synthetic→AgentOwnerRoot→proven HumanRoot→UnknownOrigin | `interaction-authority` | provenance 决定权属 interaction-authority；PromptKey 匹配机制属 dispatch-protocol |
| PROMPT-010 | 禁自激励：合成/repair/review/synthetic/重试 不得抬权（Root、预算、SelectedAgent、FallbackOffset、默认 Agent、Model） | `interaction-authority` | interaction-authority RED = synthetic/unknown/continuation 冒充新 root |
| PROMPT-011 | 未决发送恢复：崩溃后靠 PromptKey 定位真实物理落地，未找到保持 Pending 不重发，预算耗尽 Abandoned | `dispatch-protocol` | dispatch-protocol OWNS「physical acceptance 只由真实物理证据建立 + uncertain 不重发 + at-most-one」。`RecoveryTailWindow=50`/`Budget=3`=HOW/GARBAGE 常数 |
| PROMPT-015 | Prompt Composition Protocol：World/Role/Library/Runtime/Mission 五层；层可告知不得冒充；冲突按语义所有权裁决，不设「更靠近 system 者胜」 | `cognitive-environment` | cognitive-environment OWNS 长期 cognition 语义层与 Runtime/Mission 边界。「Tools 非 Role 章节」=action-affordance 拥有 tool contract，cognitive 只引用 |
| PROMPT-016 | Office Library：知识≠权威；Class×Delivery×Audience 三轴；禁书扩权/bible/异书/隐藏编排/第二真源 | `cognitive-environment` | cognitive-environment OWNS `knowledge ≠ authority；craft 跨 authority 边界流动` |
| PROMPT-017 | `ProviderLanguage` 类型 + session 创建绑定不可变 + child 继承 + 全局偏好只影响未来 session + localizable/invariant 分类 | `provider-language` | provider-language OWNS 全文语义 |
| PROMPT-018 | Assistance continuation：NeedHelpEscalation/Advice 延长 LogicalRun、复用 authority/profile/system prompt/ToolCapabilitySet、不建 HumanRoot、不 reset cursor | `interaction-authority` | interaction-authority CURRENT EVIDENCE 已引 PROMPT-018；Cursor Pair Hint wire 附着→`provider-projection`，binding 换档→`provider-attempt-recovery` |
| PROMPT-019 | Provider-visible prose：Meaning→semantic owner、Language→session、Rendering→machinery；Class A/B/C；禁 TranslationRegistry/match lang；SyntheticToml 只布局转义 | `provider-language` | provider-language OWNS 三向分离与 A/B/C 分类；layout/escaping→`provider-projection`；meaning 分布→各 semantic owner |
| PROMPT-020 | Tool Affordance Law：act/时机/负边界/成功后果/参数意义五问；高风险 verb 最低集合 | `action-affordance` | action-affordance OWNS 调用瞬间局部 contract 五问 |
| PROMPT-021 | Critical Semantic Redundancy：关键区别出现在每个会改变行动的决策面；single ownership ≠ single presentation | `action-affordance` | action-affordance OWNS「canonical fact 可在多 decision boundary 镜像，多处呈现不产生多 ownership」 |

### NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| PROMPT-008 | 原子 AttemptExecutionProfile：Authority 子记录稳定 | `interaction-authority` | root/continuation authority 的原子载体 |
| PROMPT-008 | ToolCapabilitySet 同源：provider schema 与 execution gate 读同一 set；StrengthReplica 只 `{Read;Glob;Grep}` | `capability-enforcement` | capability-enforcement OWNS「schema 与 runtime gate 读同一 capability truth、request-specific 收窄」 |
| PROMPT-008 | `ProjectionChoice = UsePrefixProbe / UseCommittedEpoch` 仅对本次 attempt 有效 | `prefix-stability` | prefix epoch/candidate 分离；`provider-projection` 提供 intent 模型 |
| PROMPT-008 | ProviderRunIdentity 首次取得后 bind-once，下游同读 | `host-boundary` | 物理 identity 可信取得边界 |
| PROMPT-008 | AttemptExecutionProfile record layout 本身 | HOW | HANDOFF §18.4：integration structure，非未来 package；不拥有 record |
| PROMPT-013 | MagicTodoManagerGuideline 冻结语义（obligations 增删、checkpoint 连续、T1 首收、不伪造 Activation） | `obligation-ledger` | obligation-ledger OWNS Manager guideline + TODO-013/015 |
| PROMPT-013 | Manager 可见/禁止 surface：可看 PERFECT/REVISE outcome+report，禁看 reviewer 名/witness/cohort/hidden task | `participant-horizon` + `finality` | horizon OWNS admission；finality OWNS hidden terminal mechanism；GLORY-030/SURFACE-005 窄出口 |
| PROMPT-013 | 「禁止并入 host/pair-programming-guideline」 | `provider-projection` | presentation ownership；不造第二投影 |
| PROMPT-014 | SessionPersona 一次冻结不可变；Binding 变化 ≠ 换人；不得把 Binding 名冒充 Persona | `participant-identity` | identity OWNS Persona freeze + binding≠person（AGENT-028/029） |
| PROMPT-014 | office system prompt 同一 Life 内 byte-identical，不因 T1/Fallback/Strength/review/compaction/reanchor 改写 | `prefix-stability` | prefix-stability OWNS provider/model/system/tool 中参与 prefix identity 范围；CURRENT EVIDENCE 已引 PROMPT-014 |
| PROMPT-014 | SessionProviderLanguage session 创建绑定不可变 | `provider-language` | language bind-once |

### GARBAGE / HOW — 不进入未来 WHAT

| 内容 | 判定 | 说明 |
|---|---|---|
| PROMPT-012 全条（Student/Teacher、Learn→Compile、QA bootstrap 已删；编号永久空缺） | GARBAGE | migration absence ratchet；`student-teacher-absence.mjs` 属迁移 proof，基线稳定后删除。仅「插件 user-shaped message 仍经 PROMPT-005」保留并已归 dispatch-protocol |
| PROMPT-003「ManagerGuard 仅保留用于历史 journal 行解析」 | HOW | 兼容解析 note，非永久 requirement |
| PROMPT-011 `RecoveryTailWindow=50`/`RecoveryAttemptBudget=3` | HOW | dispatch-protocol DOES NOT OWN 精确常数；WHAT 只要求 bounded + no blind resend |
| PROMPT-008 `AttemptExecutionProfile` / `AuthorityExecutionProfile` record 字段集 | HOW | integration structure，见 HANDOFF §18.4 |
| PROMPT-019 Gate 0/Batch 迁文日程、ARCH-016 Gate E/C/F 引用 | HOW | 迁移日程与 gate 编号是当前 proof/Change 工作，非未来 WHAT |

---

# 本轮 delta

## Boundary delta

```text
UNCHANGED  45 包全部不变
SPLIT      无（PROMPT-008/013/014 的 NEEDS-SPLIT 已被现有包分解吸收，无需改包集合）
MERGED     无
NEW        无
REMOVED    无
```

## Coverage delta

```text
new OWNED    18 条单-owner 条款（PROMPT-001..007/009/010/011/015..021）
new NEEDS-SPLIT  3 条（PROMPT-008/013/014）→ 已分解进现有 owner，无新包
new GARBAGE  PROMPT-012（Student/Teacher absence）+ PROMPT-011 常数 + PROMPT-008 record layout
new ORPHAN   0
new OVERLAP  0（现行 prompt.md 的混合已在边界卡中记录；本轮把混合定位到 clause 级）
```

## Proof delta

```text
authority.test.mjs        → interaction-authority + dispatch-protocol（已知，见 PROOF-MAP）
prompt-stability.test.mjs → participant-identity + prefix-stability + provider-language 三方
fire-and-forget.test.mjs  → dispatch-protocol
student-teacher-absence   → DELETE（migration-only）
```

## Dependency delta

```text
无新增/删除 hard edge。
```

## 边界观察（watch，不立新包）

1. PROMPT-006 的 execution-binding 解析律归 `participant-identity`，但 send 海关（Preserve/ExplicitExecutionOverride/fail-closed）机制与 `dispatch-protocol`/`provider-attempt-recovery` 共用。当前语义 owner 唯一（identity），机制不构成第二个 WHY；若未来 dispatch 层要独立重写 binding 海关而不动 identity，需重审。
