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
| `persist.md`（含 shape PERSIST-006、how PERSIST-007/009） | DONE | 11 条款；1 NEEDS-SPLIT、9 OWNED、1 GARBAGE、0 ORPHAN |
| `context.md`（含 shape CTX-007/008、how CTX-011/012） | DONE | 16 条款；3 NEEDS-SPLIT、13 OWNED、0 ORPHAN |
| `review.md`（含 shape REVIEW-006/007/010/012、how REVIEW-004/005） | DONE | 20 条款；2 NEEDS-SPLIT、18 OWNED、0 ORPHAN |
| `todo.md`（TODO-001..015；shape/how 无独立条款） | DONE | 15 条款；7 NEEDS-SPLIT、8 OWNED、0 ORPHAN |
| `glory.md`（GLORY-001..076 + SURFACE-001..006） | DONE | 82 条款；分组覆盖：finality 主导 + 8 跨域 + legacy GARBAGE，0 ORPHAN |
| `enforcer.md`（ENFORCER-*，24 条款） | DONE | behavior-diagnosis + guidance-delivery 主导；2 GARBAGE |
| `casebook.md`（CASE-001..012） | DONE | knowledge-reuse 主导 |
| `strength.md`（STRENGTH-001..012） | DONE | speculative-investigation 主导 |
| `sphinx.md`（SPHINX-001..010） | DONE | epistemic-reasoning 主导 |
| `js-tools.md`（JS-001..020） | DONE | repository-programming 主导 |
| `fallback.md`（FALLBACK-*，9 条款） | DONE | provider-attempt-recovery 主导 |
| `architecture.md`（ARCH-002..017） | DONE | 多域：host-boundary/prefix-stability/office-capability/participant-horizon 等 |
| `flow.md`（FLOW-*，5 条款） | DONE | structured-workflow |
| `loop.md`（LOOP-*，4 条款） | DONE | degeneration-guard |
| `orchestrator.md`（ORCH-*，4 条款） | DONE | change-integration |
| `projection.md`（PROJ-*，9 条款） | DONE | provider-projection |
| `dsl-structured-program.md`（DSL-001..015） | DONE | structured-workflow + causal-wait |
| `document-governance.md`（GOV-001..012） | DONE | requirement-system + verification-system；当前 5-layer = HOW |
| `synthetic-toml.md` / `glossary.md` | DONE | 导航/路由，无独立条款 |

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

---

# `docs/what/persist.md`（+ shape/persist.md + how/persist.md）

## OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| PERSIST-001 | EventEnvelope 版本无关；additive event_type vocabulary；canonical JSON 是 identity 协议 | `durable-events` | immutable fact + stable identity + canonical bytes |
| PERSIST-002 | Append/Publish 以 `refs/wanxiang/store` CAS 为唯一提交原语；无部分写入 | `durable-events` | append/publish atomicity |
| PERSIST-004 | 任一 event 校验失败 → 拒绝构建投影/启动；禁跳坏对象继续 fold | `durable-events` | corrupt history fail closed |
| PERSIST-005 | 无 schema/store/migration generation；旧 NDJSON/blobs leave-unread、不迁不读 | `durable-events` | additive vocabulary；legacy leave-unread = migration |
| PERSIST-006 | Domain/Persist/GitGateway/AgentJournal 分层所有权；单一 durable 边界 | `durable-events` | 单一 substrate；分层→`structured-workflow` |
| PERSIST-007 | PayloadRef 经 Git raw blob；committed root `payloads/` = payload_refs 并集；dangling → StorageInvalid | `durable-events` | payload closure |
| PERSIST-008 | Projection 查询 O(1) 积分不扫全历史；投影非第二真相源 | `durable-events` | query from projection |
| PERSIST-009 | Durable Effect：Requested/Claimed → Accepted/Created/Published + Reconcile；unknown 不自动重试 | `effect-accounting` | effect-accounting 核心（HANDOFF §7.5） |
| PERSIST-010 | 上下文恢复 fold 不变量所有者：OpeningPrompt/XTrace/BlogEntry/Terminal/BlogSquash/PrefixRebase/ContextReanchored | `durable-events` | fold invariant owner；各 fact 语义→respective owner（prefix-stability/semantic-trace/work-record/obligation-ledger/speculative-investigation） |

## NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| PERSIST-003 | 提交结局 durable witness = canonical root；StorageInvalid fail closed | `durable-events` | fail closed |
| PERSIST-003 | DomainConflict 非 StorageInvalid：保留 competing heads，resolution event 收敛 | `durable-convergence` | 并发分叉显式冲突，非 LWW |

## GARBAGE / HOW — 不进入未来 WHAT

| 内容 | 判定 | 说明 |
|---|---|---|
| PERSIST-011 全条（StudentQaStore / QA.md / Student QA 权威文件已删） | GARBAGE | migration absence ratchet |
| 旧 NDJSON `wanxiangshu-next`/`*.ndjson`、目录 `blobs/`、`Boot.fs`、`createFromBoot` | GARBAGE | leave-unread clean-break；`unified-store-gate` dual-write/no-migrator 是迁移 proof |
| `events/<hex-prefix>/<EventId>.jsonl` 分片路径 | HOW | 物理布局 |

---

# persist.md 轮 delta

```text
Boundary: UNCHANGED 45；无新/拆/并/删。
Coverage: 9 OWNED、1 NEEDS-SPLIT（PERSIST-003）、1 GARBAGE（PERSIST-011）、0 ORPHAN。
Proof: EventStore append/publish/fold/corruption → durable-events；PERSIST-009 → effect-accounting；unified-store-gate dual-write/no-migrator → migration-only。
Dependency: 无新增/删除。
```

## 边界观察

