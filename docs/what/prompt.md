# Prompt Authority — 可观察行为

条款前缀：`PROMPT-`。  
Dispatcher 与 Profile 所有权见 `shape/prompt.md`。  
发送格式、来源解析与未决恢复见 `how/prompt.md`。

## PROMPT-001：顶层不变量

```text
PhysicalUserMessage ≠ AuthorityTurn
```

Host 上的 `role=user` 只是运输格式。下列内容都不是 Authority 身份证据：

- 零宽字符、空白、固定模板  
- 时间戳、文本长度  
- Synthetic TOML 的 comment / field 形态（ARCH-010、ARCH-011）

身份只能由本章的 typed 来源机制证明。

## PROMPT-002：只有 Authority Root 可以

Authority Root 独有权限：

1. 创建新的 Logical Run  
2. 选择或改变 SelectedAgent（并确定 PeerAgent / CanonicalRole / SelectedTier / ExecutionBinding）  
3. 成为新的 Fallback root  
4. 重置 Interaction Repair 预算  
5. 成为后续缺省 SelectedAgent 的延续来源  

Authority Root **不得**：

- 改变 Companion 关联（COMPANION-002：Session 结构事实）  
- 选择或覆盖 model ID（发送时始终 `Model = None`）  
- 重绑或改写 SessionPersona（AGENT-028/029；PROMPT-014：Persona 在 session 创建时一次冻结）

SelectedAgent / ExecutionBinding 的变化（含 Peer Fallback、Strength）**只**改执行绑定；Persona 与 system prompt 身份字节不变。

## PROMPT-003：Continuation 的禁区

下列均为 Continuation，**不得**执行 PROMPT-002 所列操作：

```text
InteractionRepair
JoinGuard
ManagerGuard
ReviewerGuard
ReviewConfirmation
BusyAgentNudge
ProviderRetryAttempt
SyncDelegateIdleNudge
```

历史 `TeacherQuestion` / `TeacherIdleNudge` / `StudentCompile` / `StudentCompileNudge`：
**G3 已删除（absent）**（PROMPT-012 空缺）。不得再列为现行 Continuation。

`ManagerGuard` 仅保留用于历史 journal 行解析（PromptAuthority.fromString），生产不再发送该 continuation（GLORY-070）。

Continuation 只延续已有 Logical Run：

- 不新建 RunId / completion  
- 不改 SelectedAgent / PeerAgent / CanonicalRole / SelectedTier  
- 不更新 LastAuthorityProfile  
- 不重置 Fallback / repair  
- 物理请求使用当前 Fallback cursor 的 EffectiveAgent  

`JoinGuard` 语义见 EXEC-016。

## PROMPT-018：Assistance continuation 不改变 authority

`NeedHelpEscalation` 与 `NeedHelpAdvice` 是 typed ContinuationKind。二者延长当前 LogicalRun，必须复用现有 AuthorityRoot、CanonicalRole、Persona、system prompt 与 ToolCapabilitySet；不得建立 HumanRoot、不得 reset FallbackCursor、不得记作 ProviderRetryAttempt。

`NeedHelpEscalation` 唯一允许的 binding 变化是 fast owner 请求对应 deep peer；`NeedHelpAdvice` 必须绑定触发 consultation 的原 deep agent。synthetic Cursor Pair Hint 即使使用 provider `role=user|system` 的测试 encoder，也仍是 HOST-013 provider projection，不参与 PromptIngress/AuthorityRoot/OpeningMaterial；生产 Cursor 固定 assistant encoder。

## PROMPT-019：Provider-visible prose ownership

凡进入 participant horizon 的自然语言，全部受 ProviderLanguage 管辖（PROMPT-017）。

```text
Meaning belongs to its semantic owner.
Language belongs to the session.
Rendering belongs to machinery.
```

That cognitive environment has one language at a time. No feature may quietly speak another.

