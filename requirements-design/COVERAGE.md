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
| `agent.md`（含 shape AGENT-007/019/021、how 装配） | DONE | 32 条款；15 NEEDS-SPLIT、12 OWNED、5 GARBAGE、0 ORPHAN |
| `host.md`（含 shape HOST-003/008/011/012、how HOST-004/009/010） | DONE | 27 条款；11 NEEDS-SPLIT、15 OWNED、1 GARBAGE、0 ORPHAN |
| `companion.md`（含 shape COMPANION-009、how COMPANION-005） | DONE | 15 条款；3 NEEDS-SPLIT、12 OWNED、0 ORPHAN |
| `execution.md`（含 shape EXEC-009/014/023/024/026、how EXEC-010..013/018/019/025） | DONE | 32 条款；8 NEEDS-SPLIT、23 OWNED、1 GARBAGE、0 ORPHAN |

下一 topic：`persist.md`。

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

# prompt.md 轮 delta

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

---

# `docs/what/agent.md`（+ shape/agent.md + how/agent.md）

## OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| AGENT-003 | `peer(fast)=deep` / `peer(deep)=fast` 对称且启动可证明 | `participant-identity` | 绑定配对本体；`fast`/`deep` 名 = HOW；fallback 消费 peer → `provider-attempt-recovery` |
| AGENT-005 | 新公开 Authority Root 必须携准确 Agent；省略/旧名/build/plan → fail-closed | `interaction-authority` | HumanRoot provenance 要求显式 agent；精确 `fast-*` 名 = HOW |
| AGENT-006 | 能力矩阵（Role→工具投影表） | `capability-enforcement` | 矩阵 = enforcement projection；每格「entitled consequence」→ `office-capability`（ARCH-017）；工具名清单 = HOW |
| AGENT-007 | 双层边界：Host-final schema + ToolRegistry execution gate 都只读同一 `AttemptExecutionProfile` | `capability-enforcement` | capability-enforcement OWNS「schema 与 runtime gate 读同一 capability truth」 |
| AGENT-008 | Blogger/Distiller/Bookkeeper 不得出现在任何 provider-visible enum/schema | `participant-horizon` | horizon OWNS internal participant 不进入无资格 choice surface；enforcement → `capability-enforcement` |
| AGENT-010 | `permissions(fast-ROLE)=permissions(deep-ROLE)`；不得 fast 只读 deep 可写 | `capability-enforcement` | capability-enforcement OWNS「tier 不改变同 office authority」 |
| AGENT-017 | `mv` POSIX 语义（source/destination、覆盖、目录/跨文件系统） | `repository-programming` | 文件变换编程面语义 |
| AGENT-018 | `rm` POSIX 语义但禁删非空目录 | `repository-programming` | 文件变换编程面语义 |
| AGENT-019 | `external_directory="allow"` 每 managed agent 显式写入、唯一生产写点 | `capability-enforcement` | 唯一 enforcement 写点；`external_directory` 是 Host 路径边界机制 → `host-boundary` 交叉 |
| AGENT-027 | Semble 进程内搜索 = 低可信 orientation data，不是 repository fact/evidence | `repository-investigation` | 低可信 orientation → `knowledge-reuse` 交叉；stdio MCP/uvx/env = HOW |
| AGENT-028 | Persona Registry：`Role × initial tier → SessionPersona` 创建时一次冻结 | `participant-identity` | identity OWNS Persona freeze；具体 Persona display 名 = GARBAGE |
| AGENT-029 | `Role ≠ Persona ≠ ExecutionBinding`；Fallback/Strength 只改 Binding，Persona/system 字节不变 | `participant-identity` | identity 核心 ontology |

## NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| AGENT-001 | `Role` DU + `AgentTier`；Tier 只改 ExecutionBinding | `participant-identity` | Role=office identity，Tier=binding |
| AGENT-001 | Canonical Role 决定工具权限与 system prompt | `office-capability` | role→entitled consequence |
| AGENT-001 | Bookkeeper 保持 InternalLeaf + Attached，不进 public Role DU | `session-ontology` | InternalLeaf + Attached ontology |
| AGENT-009 | 示踪面可见集合（fork/commission/inspect/horizon 可见集合） | `participant-horizon` | 什么有资格被看见 |
| AGENT-009 | `fork` description 写五 Office entitled consequence + navigator/researcher 区分 | `action-affordance` | 调用方局部 contract + boundary mirror |
| AGENT-009 | fork/commission 可见面背后的委托语义 | `delegation` | fork/commission = delegation surface |
| AGENT-011 | Manager 无普通工具：不读文件/不跑终端/不改仓库/不 inspect | `office-capability` | Manager non-consequence |
| AGENT-011 | Manager 矩阵只有 fork/join/horizon/todowrite/fission/suicide | `capability-enforcement` | matrix projection |
| AGENT-012 | `inspect` description 把 Inspector 写成见证者，不写「第二双编辑的手」 | `action-affordance` | PROMPT-021 调用方 boundary mirror |
| AGENT-012 | Inspector 不得泄露 query-shell/取证权、不得当验证代理 | `office-capability` | Inspector consequence |
| AGENT-013 | 只有 DevOps 可 open/send/read/signal-terminal + run；修改只能经委派 | `office-capability` | DevOps terminal consequence |
| AGENT-013 | terminal 的物理 act/completion semantics | `process-execution` | 真实进程/PTY 语义 |
| AGENT-013 | 不向 provider 暴露 status/code/TIMED_OUT DTO；10s join 预算 = HOW | `participant-horizon` | 内部状态转 consequence |
| AGENT-014 | Reviewer 只读 + judge，不能写文件/跑命令 | `office-capability` | Reviewer consequence |
| AGENT-014 | Reviewer 矩阵 projection | `capability-enforcement` | matrix |
| AGENT-015 | Orchestrator 只 commission fast/deep-manager，不暴露 job id/worktree/reused | `office-capability` | Orchestrator consequence |
| AGENT-015 | `commission` = 委托（新路/按 Byname 续做） | `delegation` | commission semantics |
| AGENT-015 | 不暴露机器字段 | `participant-horizon` | admission filter |
| AGENT-016 | `mv`/`rm` 只进 Coder 矩阵，其它角色（含 DevOps）不得 | `office-capability` | Coder consequence |
| AGENT-016 | 双层 fail-closed 适用 | `capability-enforcement` | gate |
| AGENT-023 | `bash-honeypot` 仅 Coder；不执行 shell、只返越权拒绝；非放行 bash | `office-capability` | Coder consequence boundary |
| AGENT-023 | bash 对 managed role deny | `capability-enforcement` | gate |
| AGENT-024 | SyncDelegate DAG（Inquiry/Coder/DevOps→Inspector、DevOps→Coder）无环 + InvocationMode | `delegation` | 同步委派 topology |
| AGENT-024 | Dedicated Inspector/Coder = Work + Attached | `session-ontology` | HOST-008 execution class |
| AGENT-024 | callee 普通 completion 结束；Host 物化 bounded WorkRecord(includeOpening=false) | `work-record` | bounded canonical statement |
| AGENT-025 | Inquiry = reasoning（reason/question/compare/challenge/synthesize，经 Sphinx co-yield） | `epistemic-reasoning` | 认识状态求解，不是证据扫库 |
| AGENT-025 | Inspector = evidence acquisition；分层 Inquiry→Inspector | `repository-investigation` | 证据采集边界 |
| AGENT-025 | Inquiry 工具面 = {inspect, sphinx MCP}，禁止 filesystem 直读 | `capability-enforcement` | surface projection |
| AGENT-026 | Browser 的 network 能力 = public-web 事实建立 | `external-investigation` | Browser consequence |
| AGENT-026 | `ToolPermission.Network` → 仅 Browser allow `stealth-browser-mcp_*` | `capability-enforcement` | permission |
| AGENT-026 | stealth-browser MCP = Host 集成机制（uvx/ref/env 启动判定） | `host-boundary` | Host adapter HOW |
| AGENT-030 | Sphinx = 认识状态求解器（不是业务工具） | `epistemic-reasoning` | SPHINX-001..010 |
| AGENT-030 | `ToolPermission.Sphinx` → 仅 Inquiry allow `sphinx_*` | `capability-enforcement` | permission |
| AGENT-031 | 同 Session/LogicalRun/AuthorityRoot/Persona 上的 fast→deep escalation continuation | `interaction-authority` | authority continuity |
| AGENT-031 | deep 命中 → 真实 consultation child（freeze frontier、CommissionerRecord、advice 返回） | `delegation` | consultation = 委托 |
| AGENT-031 | Cursor Pair Hint 附着真实 terminal tool result，不造 synthetic role | `provider-projection` + `prefix-stability` | wire injection |
| AGENT-031 | Pair Hint 鼓励 `[NEEDHELP]` 的正常协作语义 | `cognitive-environment` | craft |
| AGENT-032 | Semble 搜索 hits = 低可信 hint，不是 instructions/proof/tool history | `knowledge-reuse` | low-trust cache |
| AGENT-032 | explicit keywords fresh search；不自动抽词、无 cross-call cache | `repository-investigation` | evidence acquisition 边界 |
| AGENT-032 | SyntheticToml 渲染 instruction/data 分界 | `provider-projection` | renderer |

## GARBAGE / HOW — 不进入未来 WHAT