1. PERSIST-009 是 §7.5「durable-events ≠ effect-accounting」的直接证据：EventStore 不拥有 Requested/Accepted/unknown 语义，PERSIST-009 属 `effect-accounting`。
2. PERSIST-010 是 fold 不变量 owner，但其承载的每类 fact（XTrace/BlogEntry/PrefixRebase/Todo/Strength）语义分属各自 package；fold 只拥有「不满足即拒绝」的完整性规则。

---

# `docs/what/context.md`（+ shape/context.md + how/context.md）

核心裁决「不预测，只恢复」→ `context-compression`。

## OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| CTX-001 | 不观察容量：禁读/查/推导模型窗口大小，禁 tokenizer/换算 | `context-compression` | 不把窗口估算当产品真相 |
| CTX-002 | 不主动预测溢出：请求前不判断「是否近上限」，真实失败是唯一触发 | `context-compression` | failure-driven |
| CTX-003 | 最低环境合同：≥200 KiB provider-visible 动态输入 | `context-compression` | 输入合同；200 KiB = HOW |
| CTX-004 | 输出预算属 provider：不算 squash token/压缩比 | `context-compression` | bounded output contract |
| CTX-005 | 失败不分类：只看 Outcome，不按错误文字分叉 | `context-compression` | 不靠 error prose 分类 |
| CTX-006 | 恢复槽 = armed∧primed∧hasMaterial；是机会不是必然压缩 | `context-compression` | armed/primed 由 fallback→`provider-attempt-recovery` |
| CTX-007 | 按 RequestKind 分派结局：同 RequestKind 同结局同一分派，禁错误字符串分叉 | `context-compression` | 穷尽分派→`structured-workflow` |
| CTX-008 | 恢复槽失败仍走 Fallback 连续失败计数；不为压缩另造预算 | `provider-attempt-recovery` | 单一失败预算；recovery slot→`context-compression` |
| CTX-009 | X 不发压缩请求：压缩只在 Y squash 或 X prefix 替换 | `context-compression` | compression 只发生在 Y/X projection |
| CTX-011 | 候选严格新于已提交 epoch；无候选走正常请求；digest 失配 fail closed | `prefix-stability` | candidate coverage；→`context-compression` |
| CTX-013 | Blogger delta TOML：data-only 冻结、硬上限、decision-relevant reasoning、与 LWR gap 分投影 | `context-compression` | delta 输入；TOML 渲染→`provider-projection` |
| CTX-014 | 诊断边界：可观测诊断不得成控制输入，不用日志字段驱动 Fallback/probe/squash | `causal-wait` | diagnostic observation 非权威 |
| CTX-016 | WorkRecordStart Opening floor：Opening = 交托关闭前区间，结构性 cursor 非 Stage | `work-record` | Opening preserved；BlindPlan T1→`obligation-ledger`；Blogger effectiveStart→`context-compression` |

## NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| CTX-010 | 候选只进 ProjectionChoice，成功才提升，失败丢弃，无 PrefixProbeRolledBack | `context-compression` | candidate≠committed |
| CTX-010 | probe 成功 → PrefixRebaseCommitted(Probe) → ActivePrefixEpoch | `prefix-stability` | epoch promotion |
| CTX-012 | X probe 成功提交 epoch/失败无事实；Y squash 成功 BlogSquashCommitted | `prefix-stability` + `context-compression` | commit semantics 分属 |
| CTX-015 | ActivePrefixEpoch/PrefixRebaseCommitted 是唯一 epoch SSOT（Probe\|TodoCheckpoint）；commit 时点 seal 前、provider 成败不回滚 | `prefix-stability` | epoch SSOT |
| CTX-015 | desired cutoff 仅由 Accepted 链纯推导 | `obligation-ledger` | accepted obligation chain |
| CTX-015 | Y prefix materialize（PrefixCoverage-complete-turn，禁 RawGap） | `context-compression` | materialization |

---

# context.md 轮 delta

```text
Boundary: UNCHANGED 45。Coverage: 13 OWNED、3 NEEDS-SPLIT（CTX-010/012/015）、0 ORPHAN。
Proof: context failure-driven probe/squash/coverage → context-compression；prompt-stability prefix byte → prefix-stability。
Dependency: 无新增/删除。
```

## 边界观察

1. CTX-015 是「prefix-stability vs obligation-ledger vs context-compression」三方的精确交界：epoch SSOT 属 prefix-stability，desired cutoff 属 obligation-ledger（Accepted 链），Y materialization 属 context-compression。无独立 `todo-rebase` package。

---

# `docs/what/review.md`（+ shape/review.md + how/review.md）

`review-protocol` 已拆 `review-judgement` + `review-assurance`（HANDOFF §6.4）。

## OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| REVIEW-001 | `judge` 工具：typed `verdict = PERFECT\|REVISE`；无描述字段；回执不 echo | `review-judgement` | PERFECT/REVISE 语义合同 |
| REVIEW-002 | 任一 durable REVISE 立即关 cohort 与 continuation capability；FinalityRejected 另行 record-ready | `finality` | rejection 语义；record-ready→`review-assurance` |
| REVIEW-003 | 第二次 PERFECT 需 9 条件（同 Session/Barrier/tree、不同 run/call、seal 含 challenge…） | `review-assurance` | challenge 因果确认 |
| REVIEW-004 | ReviewAttemptIdentity 五元组；同 run 额外 PERFECT 不计数 | `review-assurance` | attempt identity |
| REVIEW-005 | 两条独立因果链：ConfirmationPrompt（发送） vs ChallengeEvidence（消费） | `review-assurance` | causal confirmation |
| REVIEW-006 | 自包含 ReviewWitness：独立回答谁审/哪 tree/两次 run/是否看 challenge | `review-assurance` | witness self-containment |
| REVIEW-007 | Manager 面无 Review Guard：ManagerWorkflow 只判 join/finality/planning/handedOff | `finality` | Manager 面 hidden review（GLORY-070） |
| REVIEW-008 | Git tree 变化使 witness 无效（pending 拒绝/confirmed 不可再 guard） | `review-assurance` | reviewed object 变化失效 |
| REVIEW-009 | Orchestrator 复审：rebase 后旧 witness 无效，须重新双 PERFECT | `review-assurance` | re-review；ff publish→`change-integration` |
| REVIEW-010 | ProviderInputSeal fail-closed：无法绑定 ProviderRunIdentity 则不确认 | `review-assurance` | seal 绑定；Host 因果读→`host-boundary` |
| REVIEW-011 | Examiner's Ledger 是判断方向非 checklist；PERFECT+minor 共存；material defect 才 REVISE | `review-judgement` | discrimination + proportionate |
| REVIEW-012 | Reviewer 提示词权威 = Role Law + Examiner's Ledger；双 PERFECT 流程不入提示词 | `cognitive-environment` | prompt 组合；双 PERFECT 不入提示→`review-assurance` |
| REVIEW-014 | VerdictKnown 与 ConsumableReview 两段式，禁单 Stage/bool | `review-assurance` | 可消费分型 |
| REVIEW-016 | 有界 canonical LWR + safety seal；三 request-range 用途 includeOpening=false | `work-record` | bounded canonical statement |
| REVIEW-017 | 同 snapshot record-ready；禁 timer/sleep/wall-clock 轮询 | `review-assurance` | fresh witness；event-driven→`causal-wait` |
| REVIEW-018 | 基础设施失败永远不是 PERFECT/REVISE，不伪造 settlement | `review-assurance` | infra failure ≠ REVISE（HANDOFF §18.8） |
| REVIEW-019 | 仅 proven loss 后替换 Dedicated；不确定 fail closed | `managed-session-lifecycle` | proven-loss replacement |
| REVIEW-020 | process verdict 不是终末 witness；process PERFECT ≠ terminal PERFECT | `review-assurance` | 代数分离；judgement 语义→`review-judgement` |

## NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| REVIEW-013 | TodoProcessReview 派生（每 Accepted 一次）、一次 terminal | `obligation-ledger` | process review cadence |
| REVIEW-013 | 过程判断语义（无 challenge、无 dual-PERFECT） | `review-judgement` | process verdict |
| REVIEW-013 | FinalityReview 的 challenge/witness/cohort 代数 | `review-assurance` | terminal witness |
| REVIEW-015 | 每 Life 一个 logical DedicatedTodoReviewer + enlist/replacement | `obligation-ledger` | Rk obligation |
| REVIEW-015 | dedicated session create/retire/graduate ≠ Dispose | `managed-session-lifecycle` | hidden session lifecycle |
| REVIEW-015 | Manager 不可见 dedicated session/barrier/witness | `participant-horizon` | hidden surface |

## GARBAGE / HOW — 不进入未来 WHAT

| 内容 | 判定 | 说明 |
|---|---|---|
| `verdict`（旧工具名）非法、`verdict` 参数字段保留 | HOW | 当前 vocabulary；参数名非永久 contract |
| 双 PERFECT 屏障由 Host 执行、Reviewer 提示词不灌输 | HOW | 实现位置，非 ontology |
| `ChallengeTextVersion=1`、英文 canonical 字节不变版本保持 | HOW | 文案世代机制 |

---

# review.md 轮 delta

```text
Boundary: UNCHANGED 45。Coverage: 18 OWNED、2 NEEDS-SPLIT（REVIEW-013/015）、0 ORPHAN。
Proof: seal/witness/challenge → review-assurance；judge/judgement semantics → review-judgement；canonical LWR → work-record。
Dependency: 无新增/删除。
```

## 边界观察

1. review.md 是 §6.4「review-protocol → review-judgement + review-assurance」的直接证据：REVIEW-001/011 属 judgement（判断语义），REVIEW-003..010/014/017/018/020 属 assurance（witness/seal/可消费），REVIEW-013/015 是 judgement×assurance×obligation-ledger×lifecycle 的交界。
2. REVIEW-018 确认 §18.8「infrastructure failure 不是 semantic REVISE」：基础设施失败 owner 在 `review-assurance`，不在 `review-judgement`。

---

# `docs/what/todo.md`（TODO-001..015）

Magic Todo 语义唯一 owner = `obligation-ledger`；跨域机制只引用不复制。

## OWNED — 单 owner

| Clause | Proposition | Future owner | Evidence / 边界 |
|---|---|---|---|
| TODO-002 | `todowrite(obligations:[{name,work}])`；CurrentObligations=last accepted；obligation 层级/可托付完整性/T1 特例 | `obligation-ledger` | obligation authoring surface + 语义；schema 是当前 surface |
| TODO-003 | 义务账禁 status 枚举机；duplicate name 语法拒绝；禁按 work 文本猜 identity | `obligation-ledger` | obligation identity/continuity |
| TODO-004 | Admission/replay/V2 门禁；同 message 多 todowrite 全拒；Same ToolCallId 幂等；失败分型（syntax/semantic REVISE/infra fatal） | `obligation-ledger` | admission；physical success→`effect-accounting`；infra fatal→`crash-reconciliation` |
| TODO-005 | Accepted 立即 supersede CurrentObligations；REVISE 不回滚 Tk、不 semanticMerge | `obligation-ledger` | accepted account supersession |
| TODO-007 | Canonical 投影 vs Host TodoTable compatibility sink；sink 永不反推 canonical | `obligation-ledger` | canonical single truth |
| TODO-011 | 新 Life CurrentObligations 空；升级瞬间一次 LegacyTodoSeedAdopted | `obligation-ledger` | 初始 account；migration-scoped |
| TODO-012 | 恢复只从 durable facts；禁止 Stage/布尔/时间猜；禁第二套 SSOT | `obligation-ledger` | recovery；no Stage PC→`structured-workflow`、durable→`crash-reconciliation` |
| TODO-014 | TODO-* 语义只在本文件定义一次；跨域只引用不复制 | `requirement-system` | single semantic ownership |