Language 集中为律；Meaning 仍按 semantic owner 分布。禁止巨型 `TranslationRegistry`。禁止业务代码 `match lang` / `if lang then …` 散落自然语言句子。

| Class | 规则 |
|-------|------|
| A Provider prose | 必须 i18n（system / Role / Library / runtime / finality / tool desc+consequence / assistance / review challenge / …） |
| B Technical literals | 永不翻译（tool names / args / wire / enum / path / command） |
| C Internal diagnostics | 不进 horizon → 不属 Provider i18n |

```text
semantic owner
  → resources/provider/<path>/{en,zh-CN}.md
ProviderResources
  → already-localized string
SyntheticToml / ToolHostCodec
  → layout / escaping only；接收已本地化串
```

`SyntheticToml` 只拥有布局与转义，不拥有 prose 语义。`ToolHostCodec` 接收已按 `SessionProviderLanguage` 本地化的 Description；工具合同跨语言同形（PROMPT-017 invariant 面）。

参数化散文：资源模板用 `{{name}}`；填入值不翻译。

Bound session：缺 localization ≠ 许可换语言；fail closed（禁 silent English fallback）。

Gate 0 / Batch 迁文属 Change 工作；本条立法，不定批次日程。装载见 `how/prompt.md`；所有权门禁见 ARCH-016 Gate E；成对资源、`{{placeholder}}` 结构 parity 与 Role Law semantic-anchor parity 见 ARCH-016 Gate C。

## PROMPT-004：来源类型

```fsharp
type PromptOrigin =
    | AuthorityRoot of RootAuthorityKind
    | Continuation of ContinuationKind
    | HostInternal
    | UnknownOrigin

type RootAuthorityKind = HumanRoot | AgentOwnerRoot
```

| 来源 | 行为 |
|------|------|
| HumanRoot | 必须显式 `fast-*` / `deep-*`；省略 → fail-closed |
| AgentOwnerRoot | Manager fork / Idle 新任务等；必须显式准确 Agent |
| UnknownOrigin | fail-closed：不更新 profile、不启 Fallback、不发 continuation |

## PROMPT-007：Fire-and-forget 的含义

Fire-and-forget **只**表示调用方不等待 PhysicalAccepted。  
不得绕过 claim、authority、持久化、幂等与错误记录。

禁止独立的 `postPromptFireAndForget` 旁路；统一为 Dispatcher 的 `AwaitMode.Detached`。

## PROMPT-010：禁止自激励

禁止下列「用合成身份抬升权限」：

```text
零宽 continuation → HumanRoot
repair continuation → 新的 repair 预算
Review confirmation → 改 Reviewer SelectedAgent
synthetic → 重置 Fallback Offset
B 侧重试 → 下一真人 root 默认 Agent
向 Host Prompt 覆盖 Model
```

## PROMPT-011：未决发送恢复

Host 已接受 prompt，但插件在写 Submitted / PhysicalAccepted 前崩溃时，判定物理落地的恢复协议。机制与 PromptKey 组成见 `how/prompt.md`。

常量：

```text
RecoveryTailWindow    = 50   // 读目标 Session 尾部以检索同 PromptKey 的 user 消息
RecoveryAttemptBudget = 3    // 跨越 3 次插件启动
```

行为：

1. 对每个 `Claimed` 或 `Submitted` 的 PromptKey：读目标 Session 尾部 `RecoveryTailWindow` 条，查找 metadata 含同一 PromptKey 的 `role=user`。
   - 找到 → 补写 `PhysicalAccepted`（真实 `msg_*`）
   - 未找到 → 保持 Pending，**不自动重发**
2. 第 `RecoveryAttemptBudget` 次启动仍无法证明物理落地 → `Abandoned(UnresolvedAfterRecovery)`。

合同：

```text
at-most-one logical effect + fail-closed unknown outcome
```

禁止：