| 内容 | 判定 | 说明 |
|---|---|---|
| AGENT-002 全条（必须恰好 22 个 agent、fast/deep 名、非空互异 model 串） | GARBAGE | exact catalog + machine names = implementation vocabulary；「缺一启动失败」是当前 runtime 契约。Bookkeeper 内部身份 → `session-ontology` |
| AGENT-004 全条（非法旧名清单：orchestrator/meditator/executor/student/teacher/裸名…） | GARBAGE | legacy reject ratchet = migration proof；`student-teacher-absence.mjs` 基线稳定后删 |
| AGENT-020（Student/Teacher 已删） | GARBAGE | migration absence |
| AGENT-021（Student request-specific 双门已删） | GARBAGE | migration absence |
| AGENT-022（Student SKILL 已删） | GARBAGE | migration absence |
| AGENT-026/030 MCP 的 uvx command、ref、env 前缀、fixture 启动判定 | HOW | Host adapter 机制，非产品 ontology |
| AGENT-032 `MaxKeywords=8`/`TopK=4`/`MaxHintsTotal=24`/`64 KiB` | HOW | tuning values（HANDOFF §12 已列） |
| AGENT-013 `join` 10s 等待预算 | HOW | 具体 budget 值；「有界等待」才是 WHAT |
| Persona display 名（Integrator/Director/Coordinator/…） | HOW | AGENT-028 表；除非命名本身是 public contract |

---

# agent.md 轮 delta

## Boundary delta

```text
UNCHANGED  45 包全部不变
SPLIT      无（15 条 NEEDS-SPLIT 均被现有包分解吸收）
MERGED     无
NEW        无
REMOVED    无
```

## Coverage delta

```text
new OWNED     12 条（AGENT-003/005/006/007/008/010/017/018/019/027/028/029）
new NEEDS-SPLIT  15 条（AGENT-001/009/011/012/013/014/015/016/023/024/025/026/030/031/032）
new GARBAGE   AGENT-002（exact catalog）、AGENT-004（legacy names）、AGENT-020/021/022（Student absence）
new ORPHAN   0
new OVERLAP  0（clause 级已定位）
```

## Proof delta

```text
agent-permission-gate.test.mjs  → capability-enforcement
capability-isomorphism-gate.mjs → capability-enforcement
session-ownership-matrix/ratchet → session-ontology
semantic-anchors.mjs / prompt-semantic-depth → 拆 cognitive-environment + office-capability + action-affordance（已知）
student-teacher-absence.mjs      → DELETE（migration-only）
```

## Dependency delta

```text
无新增/删除 hard edge。
```

## 边界观察

1. AGENT-031（NEEDHELP）继续维持 WATCH：四类 guarantee（interaction-authority / delegation / provider-projection+prefix-stability / cognitive-environment）已能组合解释，本轮未发现独立 WHY → 不立 `collaboration-guidance`（HANDOFF §10.2 维持）。
2. agent.md 的「office consequence + capability matrix」双层（如 AGENT-011/013/016）确认 §7.1 person/office/execution 边界：consequence 属 `office-capability`，matrix/gate 属 `capability-enforcement`，二者不同 WHY。

---

# `docs/what/host.md`（+ shape/host.md + how/host.md）

## OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| HOST-001 | 事件分层：业务层不消费流式碎片，只碎片→粗粒度信号→single-flight→SDK 完整消息→纯策略 | `host-boundary` | host-boundary OWNS「stream fragment 只可作窄传感器输入，不能积分成业务真相」 |
| HOST-002 | 仅 idle/retry/abort/deleted 信号可入生命周期与 Reconciler；abort 解码 typed `AttemptAborted` 不推进 fallback；`TurnUnknown` 是私有观测非 `TurnOutcome` | `host-boundary` | 信号 admission + observation boundary。无 PromptKey 用户消息 signal JoinAttempt → `dispatch-protocol` 交叉 |
| HOST-003 | `Transport ≠ Domain`：typed `HostSignal`，业务不观察 raw payload | `host-boundary` | Host capability contract |
| HOST-005 | XTrace 是唯一 append-only 原始语义轨迹：含 prompt/assistant/reasoning/tool/omission，不含 UI delta/usage/timestamp；provenance 按 provider run 分段 | `semantic-trace` | semantic-trace OWNS append-only semantic history + typed capture boundary |
| HOST-007 | 日志只记诊断；禁写 stage/phase/owner/lease/generation 当控制状态 | `crash-reconciliation` | 日志≠恢复协议；「状态标签只允许物理/领域事实」→ `structured-workflow` 交叉 |
| HOST-008 | Session 关联所有权：`ExecutionClass(Work|InternalLeaf) × AttachmentKind` 正交；durable association 为事实、派生视图 | `session-ontology` | session-ontology OWNS 正交分类轴。`TeacherSessionId` absent → GARBAGE |
| HOST-009 | Attached 创建/复用/Replacement/retire 唯一 owner；重启按 journal 关联匹配，无关联新建，冲突 fail-closed | `managed-session-lifecycle` | lifecycle OWNS create/reuse/replacement。dispose 杀 PTY → `process-execution` 交叉 |
| HOST-011 | Tool 身份两个半边：ToolContext 有 message+call id，before/after 只有 call id；缺一 fail closed | `host-boundary` | provider/tool 物理 identity 取得边界 |
| HOST-012 | 多实例共享 vs 每实例独有；共享表单事件循环同步片段、禁跨 await RMW | `host-boundary` | Host 按 directory 实例化的边界契约 |
| HOST-016 | 空 Content 预防：assistant/user 无 text 时用 reasoning/占位填充，避免上游 400 | `host-boundary` | Host adapter sanitization |
| HOST-022 | physical-success 双路径 Accepted：live after ∨ recovery completed ToolPart，收敛同一 id+digest | `effect-accounting` | effect-accounting OWNS Requested/物理发生/Accepted 分型 |
| HOST-023 | reviewing sink 决策与 reconciliation：sink=compatibility projection，canonical=obligations；REVISE 消费后幂等投影 | `obligation-ledger` | canonical obligation truth；review 交叉 → `review-assurance` |
| HOST-024 | V2 runner fail-closed：无 proven definition+before+after 则 Attempt construction 拒绝 | `obligation-ledger` | admission gate；canary 证明 → `verification-system` |
| HOST-025 | sessionID+callID 经 SDK 快照唯一定位 ToolPart/assistant/run/ordinal/XTrace；不能唯一 → fail closed | `host-boundary` | Host SDK 定位 canary |
| HOST-026 | `SessionProviderLanguage` 创建瞬间绑定不可变；child 继承；事后改全局只影响新 session | `provider-language` | provider-language OWNS bind-once + inheritance |

## NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| HOST-004 | `QuiescencePermit`：idle-derived 副作用准入，process-local、不写 Journal、不参与 crash recovery；观测稳定≠静止资格 | `causal-wait` | wait observation 非权威 + 重启安全消失 |
| HOST-004 | Reconciler single-flight/dirty/有界因果重读/`TurnOutcome` 分类（`TurnUnknown` 私有） | `host-boundary` | Host 快照观测 machinery |
| HOST-006 | `ContextReanchored`：`PrefixEpoch+1`、`Snapshot=None`、`PrefixCoverage` 归零 | `prefix-stability` | prefix epoch/cold boundary |
| HOST-006 | Host compaction 不得当恢复失败/容量信号；唯一合法恢复是失败驱动协议 | `context-compression` | context pressure recovery policy |
| HOST-006 | compaction 观测 gate（关闭自动/overflow/prune，否则启动失败） | `host-boundary` | Host compaction observation |
| HOST-010 | Transform → ProviderRunIdentity 因果读唯一未完成 assistant，用于 ReviewSeal | `review-assurance` | witness identity binding |
| HOST-010 | 唯一性靠单 actor 写 assistant；命中 0/≥2 → 不写 seal fail-closed | `host-boundary` | Host 观测唯一性 + causal-read |
| HOST-013 | durable anchored pair 序列（append-only、Ordinal/CallId/Gap、幂等 replay） | `prefix-stability` | append-only prefix law；substrate → `durable-events` |
| HOST-013 | renderer（ordinary completed auto-injected；Cursor `NUL+BOM` suffix） | `provider-projection` | deterministic wire renderer |
| HOST-013 | Pair Hint 正文 craft payload（中文思考/NEEDHELP 正常/parallel wave） | `cognitive-environment` | craft content |
| HOST-013 | occurrence + tip nudge 语义 | `guidance-delivery` | occurrence/coverage/dedupe |
| HOST-013 | `SessionStartedAt → now` wall-clock elapsed（`IClockPort`，禁 ambient） | `time-capability` | 时间显式 capability |
| HOST-015 | 物理 parent 恒为 family root（深度 2）；逻辑归属只由 journal 关联承载 | `session-ontology` | 物理 topology ≠ logical ownership |
| HOST-015 | 重启按 journal 关联匹配复用/新建，冲突 fail closed | `managed-session-lifecycle` | restore matching |
| HOST-017 | Magic Todo canonical/checkpoint/review/Finality 语义；Host 不改本体、不覆盖 builtin、sink 降级 compatibility | `obligation-ledger` | canonical todo protocol |
| HOST-017 | `TodoWriteAccepted` 必须 physical success | `effect-accounting` | Accepted 分型 |
| HOST-017 | V1 三钩子 overlay 可观察合同与 canary | `host-boundary` | Host membrane 合同 |
| HOST-018 | `tool.definition` 三处同步（parameters/jsonSchema/description） | `obligation-ledger` | V2 schema 唯一广告点 |
| HOST-018 | description 覆盖 Manager 可见纪律 | `action-affordance` | 调用合同 |
| HOST-018 | description 禁泄露 hidden reviewer/cohort/barrier | `participant-horizon` | hidden orchestration 不进 horizon |
| HOST-019 | pending+`{}` 不得降级当输入；deferred prepare 等 materialize、digest 对齐 | `obligation-ledger` | admission canonical |
| HOST-019 | before 可能落在 pending/`{}` 与 final input 之间（Hook 时序 barrier） | `host-boundary` | Host hook 行为 |
| HOST-020 | decode obligations + durable `TodoWritePrepared`（尚非 checkpoint） | `obligation-ledger` | admission |
| HOST-020 | executor 只观察原地 mutation、non-enumerable compatibility 投影 | `host-boundary` | Host hook 语义 |
| HOST-021 | after 顺序：Accepted → ensureReview → 富化 result（ConsumableReview LWR、merge preview） | `obligation-ledger` | todo protocol |
| HOST-021 | Accepted 必须 physical success（幂等 live/recovery） | `effect-accounting` | Accepted 分型 |
| HOST-021 | 富化 result 消费上次 `ConsumableReview` | `review-assurance` | judgement 可消费 |
| HOST-027 | Host 只从 reasoning delta 识别 sentinel `[NEEDHELP]`，rolling suffix，每 run 一次 | `host-boundary` | reasoning sensor |
| HOST-027 | assistance abort 不是 ProviderFailure/LoopKill，不推进 fallback/retry budget | `interaction-authority` | authority continuity |
| HOST-027 | abort cause 分离（assistance vs loop vs provider failure，一个 abort 一个 cause） | `degeneration-guard` | detector 边界 |
| HOST-027 | deep 命中 → 真实 consultation child + advice 返回 | `delegation` | consultation = 委托 |