## NEEDS-SPLIT — 一个 Clause 多个独立 WHY

| Clause | 分出的 proposition | 各自 Future owner | Evidence / 边界 |
|---|---|---|---|
| TODO-001 | Manager BlindPlan lifecycle + T1 checkpoint 节拍 | `obligation-ledger` | mission lifecycle |
| TODO-001 | WorkRecordStart = OpeningBoundary 结构性 floor（非 Stage） | `work-record` | Opening floor |
| TODO-006 | 1:1 lag-1 process review cadence（Rk 不阻塞 Tk） | `obligation-ledger` | review cadence |
| TODO-006 | VerdictKnown vs ConsumableReview 两段式 | `review-assurance` | 可消费分型 |
| TODO-008 | dedicated reviewer 每 Life 一个 + frontier | `obligation-ledger` | Rk obligation |
| TODO-008 | canonical LWR includeOpening=false 三段 | `work-record` | bounded statement |
| TODO-008 | RecordCoverage（RawGap 允许）vs PrefixCoverage（禁 RawGap）分型 | `context-compression` | coverage 分型 |
| TODO-009 | PrefixRebaseCommitted → ActivePrefixEpoch SSOT；provider 成败不回滚 | `prefix-stability` | epoch SSOT |
| TODO-009 | desiredCutoff 仅由 Accepted 链推导 | `obligation-ledger` | accepted chain |
| TODO-009 | rebase 只消费 PrefixCoverage proven Y | `context-compression` | materialization |
| TODO-010 | suicide 是唯一 tail drain；零 checkpoint fail closed | `finality` | drain rule |
| TODO-010 | latest ConsumableReview await；REVISE → Life 继续 | `obligation-ledger` | checkpoint obligation |
| TODO-013 | MagicTodoManagerGuideline Manager-only fragment | `obligation-ledger` | guideline |
| TODO-013 | 隐藏 reviewer 表面（outcome/report 可见，身份/barrier/witness 不可见） | `participant-horizon` | hidden surface |
| TODO-013 | ProcessReviewLWR 复用 Finality safety-seal | `finality` | safety seal |
| TODO-015 | T1 = 第一次 Accepted = commitment；revelation 只经 conversation tool result | `obligation-ledger` | T1 commitment |
| TODO-015 | T1 call/result 属 constitutive OpeningMaterial | `work-record` | preserved Opening |

---

# todo.md 轮 delta

```text
Boundary: UNCHANGED 45。Coverage: 8 OWNED、7 NEEDS-SPLIT、0 ORPHAN。
Proof: MagicTodoProjection/admission/checkpoint/CurrentObligations → obligation-ledger；ConsumableReview → review-assurance；epoch → prefix-stability。
Dependency: 无新增/删除。
```

## 边界观察

1. todo.md 是 `obligation-ledger` 的语义核心，但 7 条 NEEDS-SPLIT 显示它严格限制在「账本」：review 可消费→`review-assurance`、epoch→`prefix-stability`、LWR→`work-record`、coverage→`context-compression`、hidden 面→`participant-horizon`、drain→`finality`。TODO-014 本身承认「跨域只引用不复制」= `requirement-system`。
2. TODO-012 的「禁止程序计数器」与 TODO-004 的「infra fatal vs semantic REVISE vs syntax red-text」三态失败分型，是 obligation-ledger 依赖 `structured-workflow`/`crash-reconciliation`/`review-judgement` 的关键证据。

---

# `docs/what/glory.md`（GLORY-001..076 + SURFACE-001..006，82 条款）

glory.md 是 `finality` 语义主载体；因条款密集且同域，采用分组覆盖。

## 分组 OWNED — 主 owner

| Clause 范围 | 主 owner | 说明 |
|---|---|---|
| GLORY-001/003/005/008/010/011/012/013/015/026/027/028/029/033/034/035/036/037/038/039/040/041/042/043/044/045/046/052/053/054/055/057/060/061/062/063/064/065/066/067/068/069/070/076 | `finality` | terminal lifecycle：suicide/cohort/roster/rejection/blessing/rest/Reawakening/Life 隔离/三种经验 |
| GLORY-004/006/022/023/024/025/047/049/050 | `work-record` | canonical LWR、Opening raw、三段标题、compression floor |
| GLORY-051/056/058/059/072/073 | `review-assurance` | request 绑定、dual-PERFECT 证明、tree fresh、record-ready |
| GLORY-002/030/031/032/048 + SURFACE-005 | `participant-horizon` | 隐藏 reviewer/barrier/witness 不进 Manager 面 |
| GLORY-009 | `structured-workflow` | 无持久程序计数器 |
| GLORY-074 | `obligation-ledger` | OpeningPolicy=BlindPlan、T1 commitment（跨域→`work-record`/`finality`） |
| GLORY-075 | `participant-identity` + `prefix-stability` | system prompt byte-identical + Persona 冻结 |
| GLORY-071 | `prefix-stability` | cold prompt boundary |
| SURFACE-001/002 | `provider-language` | 新增固定文案英文/LF |
| SURFACE-003/004 | `provider-projection` | typed data 不可反解；surface 唯一 owner（跨→`requirement-system`） |
| SURFACE-006 | `verification-system` | surface proof gate |