```text
假装 exactly-once
用时间窗口代替 PromptKey
把 accepted-* 当物理落地
为清理挂起而重发
```

## PROMPT-012：（空缺）Student / Teacher Prompt 身份 — G3 已删除

**编号永久空缺。** G3 clean-break 删除 `fast|deep-student` HumanRoot、QA bootstrap、
`StudentLearn` / `StudentCompile`、`TeacherQuestion` / `TeacherIdleNudge` / `StudentCompileNudge`
与 Learn→Compile request-kind 切换。无 alias、无 deprecated Prompt 路径。

后继：SyncDelegate 首发与 `SyncDelegateIdleNudge`（PROMPT-003/005；HOST-008 / EXEC-026/028）；
插件 user-shaped message 仍一律经 PROMPT-005，不得直接 `prompt_async`。

## PROMPT-013：Magic Todo Manager 持续规划 guidance

Manager 的 provider-visible 规划指引是 **HOST-013 通用结对编程 guideline 的加法片段**，不是全局替换：

```text
general pair-programming guideline（HOST-013）
+
if CanonicalRole = Manager
   AND todowrite is provider-visible
then MagicTodoManagerGuideline
```

禁止把 Magic Todo 文案并入 `resources/provider/host/pair-programming-guideline` 或对其它角色投影。

`MagicTodoManagerGuideline` 冻结语义（全文 owner 见 TODO-013/015；协议本体见 TODO-001/002）：

```text
Keep the mission's living obligations truthful with todowrite.
Planning and execution are one continuous activity after entrustment.
Do not stop for a separate Activation / system-prompt phase.
Update todowrite when truthful decomposition / discovered work / progress changes.
obligations: [{ name, work }]; name stable while same obligation.
Do not remove an obligation merely to make the road look shorter.
Do not preserve an obligation after the work has genuinely discharged it.
Continue independent next-stage work while prior checkpoint is reviewed.
Each accepted todowrite consumes the preceding checkpoint review and starts the next.
Do not emit multiple todowrite calls in the same assistant message.
BlindPlan T1 = first accepted todowrite; revelation is conversation tool result only.
```

Pre-T1 Planning Table / T1 revelation / Living Mission 分属 TODO-015；不得经 Prompt 路径伪造 Activation。

### 可见 / 禁止 surface（与 GLORY-030 窄例外对齐）

Manager **允许**观察 Todo Checkpoint process-review 的：

```text
outcome（PERFECT / REVISE）
concrete report（ProcessReviewLWR，经既有 Finality safety-seal）
```

Manager **禁止**在 system prompt、continuation、schema、固定错误、tool description/result 中出现：

```text
reviewer / reviewer agent name / reviewer session
barrier / witness / 2N / finality cohort
confirmation rounds / hidden task 编排
```

窄例外仅限 Magic Todo process protocol（TODO-013）；不放宽 Finality 内部机制或其它 review surface。  
GLORY-030 / SURFACE-005 以本条 + TODO-013 为唯一允许的 PERFECT/REVISE/report 出口；其余 Manager 固定 surface 仍全面禁止。

V2 `todowrite` schema / description owner 见 TODO-002；admission 与 V2 hook 门禁见 TODO-004。  
不得经 Prompt 路径伪造 checkpoint、ConsumableReview 或 PrefixEpoch。

## PROMPT-014：System prompt 稳定性与 Persona 冻结

```text
SessionPersona          session 创建一次绑定，不可变（AGENT-028）
office system prompt    同一 Life 内 byte-identical（GLORY-075）
SessionProviderLanguage session 创建绑定，不可变（PROMPT-017）
ExecutionBinding        可随 Peer Fallback / Strength 变化
```

禁止因下列事件改写 system prompt 字节或重绑 Persona：

```text
BlindPlan T1 / entrustment revelation
Planning → Working（已删除）
Peer Fallback / Strength replica
process review / Finality
Host compaction / reanchor / recovery
```