## GARBAGE / HOW — 不进入未来 WHAT

| 内容 | 判定 | 说明 |
|---|---|---|
| HOST-014 全条（Student/Teacher Host 行为、QA bootstrap、teacher 双 await、Learn/Compile nudge） | GARBAGE | migration absence ratchet；`student-teacher-absence.mjs` 基线稳定后删 |
| HOST-008 `TeacherSessionId`/`StudentTeacherLinked`/`SatelliteKind` 历史字段 | GARBAGE | 历史过渡字段，G3 gone |
| HOST-010 OpenCode 源码锚定引理（commit e024e2ef、`session/prompt.ts` 行号） | HOW | Host source canary，绑定具体版本；「因果读唯一」才是 WHAT |
| HOST-013 `NUL+BOM` 分隔符、`source="pair-programming-auto-injected"`、`auto-injected` 工具名 | HOW | wire representation 机制 |
| HOST-004 `rereadsRemaining = maxCausalRereads + 1`、3 次因果重读 | HOW | 具体预算值；「有界因果重读」才是 WHAT |

---

# host.md 轮 delta

## Boundary delta

```text
UNCHANGED  45 包全部不变
SPLIT      无（11 条 NEEDS-SPLIT 均被现有包分解吸收）
MERGED     无
NEW        无
REMOVED    无
```

## Coverage delta

```text
new OWNED     15 条（HOST-001/002/003/005/007/008/009/011/012/016/022/023/024/025/026）
new NEEDS-SPLIT  11 条（HOST-004/006/010/013/015/017/018/019/020/021/027）
new GARBAGE   HOST-014（Student/Teacher absence）+ HOST-008 历史字段
new ORPHAN   0
new OVERLAP  0（clause 级已定位）
```

## Proof delta

```text
provider-leak-gate / horizon-surface → participant-horizon（已知）
host canaries / transform snapshot    → host-boundary
session-ownership-matrix / ratchet    → session-ontology
prompt-stability（prefix byte）       → prefix-stability
MagicTodo membrane canary D..G        → obligation-ledger + effect-accounting + host-boundary（拆 oracle）
```

## Dependency delta

```text
无新增/删除 hard edge。
```

## 边界观察

1. HOST-013（结对编程 marker）是 host.md 最重混合：prefix-stability（anchored append-only）+ provider-projection（renderer）+ cognitive-environment（craft 正文）+ guidance-delivery（occurrence/nudge）+ time-capability（elapsed）。五个 WHY 均已有 owner，不立新包；`NUL+BOM`、`auto-injected` 等 wire 机制判 HOW。
2. HOST-017..025（Magic Todo membrane）确认 HANDOFF §13.4：membrane 不是单包，canonical → `obligation-ledger`，Accepted → `effect-accounting`，review 可消费 → `review-assurance`，description → `action-affordance`，hidden 编排 → `participant-horizon`，SDK 定位 canary → `host-boundary`。
3. HOST-027（NEEDHELP sensor）与 AGENT-031 同构，继续维持 WATCH：interaction-authority + delegation + degeneration-guard + host-boundary 已能组合解释，不立 `collaboration-guidance`。

---

# `docs/what/companion.md`（+ shape/companion.md + how/companion.md）

## OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| COMPANION-001 | 每个 Work Session X 恰好一个叶子 Companion Y；与 Role/Tier/工具面/LogicalRun/Authority/Fallback 无关 | `session-ontology` | topology；不是 role eligibility（HANDOFF §11.1） |
| COMPANION-002 | X → 恰好一个 Y，Y 不再递归，深度恒 1 | `session-ontology` | topology |
| COMPANION-004 | Y 的 system 唯一来源 = PromptResources 组合 Blogger Role Law；fast/deep 同 prompt、同 `chronicle` 面 | `cognitive-environment` | Role Law 组合；同 office 跨 tier → `participant-identity`；禁动态 token → `context-compression` |
| COMPANION-005 | BlogFrame（Entry/Squash，无 Seed）投影；frame 正文 = 纯工作记录 | `context-compression` | Y frame = 压缩表示；BlobRef → `durable-events` |
| COMPANION-006 | squash 只处理本 X frames，不混父 context | `context-compression` | compression 操作 |
| COMPANION-009 | 同一 PrefixEpoch 内前缀逐字节稳定；epoch 切换仅 probe/reanchor/TodoCheckpoint 三证据源 | `prefix-stability` | append-only prefix law + cold boundary |
| COMPANION-010 | FrozenRecordPrefix 明确标记 context block 注入，不伪装指令，同 epoch 冻结 | `prefix-stability` | frozen prefix；「不伪装 instruction」→ `provider-projection` |
| COMPANION-011 | Cutoff 只在完整 semantic turn 边界；投影前重算 digest，失配 fail closed | `prefix-stability` | cutoff boundary；digest 失配不是 compaction 善后 |
| COMPANION-012 | 缓存比较只用进模型字段（排除 timestamp/cost/usage/runtimeId） | `provider-projection` | semantic ≠ wire equality |
| COMPANION-013 | Synthetic id 由 SealRoot/frameEpoch/ordinal 确定性派生；禁 GUID/random/时间 | `prefix-stability` | 稳定 synthetic identity；promote 继承 SealRoot |
| COMPANION-014 | OpeningMaterial = preserved XTrace `[work start, OpeningBoundary)`；禁止 Assignment/requirements 重建；关闭永不移动；always raw | `work-record` | work-record OWNS「Opening preserved，不从 Assignment 重建」 |
| COMPANION-015 | WorkRecord 十条不变量（①..⑩）；Sync/Async 同一协议；prose claim 无固定 schema | `work-record` | work-record 核心 OWNS 全部 |

## NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| COMPANION-003 | WorkRecord 三段（Opening/Chronicle/Recent work）+ prose claim + 无 Closing report | `work-record` | bounded canonical statement |
| COMPANION-003 | XTrace = X 唯一原始语义轨迹；Strength Candidate 永不入迹 | `semantic-trace` | canonical history capture |
| COMPANION-003 | RecordCoverage（XTrace 游标）与 PrefixCoverage（完整 turn 边界）分型；CoverableRecordPrefix 才可 rebase | `prefix-stability` | 两种证明量纲分离 |
| COMPANION-007 | 送 Y 的 delta 可含 tool 作压缩输入 | `context-compression` | compression 输入投影 |
| COMPANION-007 | LWR gap 剔 raw tool | `work-record` | LWR gap 表示 |
| COMPANION-007 | canonical digest 用 Semantic projection，禁反向解析 TOML | `provider-projection` | semantic ≠ wire |
| COMPANION-008 | BlogEntryCommitted 原子推进 frame 可见性与 RecordCoverage；busy/失败不推进 | `context-compression` | commit boundary |
| COMPANION-008 | RecordCoverage = LWR gap 起点 | `work-record` | coverage 分型 |
| COMPANION-008 | Host compaction 只作废 PrefixCoverage，不得清零 RecordCoverage/Frames | `prefix-stability` | reanchor 边界 |

## GARBAGE / HOW — 不进入未来 WHAT

| 内容 | 判定 | 说明 |
|---|---|---|
| 旧标题 `Opening task`/`Work log`/`Uncompressed tail`/`Final output` 已删 | GARBAGE | absence ratchet；`Closing report` DTO 删除同 |
| `OpeningPromptRaw = { AssignmentText; AuthoritativeRequirements }` 拼接重建 | GARBAGE | legacy blob；由 preserved XTrace interval 取代 |
| `[[do_not_exec]] historic_frame` 消息层渲染、`#` 由 SyntheticToml.comment 注入 | HOW | wire rendering 机制 |
| COMPANION-005 instruction header 不进 200 KiB chunk / frame blob | HOW | 具体容量值（HANDOFF §12） |

---

# companion.md 轮 delta

## Boundary delta

```text
UNCHANGED  45 包全部不变
SPLIT      无（3 条 NEEDS-SPLIT 均被现有包分解吸收）
MERGED     无
NEW        无
REMOVED    无
```

## Coverage delta

```text
new OWNED     12 条（COMPANION-001/002/004/005/006/009/010/011/012/013/014/015）
new NEEDS-SPLIT  3 条（COMPANION-003/007/008）
new GARBAGE   旧标题 + OpeningPromptRaw（legacy blob）
new ORPHAN   0
new OVERLAP  0（clause 级已定位）
```

## Proof delta

```text
canonical LWR materializer/proofs → work-record
XTrace append/capture/frontier    → semantic-trace
prompt-stability（prefix byte）    → prefix-stability
```

## Dependency delta

```text
无新增/删除 hard edge。
```

## 边界观察