## 显著 NEEDS-SPLIT 条款（已分解进现有包）

| Clause | 分出的 proposition | Future owner |
|---|---|---|
| GLORY-002 | Manager 不得控制隐藏 Reviewer | `finality` + `participant-horizon` |
| GLORY-004 | REVISE/Blessing 反馈只来自 canonical LWR | `work-record` + `finality` |
| GLORY-030 | checkpoint 过程评审 outcome/report 窄例外 | `participant-horizon` + `obligation-ledger` + `finality` |
| GLORY-037/040/062 | TODO-010 尾抽干（await ConsumableReview） | `finality` + `obligation-ledger` |
| GLORY-044 | REVISE 立即 cohort 关闭 + 双轨交付 | `finality` + `review-assurance` |
| GLORY-056/057 | infra failure 非 REVISE + undecidable 恢复 | `review-assurance` + `crash-reconciliation` |
| GLORY-058 | dual-PERFECT 证明（process PERFECT 不计入） | `review-assurance` + `review-judgement` |
| GLORY-072/073 | record-ready 等待与 recovery | `review-assurance` + `work-record` + `causal-wait` |
| GLORY-074 | BlindPlan T1 = commitment；交托只在 conversation | `obligation-ledger` + `work-record` |
| GLORY-075 | 同 Life system prompt byte-identical；Persona 冻结 | `participant-identity` + `prefix-stability` |

## GARBAGE — legacy Activation / Birth / Labor

| 内容 | 判定 | 说明 |
|---|---|---|
| GLORY-014 `ManagerNarrative.PlanningTail`（legacy 冻结字节） | GARBAGE | 仅 legacy decode；生产 cutover 后不用 |
| GLORY-018 无生产 Activation + GLORY-019 Activation 文本 + GLORY-020 Activation continuation | GARBAGE | planning-only 两阶段已删 |
| GLORY-021 `WorkActivated` inert legacy fact + 历史 `ProtectedPrefixEnd` | GARBAGE | inert；Opening floor 由 WorkRecordStart 取代 |
| GLORY-016/017/023/024 中「Birth/Labor floor」「Activation 前置」措辞 | GARBAGE | 措辞退役；Opening protection 语义→`work-record` 保留 |

---

# glory.md 轮 delta

```text
Boundary: UNCHANGED 45。Coverage: 82 条款分组覆盖，0 ORPHAN；legacy Activation/Birth/Labor 判 GARBAGE。
Proof: suicide/finality/cohort/last_words → finality；seal/witness/challenge → review-assurance；LWR → work-record。
Dependency: 无新增/删除。
```

## 边界观察

1. glory.md 确认 `finality` 是「终结资格/cohort/rejection/blessing/rest」的 owner，但它的边界必须让出：record-ready→`review-assurance`、LWR→`work-record`、BlindPlan T1→`obligation-ledger`、hidden reviewer→`participant-horizon`、system prompt 字节→`participant-identity`/`prefix-stability`。
2. 大量 legacy Activation/Birth/Labor/PlanningTail/WorkActivated 条款（GLORY-014/016..021/023/024）判 GARBAGE——它们正是 HANDOFF §12「clean world 正面定义，不背历史墓碑」的对象。

---

# 单-owner 主导文件（分组覆盖）

## `docs/what/enforcer.md`（24 条款）

| 范围 | Future owner | 说明 |
|---|---|---|
| ENFORCER-001/003/004/020..025/040/060/061/062/063/070/170 | `behavior-diagnosis` | tip = diagnosis occurrence；cycle 原子提交、tip→RuleId、Observation 配对、rulebook folder SSOT |
| ENFORCER-071 | `guidance-delivery` | TipDeliveryFrontier vs TipSemanticCoverage 两轴、Full/Identity、reanchor redelivery |
| ENFORCER-010/011 | `capability-enforcement` | Blogger 工具面仅 chronicle；旧名 blog 非法 |
| ENFORCER-026 | `provider-projection` | Transport ≠ Semantic schema |
| ENFORCER-030 | `cognitive-environment` | Blogger 统一 system（Role Law 组合） |
| ENFORCER-002/072/073 | GARBAGE | score-vector 删除、catalog.json 废止（clean break） |

## `docs/what/casebook.md`（CASE-001..012）

`knowledge-reuse` 主导全部 12 条：Case = Q+A+可重放 observations、fetch 重放、freshness≠correctness、Bookkeeper、EventStore 权威、LRU、feature gating、并发 DomainConflict 收敛、低信任 index。交叉：EventStore→`durable-events`、observation capture→`repository-investigation`。

## `docs/what/strength.md`（STRENGTH-001..012）

`speculative-investigation` 主导全部 12 条：零影响基线、eligible opportunity、K0/K1/K2 预算、Replica authority、Candidate frame、Prepared≠历史、Promotion 由消费证据、Replay/XTrace closure、no-reflection、Predictor/control、熔断。交叉：persona/language 继承→`participant-identity`、read/glob/grep 同源→`capability-enforcement`、payload_refs→`durable-events`。

## `docs/what/sphinx.md`（SPHINX-001..010）

`epistemic-reasoning` 主导全部 10 条：生成式认识状态求解器、handle 绑定、MCP start/resume、EpistemicState 全局闭包、Proposal≠Evidence（No Free Information）、RootContract 保留分布、概率合格数值证据、经典算法可验证退化。MCP/wire 身份→`host-boundary`。

## `docs/what/js-tools.md`（JS-001..020）