`The system prompt names the office. The conversation tells you which road is yours.`  
T1 revelation 只走 conversation tool result（TODO-015）。  
SelectedAgent / Binding 变化 ≠ 换人；不得把 Binding 名冒充 Persona 自称（AGENT-029）。

## PROMPT-015：Prompt Composition Protocol

万象术没有单一 system prompt；每个 provider-facing 自然语言材料恰属一个主权威：

```text
World    what is universally true here          → Common Law + shared mythology
Role     who you are and what belongs to you    → Role Law（fast/deep 共享）
Library  inherited technical knowledge          → Office Library（PROMPT-016）
Runtime  what is true about this invocation now
Mission  what must become true in this assignment
```

Canonical composition（概念顺序 ≠ wire）：

```text
SYSTEM: Common Law → Role Law → Office Library
TOOLS:  current generated tool surface
RUNTIME / CONVERSATION: lifecycle and event-driven injections
USER / ASSIGNMENT: current mission
```

层可互相告知，不得互相冒充。冲突按语义所有权边界裁决，**不**设「更靠近 system 者胜」全序。  
Tools 不是 Role Prompt 章节：capability 变化不改人格；拥有 tool ≠ 获 authority。

六种生命周期文本只 orient，不 educate，不叠第二套 envelope：

```text
Activation / Reawakening / Continuation / Handoff / Fission / Departure
```

generic Activation ≠ Manager BlindPlan phase；不得触发 system prompt 替换（TODO-015）。  
语言资产须有稳定 semantic identity；文件名只存 localized authored representation。

## PROMPT-016：Office Library

Office Library = 角色继承的技术书籍集合。保存职位历代 craft；**不是** Common Law，不定义角色 authority。

```text
Law tells you what must remain true.
Role tells you what is yours to decide.
Books teach you how predecessors learned to do it well.
The assignment tells you what must become true now.
```

`Information may cross authority boundaries. Authority does not travel with it.`

三条独立轴：Normative Class × Delivery Mode × Audience。  
Class：Rulebook / Handbook / Ledger / Atlas / Field Notes。  
Delivery：Inherited Volume / Triggered Folio / Request-Bound Volume。  
Audience 绑 semantic role 或 request contract，不绑 model strength；fast/deep 不造第二套思想传统。

禁止：书扩大 Role 权；universal bible 灌每个 persona；同 role 的 fast/deep 异书；把隐藏编排写入 Reviewer 书；复制已有 canonical 成第二真源。若他处已有 SSOT，Library 组合引用。

初始 canonical volumes（正文另属资源）：Kolmogorov Book（Handbook）、The Rulebook、The Examiner's Ledger、The Book of Scarcity——分发矩阵见 GrandRewrite / shape，本条只定知识≠权威合同。

## PROMPT-017：ProviderLanguage

```fsharp
type ProviderLanguage =
    | English
    | SimplifiedChinese
```

第一版 EN / zh-CN 双语同时上线。

```text
global preference
    ↓ at session creation
SessionProviderLanguage (immutable)

child / attached / internal
    inherits owner/commissioner language

用户后续切全局语言
    → only future sessions
```

| Localizable | Invariant（永不翻译） |
|-------------|----------------------|
| system prompts / Role Law / Common Law / Office Library | tool names / argument names |
| tool descriptions / runtime instructions / consequences | wire field names / enum literals |
| Finality / T1 / hints / WorkRecord headings | paths / source identifiers / commands |
| Blogger/Bookkeeper/Distiller assignments | |

`A translation changes the language of the world, not the identifiers of its machinery.`  
Synthetic TOML：Comments ≈ instruction；Fields ≈ operands；每个 provider text owner 独立负责 EN + ZH（SURFACE-004）。  
进入 horizon 的 prose 所有权、三类字符串与装载路径见 PROMPT-019；本条只定 `ProviderLanguage` 类型与 session 绑定。