1. companion.md 是 §11.1「Companion 不是永久 ontology」的直接证据：15 条 COMPANION 无一条需要独立 `companion` package；topology→`session-ontology`、frame/squash→`context-compression`、XTrace→`semantic-trace`、WorkRecord→`work-record`、prefix→`prefix-stability`。未来 deterministic in-process summarizer 替代 physical Blogger leaf 时这些 WHAT 均不变。

---

# `docs/what/execution.md`（+ shape/execution.md + how/execution.md）

## OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| EXEC-001 | Fork/Join/Horizon + 终端 + commission 工具面角色表 | `capability-enforcement` | 工具面 projection；fork/commission 语义→`delegation`，office 后果→`office-capability` |
| EXEC-002 | Fork 语义：calling/name/charge；续做按 Byname；不暴露 AgentId/reuse/agent_id/role/tier | `delegation` | fork = mission witness；machine topology 不进 contract |
| EXEC-003 | 终端四动词四合同（open/send/read/signal）；不返回 pty_id/closed/status | `process-execution` | 真实终端 act/observation 分型 |
| EXEC-006 | Child Run 生命周期与父背景记录分离 | `managed-session-lifecycle` | child lifecycle |
| EXEC-007 | Nudge 是 Continuation，不建新 Authority | `interaction-authority` | continuation 语义 |
| EXEC-008 | 父背景记录不冒充 child completion | `work-record` | record honesty |
| EXEC-009 | Handle 四态（Active/CompletedAwaitingJoin/Abandoned/Retired）；tombstone 不可回退 | `managed-session-lifecycle` | handle lifecycle；消费唯一 |
| EXEC-010 | Process Request 类型化 | `process-execution` | typed process request |
| EXEC-011 | Process Deadline 有界；超时确定失败路径 | `process-execution` | bounded deadline；时间→`time-capability` |
| EXEC-012 | 大输出摘要：超限走摘要，不静默截断成成功空结果 | `output-distillation` | fragment 不能冒充整体成功 |
| EXEC-013 | Large Gate 与输出预算合同一致；禁无界缓冲 | `output-distillation` | output budget |
| EXEC-015 | PTY completion 只由 backend onExit；禁 stdout 启发式 | `process-execution` | physical completion 由 backend fact 建立 |
| EXEC-018 | Join 批次：MaxJoinBatch=32、稳定排序、逐项 CAS、非确定序禁 | `delegation` | bounded batch；`MaxJoinBatch=32`=HOW |
| EXEC-019 | Orchestrator commission 批量 join（FIFO 排空上限 32） | `delegation` | commission join |
| EXEC-020 | Agent 终态代数 `Completed\|Failed\|Abandoned`；ABORTED 非终态，取消是控制面 | `effect-accounting` | outcome 分型；控制面/数据面→`structured-workflow` |
| EXEC-021 | completion blob v2：finality 仅 completed\|failed；`LegacyFalseAbort` 永不 RunCompletion | `effect-accounting` | outcome 分型；schemaVersion=HOW |
| EXEC-022 | 假 completion 补偿：`HandleFalseCompletionRejected`→确定性 replacement；禁把假 abort 洗成成功 | `effect-accounting` | unknown≠success；reconcile→`crash-reconciliation` |
| EXEC-023 | 恢复所有权与线性序：permit→join，禁跳步；Distiller 定向等待受 permit 门 | `crash-reconciliation` | restart 从 durable facts 重入普通程序 |
| EXEC-024 | Mailbox 双通道：agent 路径 Pulse（读 Journal）、PTY 路径 PublishPty | `process-execution` | 完成事实双通道；Journal→`durable-events` |
| EXEC-025 | DevOps Join 超时（10s）→ `DeadlineExpired` 自然语言；Manager/Orchestrator 无限期 | `time-capability` | bounded deadline；10s=HOW |
| EXEC-029 | Commission 语义：Orchestrator 委托独立集成之路给 Manager；不暴露 job_id/worktree/reused | `delegation` | commission = independent road |
| EXEC-030 | Provider leak 禁令：机器拓扑不进 provider；穿过 horizon 的只有后果与 WorkRecord | `participant-horizon` | admission filter |
| EXEC-032 | RepositoryWarmStart invocation timing：batch admission 后、首 send 前 prepare；不另发 late hints | `knowledge-reuse` | warm-start 时序；Semble→`repository-investigation` |

## NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| EXEC-004 | Join 消费 owner 可用 completion、有界批次 | `delegation` | join semantics |
| EXEC-004 | agent 完成项 = entry-local WorkRecord（includeOpening=false），禁字段式 DTO | `work-record` | bounded statement |
| EXEC-004 | 禁投影 status/count/ordinal/kind/agent/code/message | `participant-horizon` | no DTO |
| EXEC-004 | DevOps 10s 等待预算 → `DeadlineExpired` | `time-capability` | deadline |
| EXEC-005 | horizon = pull-only snapshot（在场名册 Byname/TerminalName），禁 timer/watcher | `delegation` | roster semantics |
| EXEC-005 | 最新 BlogFrame 作 child 工作记录；blob 缺失/digest 无效 fail closed | `context-compression` | frame source |
| EXEC-005 | 无 status/id/kind/ordinal | `participant-horizon` | no DTO |
| EXEC-014 | Distiller 映射子会话是私有 runtime，不暴露公开 fork/horizon | `output-distillation` | Distiller office |
| EXEC-014 | durable handle `HostOwnedHidden`，对 list/join/horizon/guard/恢复不可见 | `managed-session-lifecycle` | hidden handle |
| EXEC-014 | 机器 Assignment（map/reduce/chunk/session id）不进 provider 工具面 | `participant-horizon` | hidden surface |
| EXEC-016 | 有 join 义务且 outstanding 后台时只发 JoinGuard Continuation | `interaction-authority` | continuation |
| EXEC-016 | finality 处理停放，Manager 不做 idle 鼓励 | `finality` | drain rule |
| EXEC-017 | join 中断 = `JoinWaitOutcome.Interrupted`，不是 ForkError | `delegation` | interrupt semantics |
| EXEC-017 | external-user ingress 只打断当前 wait，不授予 Prompt authority、不 cancel child | `interaction-authority` | authority boundary |
| EXEC-017 | operator abort → TurnAborted cleanup 取消父全部 running sub-session | `managed-session-lifecycle` | cascade cancel |
| EXEC-026 | SatelliteRuntime/SyncDelegateRuntime 拥有 create/reuse/abort/retire/级联 | `managed-session-lifecycle` | runtime ownership |
| EXEC-026 | Dedicated Inspector/Coder = Work + Attached，非 Teacher-style InternalLeaf | `session-ontology` | HOST-008 |
| EXEC-026 | sync batch 成员/顺序/canonical/serialization 合同 | `delegation` | sync delegate |
| EXEC-028 | OneShot dispose-after vs Reusable SyncDelegate 两条互斥生命周期 | `managed-session-lifecycle` | lifecycle |
| EXEC-028 | 成功 wire = child LWR（includeOpening=false）+ 末条 TurnFormalText | `work-record` | bounded record |
| EXEC-028 | 同步返回语义（canonical/sibling/答案=WorkRecord 本身） | `delegation` | sync semantics |
| EXEC-031 | SyncDelegate 无 return：ordinary completion 结束 batch，Host 物化 bounded WorkRecord | `delegation` | sync delegate contract |
| EXEC-031 | 答案 = bounded WorkRecord 本身；最后一条助手文本在 Recent work；无 answer 字段 | `work-record` | prose claim |

## GARBAGE / HOW — 不进入未来 WHAT

| 内容 | 判定 | 说明 |
|---|---|---|
| EXEC-027 全条（Student Learn/Compile、teacher、StudentQaStore、SKILL、return 已删） | GARBAGE | migration absence ratchet |
| Meditator/Executor、fork-manager/list/verdict/blog/executor(工具)/fork-pty/return 已删算法面 | GARBAGE | GrandRewrite absence；`student-teacher-absence` / legacy gates 基线稳定后删 |
| `MaxJoinBatch=32`、`DevOpsJoinTimeoutMs=10_000` | HOW | 具体预算值；「有界批次/有界等待」才是 WHAT |
| completion blob `schemaVersion=2`、`fromDecoded` 唯一构造 | HOW | 编码机制 |

---

# execution.md 轮 delta

## Boundary delta

```text
UNCHANGED  45 包全部不变
SPLIT      无（8 条 NEEDS-SPLIT 均被现有包分解吸收）
MERGED     无
NEW        无
REMOVED    无
```

## Coverage delta

```text
new OWNED     23 条（EXEC-001/002/003/006/007/008/009/010/011/012/013/015/018/019/020/021/022/023/024/025/029/030/032）
new NEEDS-SPLIT  8 条（EXEC-004/005/014/016/017/026/028/031）
new GARBAGE   EXEC-027（Student absence）+ 已删算法面（Meditator/Executor/return 等）
new ORPHAN   0
new OVERLAP  0（clause 级已定位）
```

## Proof delta

```text
PTY/process run/signal/onExit  → process-execution
Distiller fragment-humility     → output-distillation
fork/commission/sync-delegate   → delegation
canonical LWR materializer       → work-record
```

## Dependency delta

```text
无新增/删除 hard edge。
```

## 边界观察

1. execution.md 的 SyncDelegate 条款（EXEC-026/028/031）确认「同步委派 = delegation + work-record + managed-session-lifecycle」三方：delegation 拥有语义 batch/canonical/serialization，work-record 拥有 bounded WorkRecord = 答案，managed-session-lifecycle 拥有 reusable vs dispose-after 生命周期。无独立 `sync-delegate` package。
2. EXEC-004/005/014/030 的「禁止 DTO / 机器拓扑穿过 provider」反复确认 `participant-horizon` 是 execution 面所有 leak 禁令的 owner，不是每个工具的附属字段。