`repository-programming` 主导全部 20 条：capability-projected surface、generated base-class exactness、file/glob/grep/rewrite/write、JSON return、sandbox、transaction staging、all-or-nothing commit、conflict/rollback、failure algebra。交叉：Synthetic TOML 渲染→`provider-projection`、事务 staging→`effect-accounting`/`durable-events`。

## `docs/what/fallback.md`（FALLBACK-001/004/005/008/010/011/012/013/014）

`provider-attempt-recovery` 主导全部 9 条：Fallback 属 Logical Run、推进不变量、有限预算、空/XML-only 不计、abort 残留不计、Host Attempt≠领域计数、槽内维护子请求、armed 合取、Persona/language/system prompt 跨 cursor 不变。交叉：身份字节不变→`participant-identity`/`provider-language`/`prefix-stability`。

---

# `docs/what/architecture.md`（ARCH-002..017，15 条款）

| Clause | Future owner | 说明 |
|---|---|---|
| ARCH-002 事件是信号不是数据 | `host-boundary` | 碎片事件不进业务层 |
| ARCH-003 不修改 OpenCode 本体 | `host-boundary` | 只现有 Hook/SDK |
| ARCH-004 前缀缓存保护 | `prefix-stability` | 单一 ActivePrefixEpoch SSOT、冷边界三证据源 |
| ARCH-005 恢复哲学 | `crash-reconciliation` + `structured-workflow` | 恢复重入普通程序，不恢复协程 |
| ARCH-006 命名（人名词/工具动词） | `action-affordance` | 动作名表达 semantic act；commission≠fork |
| ARCH-007 工具名引用完整性 | `action-affordance` + `capability-enforcement` | same tool name = same contract everywhere |
| ARCH-008 禁止词 Stage/Phase/Lease/Owner/Generation | `structured-workflow` | 无程序计数器 |
| ARCH-010 合成文本 TOML Instruction/Data | `provider-projection` | layout/escaping 只拥有 representation |
| ARCH-011 状态先于表示 | `provider-projection` | representation 不反向创造 authority/state |
| ARCH-012 自定义 Tool 文本结果有界 | `host-boundary` | tool result wire bound（2000 行/51200 字节） |
| ARCH-013（空缺 Student/Teacher） | GARBAGE | migration absence |
| ARCH-014 Provider Horizon | `participant-horizon` | decision filter + 小法则 |
| ARCH-015 WorkRecord 散文非 schema | `work-record` | prose claim，无固定 DTO |
| ARCH-016 静态 Gates A–F | `verification-system`（机制）+ 各 semantic owner（A→capability-enforcement、B→participant-horizon、C→provider-language、D→prefix-stability/participant-identity、E→provider-language、F→office-capability） | gate 机制可共享，语义 oracle 唯一 owner |
| ARCH-017 Office Capability Model | `office-capability` | canonical 五分法 + entitled consequence |

## `docs/what/flow.md`（FLOW-001/002/004/005/008）→ `structured-workflow` 全部 5 条

流程由语言表达、DSL 直接执行、纯决策与效果分离、恢复重入普通流程、用可观察效果测试。

## `docs/what/loop.md`（LOOP-001/006/007/008）→ `degeneration-guard` 全部 4 条

低多样性/短句循环检测、LoopKill→FallbackController 桥接（不另造预算）、作用域与豁免、与既有恢复协议关系。

## `docs/what/orchestrator.md`（ORCH-001/002/007/008）→ `change-integration` 全部 4 条

commission 委托（→`delegation`）、Clean Gate、target ref 安全、崩溃恢复从 Journal 事实 fold（→`crash-reconciliation`）。

## `docs/what/projection.md`（PROJ-001..009）→ `provider-projection` 主导

投影是代数（无 AST+Interpreter）、输入事实快照、输出管线（SemanticEventTree→Semantic→Wire→Seal）、DSL 不负责生命周期。PROJ-009 MagicTodoProjection：canonical todo→`obligation-ledger`、禁止第二 LWR renderer→`work-record`、coverage 分型→`context-compression`。

## `docs/what/dsl-structured-program.md`（DSL-001..015）

| 范围 | Future owner | 说明 |
|---|---|---|
| DSL-001..007/013/014/015 | `structured-workflow` | 语言结构表达、状态标签对应物理事物、纯决策/效果分层、恢复重入、mutable 仅物理资源、Semantic Vocabulary/Compression/Decorator |
| DSL-012 业务异步等待因果观测 | `causal-wait` | process-local diagnostic observation 非权威 |

## `docs/what/document-governance.md`（GOV-001..012）

| 范围 | Future owner | 说明 |
|---|---|---|
| GOV-002/005/006/007/008/009/011/012 | `requirement-system` | 唯一 owner、唯一定义位置、单文件 lifecycle、层归属、直接闭环小变更 |
| GOV-002/011（proof 层） | `verification-system` | 证明义务层级 |
| GOV-001/003/004/010（当前 docs/ 5-layer + changes/ 目录机制 + clean break 引用） | HOW/GARBAGE | 当前文件层级；未来由 `requirements/<package>/` 取代，不迁入永久 WHAT |

## `docs/what/synthetic-toml.md` + `docs/what/glossary.md`

均无独立条款：synthetic-toml.md 只把 ARCH-010/011/012 路由到 surface（→`provider-projection`）；glossary.md 只做术语导航，冲突时以被指正式定义为准（→各 semantic owner）。不产生新 owner。

---

# 全仓反向覆盖 — 完成总结

## 范围

`docs/what/` 全部 25 个文件（含 shape/how 同前缀条款）已逐 proposition 判 future owner。
累计 ~418 条款。

## 全局结论

```text
current accepted truth = conjunction of 45 packages，无一新增/删除/合并
```

- **0 新包、0 ORPHAN、0 OVERLAP、0 dependency delta**：每一条现行规范命题都能映射到现有 45 包之一；无命题需要新 semantic owner。
- **所有 NEEDS-SPLIT 均已被现有包分解吸收**：现行 Clause 里的多 WHY（如 PROMPT-008、HOST-013、REVIEW-013、TODO-006、CTX-015）在 future ontology 中已是不同 package，无需改包集合。
- **GARBAGE 集中确认**：Student/Teacher/Meditator/Executor absence、exact 22-agent catalog、legacy Activation/Birth/Labor/WorkActivated、ScoreVector、旧 NDJSON/目录 blob、`OpeningPromptRaw`、旧标题/`Closing report` DTO——均为 migration/clean-break sediment，不进入永久 WHAT。
- **HOW 常数确认**：`MaxJoinBatch=32`、`RecoveryTailWindow=50`、`200 KiB`、`MaxKeywords=8`、MCP uvx/ref/env、wire 分隔符——具体数值非永久 contract。

## 维持的 WATCH / DEFERRED

1. `intra-participant-parallelism`（Fission）— DEFERRED：MVP capacity refusal 不立包（HANDOFF §10.1）。
2. NEEDHELP / Pair Hint — WATCH：interaction-authority + delegation + provider-projection + prefix-stability + cognitive-environment + degeneration-guard + host-boundary 已能组合解释，未发现独立 WHY，不立 `collaboration-guidance`（HANDOFF §10.2）。
3. `runtime-resource-integrity` — open question：distribution 与 provider-language 的 resource closure 仍需在 Phase C/D 观察是否形成独立 failure meaning（HANDOFF §10.3）。

## Phase A 完成标志

HANDOFF §23 Definition of Done 第 1 条（全部 `docs/what` propositions 已 reverse-classify，无未解释 ORPHAN）现已满足。后续 Phase B（WHY 反审计）、C（source/runtime evidence）、D（test/gate 覆盖）、E（dependency audit）、F（cutover 设计）仍待进行。

---

# Phase B — WHY 反审计（45 包 × docs/why + completed changes）

## 方法

对每个 future package 回到 `docs/why/*.md`（25 文件）+ `changes/completed/`（41 份）逐包问四问：

1. 是否真的只有一个不可替代 WHY？
2. 有没有另一个完全不同 failure meaning 被塞进来（double-WHY）？
3. 当前 DOES NOT OWN 是否足够硬？
4. 是否只是当前 mechanism 被误认为需求（假边界）？

## 45 包 verdict 表

| Package | WHY verdict | why-doc 证据 | flag |
|---|---|---|---|
| `requirement-system` | 单 WHY（meta：唯一 owner + 同时为真 + 无裸权威） | document-governance.md | — |
| `verification-system` | 单 WHY（meta：可失败可重放证据体系） | document-governance.md + verify proof | — |
| `structured-workflow` | 单 WHY（无第二程序计数器） | dsl-structured-program.md + flow.md + ce-temporal-ownership.md | — |
| `time-capability` | 单 WHY（时间显式 capability，非 ambient） | execution.md/loop.md deadline + IClockPort | — |
| `causal-wait` | 单 WHY（observation 可诊断但非权威） | causal-ce-observability.md + waitfact-causal-renewal.md | weak dep |
| `session-ontology` | 单 WHY（execution class × ownership × personhood 正交） | host.md why §15 HOST-008 | — |
| `managed-session-lifecycle` | 单 WHY（单一 create/reuse/retire/replacement 合同） | host.md why §15 | — |
| `host-boundary` | 单 WHY（业务只依赖可验证 Host 物理能力） | host.md why §1–9 + ARCH-002/003 | — |
| `participant-identity` | 单 WHY（Role≠Persona≠Binding） | agent.md why §1 + fallback.md why | — |
| `office-capability` | 单 WHY（office 由 entitled consequence 定义） | agent.md why + ARCH-017 | — |
| `capability-enforcement` | 单 WHY（可见 capability 与可执行 capability 同源不扩权） | agent.md why AGENT-006/007 + js-tools.md why 四层同构 | OVERLAP 已修 |
| `participant-horizon` | 单 WHY（只让行动相关最小事实穿过） | architecture.md why Provider Horizon + execution.md leak 禁令 | — |
| `cognitive-environment` | 单 WHY（长期 cognition 与 runtime/mission 分离；knowledge≠authority） | prompt.md why Library | — |
| `action-affordance` | 单 WHY（调用瞬间 act contract 五问） | prompt.md why PROMPT-020/021 + architecture.md why 关键区别 | — |
| `provider-language` | 单 WHY（life 单一稳定语言世界；protocol id 不译） | prompt.md why + host.md why §21 | — |
| `provider-projection` | 单 WHY（typed intent → 确定性表示；表示不反解 authority） | projection.md why + synthetic-toml.md why | — |
| `external-investigation` | 单 WHY（public-web facts 以 provenance 建立） | agent.md why Browser | — |
| `interaction-authority` | 单 WHY（PhysicalUserMessage≠AuthorityTurn） | prompt.md why §1 | — |
| `dispatch-protocol` | 单 WHY（已授权 interaction 过不可靠 Host 不复制逻辑效果） | prompt.md why Dispatcher 四阶段 | — |
| `effect-accounting` | 单 WHY（Requested/unknown/Accepted 分型） | persist.md why PERSIST-009 + storage.md §45 | — |
| `durable-events` | 单 WHY（单一可重放 durable substrate） | persist.md why + storage.md | — |
| `durable-convergence` | 单 WHY（replica 按对象语义收敛，无 LWW） | storage.md §10.9 + casebook.md why | — |
| `delegation` | 单 WHY（语义工作转交时 authority/charge/owner/return 明确） | execution.md why + orchestrator.md why | — |
| `process-execution` | 单 WHY（真实进程/PTY 有界可终止物理完成） | execution.md why PTY onExit | — |
| `output-distillation` | 单 WHY（大输出诚实有损压缩，fragment≠成功） | agent.md why Distiller | — |
| `change-integration` | 单 WHY（独立道路进共享 ref 短原子门，长 review 不串行） | orchestrator.md why | — |
| `semantic-trace` | 单 WHY（append-only 可定位原始语义历史） | companion.md why XTrace + host.md why | — |
| `work-record` | 单 WHY（bounded canonical 跨边界 work statement） | companion.md why + todo.md why + review.md why | — |
| `context-compression` | 单 WHY（仅失败驱动、证据边界明确地替换可压缩区） | context.md why | — |
| `prefix-stability` | 单 WHY（同 epoch 已呈现前缀 byte-stable） | host.md why §9–13 + context.md why | — |
| `provider-attempt-recovery` | 单 WHY（失败后有界换 binding 不换身份/权威） | fallback.md why | — |
| `crash-reconciliation` | 单 WHY（restart 从 durable facts 重入普通程序） | persist.md why + storage.md §39 | — |
| `degeneration-guard` | 单 WHY（病态重复提前止损，桥接标准 recovery） | loop.md why | — |
| `obligation-ledger` | 单 WHY（持续维护仍欠世界什么，非 phase 伪装） | todo.md why | — |
| `review-judgement` | 单 WHY（PERFECT/REVISE 是 discrimination 非表演） | review.md why | — |
| `review-assurance` | 单 WHY（judgement 何时可消费） | review.md why + glory.md why | — |
| `finality` | 单 WHY（不可逆结束资格非自宣） | glory.md why | fake dep 已删 |
| `behavior-diagnosis` | 单 WHY（诊断需 trigger/negative/distinction） | enforcer.md why | — |
| `guidance-delivery` | 单 WHY（诊断成立≠必重复告知） | enforcer.md why | weak dep |
| `repository-investigation` | 单 WHY（repository claim 由真实观察建立） | casebook.md why + agent.md why inspect | — |
| `knowledge-reuse` | 单 WHY（旧知识 best-effort cache 非当前证明） | casebook.md why | — |
| `repository-programming` | 单 WHY（单一 bounded transactional 能力同构编程面） | js-tools.md why | OVERLAP 已修 |
| `speculative-investigation` | 单 WHY（零影响 speculation 才可换成本收益） | strength.md why | — |
| `epistemic-reasoning` | 单 WHY（生成不增知识；proposal≠evidence） | sphinx.md why | — |
| `distribution` | 单 WHY（artifact 携带 runtime closure） | enforcer.md why dist 双副本 + package.json | — |

## 结论

- **0 double-WHY、0 假边界 → 0 拆包、0 并包、0 新包、0 删包。** 45 包逐一通过单 WHY + DOES NOT OWN hardness + independent-change 测试。
- **假边界「反例确认」**：`sphinx→epistemic-reasoning` 改名正确地把 F#/MCP/A*/Bayes/MCTS 降为 HOW（sphinx.md why「为什么 A*/Bayes/MCTS 必须是真退化」+「为什么改成 Wanxiangshu.Sphinx F#」都只是 implementation/proof 证据）；`companion` / `synthetic-toml` / `agent-catalog` / `mcp` / `sync-delegate` 继续确认不立包（why-doc 无一条需要独立 owner）。
- **1 OVERLAP 修复**：`repository-programming` 与 `capability-enforcement` 曾同时 claim「capability → surface → runtime gate 四层同构」。修复：同构/同源律唯一归 `capability-enforcement`；`repository-programming` 只应用它到编程面，新增 `capability-enforcement` hard edge（17-repository.md / 20-capability-external.md）。
- **1 假依赖删除**：`finality → managed-session-lifecycle`。life completion 触发的 dedicated reviewer session 退休是下游 effect（由 `managed-session-lifecycle` owner-closure 消费），不是 finality 定义前提（15-mission-review.md）。

## 转入 Phase E 的弱依赖候选（本轮只标记，不删）

1. `structured-workflow → causal-wait`：causal-wait 的「observation 非权威」不依赖「无程序计数器」，是 CE builder 的 implementation coupling。
2. `time-capability → causal-wait`：卡内自注「当等待需要 deadline 时」，是条件依赖，非 hard prerequisite。
3. `guidance-delivery → provider-projection`：delivery 的 occurrence/coverage 语义不依赖 renderer；渲染是下游机制。
4. `finality → participant-horizon`：finality「隐藏机制只暴露 consequence」是 horizon-respecting 约束（与 delegation 同型），薄依赖，Phase E 再定。

## Boundary / coverage / proof delta

```text
Boundary:   UNCHANGED 45（无拆/并/增/删）
Coverage:   本轮不新增 OWNED/ORPHAN/OVERLAP 条款（clause 级已在 Phase A 闭环）
Proof:      无新 proof 归属变化
Dependency: +1 edge repository-programming → capability-enforcement
            -1 edge finality → managed-session-lifecycle
            （净 0；仍 90 edges、0 cycle、0 unknown ref）
```

## 确认的跨域边界纪律（不立新包）

「failure-domain separation」（tool red / semantic REVISE / infra fatal）在 todo/review/glory why 中反复出现，但三态分别由 `capability-enforcement`（tool 语法红）、`review-judgement`（语义 REVISE）、`host-boundary`/`crash-reconciliation`（infra fatal fail-fast）各司其职；`review-assurance` 的「infra failure 不伪装 REVISE」是其 review-side 本地负边界。三者是硬边界纪律，不是第四个独立 WHY，故不立包。
